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

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Spawnr.Tests")]

namespace Spawnr
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Reactive.Disposables;
    using System.Reactive.Linq;

    public static class SpawnModule
    {
        public static ISpawnable<string> Spawn(string path, ProgramArguments args) =>
            Spawner.Default.Spawn(path, args, output => output, null);

        public static ISpawnable<KeyValuePair<T, string>>
            Spawn<T>(string path, ProgramArguments args,
                     T stdoutKey, T stderrKey) =>
            Spawner.Default.Spawn(path, args, stdoutKey, stderrKey);

        public static ISpawnable<T>
            Spawn<T>(string path,
                     ProgramArguments args,
                     Func<string, T>? stdoutSelector,
                     Func<string, T>? stderrSelector) =>
            Spawner.Default.Spawn(path, args, stdoutSelector, stderrSelector);
    }

    public partial interface ISpawnable<out T> : IObservable<T>,
                                                 IEnumerable<T>
    {
        SpawnOptions Options { get; }
        ISpawnable<T> WithOptions(SpawnOptions options);
    }

    static partial class Spawnable
    {
        public static ISpawnable<T>
            Create<T>(SpawnOptions options,
                      Func<IObserver<T>, SpawnOptions, IDisposable> subscriber) =>
            new Implementation<T>(options, subscriber);

        sealed partial class Implementation<T> : ISpawnable<T>
        {
            readonly Func<IObserver<T>, SpawnOptions, IDisposable> _subscriber;

            public Implementation(SpawnOptions options,
                                  Func<IObserver<T>, SpawnOptions, IDisposable> subscriber)
            {
                Options = options ?? throw new ArgumentNullException(nameof(options));
                _subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
            }

            public SpawnOptions Options { get; }

            public ISpawnable<T> WithOptions(SpawnOptions value) =>
                ReferenceEquals(Options, value) ? this : Create(value, _subscriber);

            public IDisposable Subscribe(IObserver<T> observer) =>
                _subscriber(observer, Options);

            public IEnumerator<T> GetEnumerator() =>
                this.ToEnumerable().GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }

    public interface ISpawner
    {
        IObservable<T> Spawn<T>(string path, SpawnOptions options,
                                Func<string, T>? stdoutSelector,
                                Func<string, T>? stderrSelector);
    }

    public static class SpawnerExtensions
    {
        public static ISpawnable<T> AddArgument<T>(this ISpawnable<T> source, string value) =>
            source.WithOptions(source.Options.AddArgument(value));

        public static ISpawnable<T> AddArgument<T>(this ISpawnable<T> source, params string[] values) =>
            source.WithOptions(source.Options.AddArgument(values));

        public static ISpawnable<T> AddArguments<T>(this ISpawnable<T> source, IEnumerable<string> values) =>
            source.WithOptions(source.Options.AddArguments(values));

        public static ISpawnable<T> ClearArguments<T>(this ISpawnable<T> source) =>
            source.WithOptions(source.Options.ClearArguments());

        public static ISpawnable<T> SetCommandLine<T>(this ISpawnable<T> source, string value) =>
            source.WithOptions(source.Options.SetCommandLine(value));

        public static ISpawnable<T> ClearEnvironment<T>(this ISpawnable<T> source) =>
            source.WithOptions(source.Options.ClearEnvironment());

        public static ISpawnable<T> AddEnvironment<T>(this ISpawnable<T> source, string name, string value) =>
            source.WithOptions(source.Options.AddEnvironment(name, value));

        public static ISpawnable<T> SetEnvironment<T>(this ISpawnable<T> source, string name, string value) =>
            source.WithOptions(source.Options.SetEnvironment(name, value));

        public static ISpawnable<T> UnsetEnvironment<T>(this ISpawnable<T> source, string name) =>
            source.WithOptions(source.Options.UnsetEnvironment(name));

        public static ISpawnable<T> WorkingDirectory<T>(this ISpawnable<T> source, string value) =>
            source.WithOptions(source.Options.WithWorkingDirectory(value));

        public static ISpawnable<string> Spawn(this ISpawner spawner, string path, ProgramArguments args) =>
            spawner.Spawn(path, args, output => output, null);

        public static ISpawnable<KeyValuePair<T, string>>
            Spawn<T>(this ISpawner spawner,
                     string path, ProgramArguments args,
                     T stdoutKey, T stderrKey) =>
            spawner.Spawn(path, args, stdout => KeyValuePair.Create(stdoutKey, stdout),
                                      stderr => KeyValuePair.Create(stderrKey, stderr));

        public static ISpawnable<T>
            Spawn<T>(this ISpawner spawner, string path, ProgramArguments args,
                     Func<string, T>? stdoutSelector,
                     Func<string, T>? stderrSelector) =>
            Spawnable.Create<T>(SpawnOptions.Create().WithArguments(args),
                                (observer, options) =>
                                    spawner.Spawn(path, options,
                                                  stdoutSelector,
                                                  stderrSelector)
                                           .Subscribe(observer));
    }

    public static class Spawner
    {
        public static ISpawner Default => new Implementation();

        sealed class Implementation : ISpawner
        {
            public IObservable<T> Spawn<T>(string path, SpawnOptions options,
                                           Func<string, T>? stdoutSelector,
                                           Func<string, T>? stderrSelector) =>
                Observable.Create<T>(observer =>
                    Spawner.Spawn(path, options,
                                  stdoutSelector, stderrSelector,
                                  observer));
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
        };

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

            void Reset(ControlFlags flags) => _flags &= (~flags & ControlFlags.Mask);
            void Set(ControlFlags flags) => _flags |= flags;
            void Set(ControlFlags flags, bool value) { if (value) Set(flags); else Reset(flags); }
        }

        static IDisposable Spawn<T>(string path, SpawnOptions options,
                                    Func<string, T>? stdoutSelector,
                                    Func<string, T>? stderrSelector,
                                    IObserver<T> observer)
        {
            var psi = new ProcessStartInfo(path)
            {
                CreateNoWindow         = true,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };

            options.Update(psi);

            IProcess process = options.ProcessFactory(psi);
            process.EnableRaisingEvents = true;

            var pid = -1;
            var control = new Control();

            process.OutputDataReceived += CreateDataEventHandler(ControlFlags.OutputReceived, stdoutSelector);
            process.ErrorDataReceived += CreateDataEventHandler(ControlFlags.ErrorReceived, stderrSelector);
            process.Exited += delegate { OnExited(control); };

            process.Start();
            pid = process.Id;

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return Disposable.Create(() =>
            {
                lock (control)
                {
                    if (control.Exited || control.Killed)
                        return;
                    control.Killed = true;
                }

                try
                {
                    _ = process.TryKill(out var _);
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

            DataReceivedEventHandler CreateDataEventHandler(ControlFlags flags, Func<string, T>? selector) =>
                (_, args) =>
                {
                    bool killed, exited, outputsReceived;
                    lock (control)
                    {
                        control[flags] = true;
                        killed = control.Killed;
                        exited = control.Exited;
                        outputsReceived = control.OutputReceived && control.ErrorReceived;
                    }
                    if (killed)
                    {
                        if (outputsReceived)
                            process.Dispose();
                    }
                    else if (args.Data is {} line)
                    {
                        if (selector is {} f)
                            observer.OnNext(f(line));
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
                    outputsReceived = control.OutputReceived && control.ErrorReceived;
                }
                if (killed || !outputsReceived)
                    return;
                Conclude();
            }

            void Conclude()
            {
                if (process.ExitCode == 0)
                    observer.OnCompleted();
                else
                    observer.OnError(new Exception($"Process \"{Path.GetFileName(path)}\" (launched as the ID {pid}) ended with the non-zero exit code {process.ExitCode}."));
                process.Dispose();
            }
        }
    }
}

#if ASYNC_STREAMS

namespace Spawnr
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Threading;

    partial interface ISpawnable<out T> : IAsyncEnumerable<T> {}

    partial class Spawnable
    {
        partial class Implementation<T>
        {
            public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                var completed = false;
                var error = (Exception?)null;
                var queue = new ConcurrentQueue<T>();
                var semaphore = new SemaphoreSlim(initialCount: 0);

                using var subscription =
                    this.Subscribe(
                        item => { queue.Enqueue(item); semaphore.Release(); },
                        err  => { error = err        ; semaphore.Release(); },
                        ()   => { completed = true   ; semaphore.Release(); });

                while (true)
                {
                    await semaphore.WaitAsync(cancellationToken)
                                   .ConfigureAwait(false);

                    if (completed)
                        break;

                    if (error is {} err)
                        throw err;

                    var succeeded = queue.TryDequeue(out var item);
                    Debug.Assert(succeeded);
                    yield return item;
                }
            }
        }
    }
}

#endif // ASYNC_STREAMS
