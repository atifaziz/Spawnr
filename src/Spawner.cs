#region Copyright (c) 2016 Atif Aziz. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

namespace Spawnr
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reactive;
    using System.Reactive.Disposables;
    using System.Reactive.Linq;

    public interface ISpawner
    {
        IObservable<T> Spawn<T>(string path, SpawnOptions options,
                                Func<string, T>? stdout,
                                Func<string, T>? stderr);
    }

    public static class Spawner
    {
        public static readonly ISpawner Default = new Implementation();

        sealed class Implementation : ISpawner
        {
            public IObservable<T>
                Spawn<T>(string path, SpawnOptions options,
                         Func<string, T>? stdout, Func<string, T>? stderr) =>
                Observable.Create<T>(observer =>
                    Spawner.Spawn(path, options, stdout, stderr, observer));
        }

        [Flags]
        enum ControlFlags
        {
            None           = 0b_0000,
            Exited         = 0b_0001,
            Killed         = 0b_0010,
            OutputReceived = 0b_0100,
            ErrorReceived  = 0b_1000,
            Mask           = 0b_1111,
        }

        sealed class Control
        {
            ControlFlags _flags;

            public bool Exited         { get => this[ControlFlags.Exited        ]; set => this[ControlFlags.Exited        ] = value; }
            public bool Killed         { get => this[ControlFlags.Killed        ]; set => this[ControlFlags.Killed        ] = value; }
            public bool OutputReceived { get => this[ControlFlags.OutputReceived]; set => this[ControlFlags.OutputReceived] = value; }
            public bool ErrorReceived  { get => this[ControlFlags.ErrorReceived ]; set => this[ControlFlags.ErrorReceived ] = value; }

            public bool this[ControlFlags flags]
            {
                get => (_flags & flags) == flags;
                set => Set(flags, value);
            }

            void Reset(ControlFlags flags) => _flags &= ~flags & ControlFlags.Mask;
            void Set(ControlFlags flags) => _flags |= flags;
            void Set(ControlFlags flags, bool value) { if (value) Set(flags); else Reset(flags); }
        }

        static IDisposable Spawn<T>(string path, SpawnOptions options,
                                    Func<string, T>? stdout, Func<string, T>? stderr,
                                    IObserver<T> observer)
        {
            var outputFlags = (stderr, stdout) switch
            {
                ({}  , null) => ControlFlags.ErrorReceived,
                (null, {}  ) => ControlFlags.OutputReceived,
                ({}  , {}  ) => ControlFlags.ErrorReceived | ControlFlags.OutputReceived,
                _ => ControlFlags.None,
            };

            var psi = new ProcessStartInfo(path)
            {
                CreateNoWindow         = options.CreateNoWindow, // effective only on Windows
                UseShellExecute        = false,
                RedirectStandardInput  = options.Input is {},
                RedirectStandardOutput = stdout is {},
                RedirectStandardError  = stderr is {},
            };

            options.Update(psi);

            IProcess process = options.ProcessFactory(psi);
            process.EnableRaisingEvents = true;

            var pid = -1;
            var control = new Control();
            IDisposable? inputSubscription = null;

            if (stdout is {})
                process.OutputDataReceived += CreateDataEventHandler(ControlFlags.OutputReceived, stdout);
            if (stderr is {})
                process.ErrorDataReceived += CreateDataEventHandler(ControlFlags.ErrorReceived, stderr);

            process.Exited += delegate { OnExited(control); };

            var subscription = Disposable.Create(() =>
            {
                lock (control)
                {
                    if (control.Exited || control.Killed)
                        return;
                    control.Killed = true;
                }

                try
                {
                    _ = process.TryKill(out _);
                }
                catch (Exception e)
                {
                    if (options.KillErrorFunction is {} f && f(e) is {} ee)
                    {
                        if (ReferenceEquals(ee, e))
                            throw;
                        else
                            throw ee;
                    }

                    // ...else all errors are ignored by default during
                    // disposal since the general expectation is that
                    // `IDisposable.Dispose` implementations do not throw.
                }
            });

            // Do maximum setup before starting process to avoid complications
            // that may arise with handling errors after.

            process.Start();
            pid = process.Id;

            try
            {
                if (stdout is {})
                    process.BeginOutputReadLine();
                if (stderr is {})
                    process.BeginErrorReadLine();

                if (options.Input is {})
                {
                    var writer = process.StandardInput;
                    var effects =
                        Observable.Concat(
                            from n in options.Input.Materialize()
                            select Observable.FromAsync(async () =>
                            {
                                try
                                {
                                    switch (n.Kind)
                                    {
                                        case NotificationKind.OnNext when n.Value is (OutputOrErrorKind.Output, var line):
                                            await writer.WriteLineAsync(line).ConfigureAwait(false);
                                            break;
                                        case NotificationKind.OnNext when n.Value is (OutputOrErrorKind.Error, var line)
                                                                       && stderr is {}:
                                            observer.OnNext(stderr(line));
                                            break;
                                        case NotificationKind.OnError:
                                            throw n.Exception;
                                        case NotificationKind.OnCompleted:
                                            await writer.FlushAsync().ConfigureAwait(false);
                                            writer.Close();
                                            break;
                                    }
                                }
                                catch (Exception e)
                                {
                                    subscription.Dispose();
                                    observer.OnError(e);
                                }
                                return Unit.Default;
                            }));

                    inputSubscription = effects.Subscribe();
                }
            }
            catch
            {
                subscription.Dispose();
                throw;
            }

            return subscription;

            DataReceivedEventHandler CreateDataEventHandler(ControlFlags flags, Func<string, T> selector) =>
                (_, args) =>
                {
                    bool killed, exited, outputsReceived;
                    lock (control)
                    {
                        if (args.Data is null)
                            control[flags] = true; // EOF
                        killed = control.Killed;
                        exited = control.Exited;
                        outputsReceived = control[outputFlags];
                    }
                    if (killed)
                    {
                        if (outputsReceived)
                            process.Dispose();
                    }
                    else if (args.Data is {} line)
                    {
                        observer.OnNext(selector(line));
                    }
                    else if (exited && outputsReceived)
                    {
                        Conclude();
                    }
                };

            void OnExited(Control control)
            {
                bool killed, outputsReceived;
                lock (control)
                {
                    control.Exited = true;
                    killed = control.Killed;
                    outputsReceived = control[outputFlags];
                }
                if (killed || !outputsReceived)
                    return;
                Conclude();
            }

            void Conclude()
            {
                inputSubscription?.Dispose();

                Exception? error = null;

                if (options.ExitCodeErrorFunction is {} ef)
                {
                    var args = new ExitCodeErrorArgs(path, options.Arguments, pid, process.ExitCode);
                    error = ef(args);
                }
                else
                {
                    if (process.ExitCode != 0)
                        error = new ExternalProcessException(process.ExitCode,
                                    $"Process \"{Path.GetFileName(path)}\" (launched as the ID {pid}) ended with the non-zero exit code {process.ExitCode}.");
                }

                if (error is null)
                    observer.OnCompleted();
                else
                    observer.OnError(error);

                process.Dispose();
            }
        }
    }
}
