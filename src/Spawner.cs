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

        static IDisposable Spawn<T>(string path, SpawnOptions options,
                                    Func<string, T>? stdoutSelector,
                                    Func<string, T>? stderrSelector,
                                    IObserver<T> observer)
        {
            var psi = new ProcessStartInfo(path, options.Arguments.ToString())
            {
                CreateNoWindow         = true,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };

            options.Update(psi);

            Process process = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true,
            };

            var pid = -1;
            var killed = false;
            var exited = false;

            process.OutputDataReceived += CreateDataEventHandler(stdoutSelector);
            process.ErrorDataReceived += CreateDataEventHandler(stderrSelector);
            process.Exited += delegate { OnExited(); };

            process.Start();
            pid = process.Id;

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return Disposable.Create(() =>
            {
                if (exited || killed)
                    return;
                killed = true;
                process.TryKill(out var _);
            });

            DataReceivedEventHandler CreateDataEventHandler(Func<string, T>? selector) =>
                (_, args) =>
                {
                    Debug.Assert(!exited);
                    if (!killed && args.Data is {} line && selector is {} f)
                        observer.OnNext(f(line));
                };

            void OnExited()
            {
                exited = true;
                using var _ = process; // dispose
                if (killed)
                    return;
                if (process.ExitCode == 0)
                    observer.OnCompleted();
                else
                    observer.OnError(new Exception($"Process \"{Path.GetFileName(path)}\" (launched as the ID {pid}) ended with the non-zero exit code {process.ExitCode}."));
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
