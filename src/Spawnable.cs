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
    using System.Linq;
    using System.Reactive.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public partial interface ISpawnable
    {
        string       ProgramPath { get; }
        SpawnOptions Options     { get; }
        ISpawner     Spawner     { get; }

        ISpawnable WithProgramPath(string value);
        ISpawnable WithOptions(SpawnOptions value);
        ISpawnable WithSpawner(ISpawner value);
        ISpawnable Update(ISpawnable other);
    }

    public partial interface ISpawnable<out T> : ISpawnable,
                                                 IObservable<T>,
                                                 IEnumerable<T>
    {
        new ISpawnable<T> WithProgramPath(string value);
        new ISpawnable<T> WithOptions(SpawnOptions value);
        new ISpawnable<T> WithSpawner(ISpawner value);
        new ISpawnable<T> Update(ISpawnable other);
    }

    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    sealed partial class Spawnable<T> : ISpawnable<T>
    {
        readonly Func<ISpawnable<T>, IObserver<T>, IDisposable> _subscriber;

        public Spawnable(string path, SpawnOptions options, ISpawner spawner,
                         Func<ISpawnable<T>, IObserver<T>, IDisposable> subscriber)
        {
            ProgramPath = path ?? throw new ArgumentNullException(nameof(path));
            Options     = options ?? throw new ArgumentNullException(nameof(options));
            Spawner     = spawner ?? throw new ArgumentNullException(nameof(spawner));
            _subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
        }

        public Spawnable(ISpawnable other,
                         Func<ISpawnable<T>, IObserver<T>, IDisposable> subscriber) :
            this(other.ProgramPath, other.Options, other.Spawner, subscriber) {}

        Spawnable(Spawnable<T> other) :
            this(other.ProgramPath, other.Options, other.Spawner, other._subscriber) {}

        public string ProgramPath { get; private set; }
        public ISpawner Spawner { get; private set; }
        public SpawnOptions Options { get; private set; }

        public ISpawnable<T> WithProgramPath(string value)
            => value is null ? throw new ArgumentNullException(nameof(value))
             : value == ProgramPath ? this
             : new Spawnable<T>(this) { ProgramPath = value };

        public ISpawnable<T> WithOptions(SpawnOptions value)
            => value is null ? throw new ArgumentNullException(nameof(value))
             : value == Options ? this
             : new Spawnable<T>(this) { Options = value };

        public ISpawnable<T> WithSpawner(ISpawner value)
            => value is null ? throw new ArgumentNullException(nameof(value))
             : value == Spawner ? this
             : new Spawnable<T>(this) { Spawner = value };

        public ISpawnable<T> Update(ISpawnable other) =>
            other == this ? this : new Spawnable<T>(other, _subscriber);

        ISpawnable ISpawnable.WithProgramPath(string value) => WithProgramPath(value);
        ISpawnable ISpawnable.WithOptions(SpawnOptions value) => WithOptions(value);
        ISpawnable ISpawnable.WithSpawner(ISpawner value) => WithSpawner(value);
        ISpawnable ISpawnable.Update(ISpawnable other) => Update(other);

        public IDisposable Subscribe(IObserver<T> observer) =>
            _subscriber(this, observer);

        public IEnumerator<T> GetEnumerator() =>
            this.ToEnumerable().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        string GetDebuggerDisplay() =>
            ProgramArguments.From(new[] { ProgramPath }.Concat(Options.Arguments))
                            .ToString();
    }

    public static partial class Spawnable
    {
        // Non-generic ISpawnable extensions for updating options

        public static ISpawnable AddArgument(this ISpawnable source, string value) =>
            source.WithOptions(source.Options.AddArgument(value));

        public static ISpawnable AddArgument(this ISpawnable source, params string[] values) =>
            source.WithOptions(source.Options.AddArgument(values));

        public static ISpawnable AddArguments<T>(this ISpawnable source, IEnumerable<string> values) =>
            source.WithOptions(source.Options.AddArguments(values));

        public static ISpawnable ClearArguments<T>(this ISpawnable source) =>
            source.WithOptions(source.Options.ClearArguments());

        public static ISpawnable SetCommandLine<T>(this ISpawnable source, string value) =>
            source.WithOptions(source.Options.SetCommandLine(value));

        public static ISpawnable ClearEnvironment<T>(this ISpawnable source) =>
            source.WithOptions(source.Options.ClearEnvironment());

        public static ISpawnable AddEnvironment<T>(this ISpawnable source, string name, string value) =>
            source.WithOptions(source.Options.AddEnvironment(name, value));

        public static ISpawnable SetEnvironment<T>(this ISpawnable source, string name, string value) =>
            source.WithOptions(source.Options.SetEnvironment(name, value));

        public static ISpawnable UnsetEnvironment<T>(this ISpawnable source, string name) =>
            source.WithOptions(source.Options.UnsetEnvironment(name));

        public static ISpawnable WorkingDirectory<T>(this ISpawnable source, string value) =>
            source.WithOptions(source.Options.WithWorkingDirectory(value));

        public static ISpawnable Input(this ISpawnable source,
                                       IObservable<OutputLine>? value) =>
            source.WithOptions(source.Options.WithInput(value));

        public static ISpawnable Input<T>(this ISpawnable spawnable,
                                          IObservable<string>? value) =>
            spawnable.Input(value is {} strs ? strs.AsOutput() : null);

        // Generic ISpawnable extensions for updating options

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

        public static ISpawnable<T> Input<T>(this ISpawnable<T> source,
                                             IObservable<OutputLine>? value) =>
            source.WithOptions(source.Options.WithInput(value));

        public static ISpawnable<T> Input<T>(this ISpawnable<T> spawnable,
                                             IObservable<string>? value) =>
            spawnable.Input(value is {} strs ? strs.AsOutput() : null);

        // Disambiguation extensions

        public static IObservable<T> AsObservable<T>(this ISpawnable<T> spawnable) =>
            spawnable is null ? throw new ArgumentNullException(nameof(spawnable))
                              : spawnable;

        public static IEnumerable<T> AsEnumerable<T>(this ISpawnable<T> spawnable) =>
            spawnable is null ? throw new ArgumentNullException(nameof(spawnable))
                              : spawnable;

        // Extensions related to capture outputs

        public static ISpawnable<OutputLine> CaptureOutputs(this ISpawnable spawnable) =>
            spawnable.CaptureOutputs(OutputLine.Output, OutputLine.Error);

        public static ISpawnable<string> CaptureOutput(this ISpawnable spawnable) =>
            spawnable.CaptureOutputs(stdout: s => s, stderr: null);

        public static ISpawnable<string> CaptureError(this ISpawnable spawnable) =>
            spawnable.CaptureOutputs(stdout: null, stderr: s => s);

        public static ISpawnable<string> FilterOutput(this ISpawnable<OutputLine> source) =>
            source.CaptureOutputs(stdout: s => s, stderr: null);

        public static ISpawnable<string> FilterError(this ISpawnable<OutputLine> source) =>
            source.CaptureOutputs(stdout: null, stderr: s => s);

        public static ISpawnable<T>
            CaptureOutputs<T>(this ISpawnable spawnable, Func<string, T>? stdout,
                                                         Func<string, T>? stderr) =>
            new Spawnable<T>(
                spawnable,
                (spawnable, observer) =>
                    spawnable.Spawner.Spawn(spawnable.ProgramPath,
                                            spawnable.Options.WithSuppressOutput(stdout is null)
                                                             .WithSuppressError(stderr is null))
                                     .Select(e => e.IsOutput ? stdout!(e.Value) : stderr!(e.Value))
                                     .Subscribe(observer));

        // Extensions to setup pipes of spawnables

        public static ISpawnable<string>
            Pipe(this IObservable<string> first, ISpawnable<string> second) =>
            second.Input(first);

        public static ISpawnable<OutputLine>
            Pipe(this ISpawnable<OutputLine> first, ISpawnable<OutputLine> second) =>
            second.Input(first);

        public static ISpawnable<OutputLine>
            Pipe(this IObservable<string> first, ISpawnable<OutputLine> second) =>
            second.Input(first.AsOutput());

        // Spawn methods

        public static ISpawnable Spawn(string path, ProgramArguments args) =>
            new Spawnable<OutputLine>(
                path,
                SpawnOptions.Create().WithArguments(args),
                Spawner.Default,
                (spawnable, observer) =>
                    spawnable.Spawner.Spawn(spawnable.ProgramPath,
                                            spawnable.Options.WithSuppressOutput(true)
                                                             .WithSuppressError(true))
                                     .Subscribe(observer));

        public static ISpawnable<KeyValuePair<T, string>>
            Spawn<T>(string path, ProgramArguments args, T stdout, T stderr) =>
            Spawn(path, args, null, stdout, stderr);

        public static ISpawnable<KeyValuePair<T, string>>
            Spawn<T>(string path, ProgramArguments args,
                     IObservable<OutputLine>? stdin, T stdout, T stderr) =>
            Spawn(path, args, stdin, line => KeyValuePair.Create(stdout, line),
                                     line => KeyValuePair.Create(stderr, line));

        public static ISpawnable<T>
            Spawn<T>(string path, ProgramArguments args,
                     Func<string, T>? stdout, Func<string, T>? stderr) =>
            Spawn(path, args, null, stdout, stderr);

        public static ISpawnable<OutputLine>
            Spawn(string path, ProgramArguments args, IObservable<OutputLine> stdin) =>
            Spawn(path, args).Input(stdin).CaptureOutputs();

        public static ISpawnable<T>
            Spawn<T>(string path, ProgramArguments args,
                     IObservable<OutputLine>? stdin,
                     Func<string, T>? stdout, Func<string, T>? stderr) =>
            Spawn(path, args).Input(stdin).CaptureOutputs(stdout, stderr);

        public static async Task<int> Async(this ISpawnable spawnable,
                                            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<int>();

            using var subscription =
                spawnable.Spawner.Spawn(spawnable.ProgramPath,
                                        spawnable.Options.WithSuppressOutput(true)
                                                         .WithSuppressError(true))
                                 .Subscribe(
                                     onNext: delegate {},
                                     onError: e =>
                                     {
                                         if (e is ExternalProcessException epe)
                                             tcs.TrySetResult(epe.ExitCode);
                                         else
                                             tcs.TrySetException(e);
                                     },
                                     onCompleted: () => tcs.TrySetResult(0));

            using var registration
                = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(
                      useSynchronizationContext: false,
                      callback: () =>
                      {
                          tcs.TrySetException(new TaskCanceledException(tcs.Task));
                          subscription.Dispose();
                      })
                : default;

            await tcs.Task.ConfigureAwait(false);
            return tcs.Task.Result;
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
        public static IAsyncEnumerable<T> AsAsyncEnumerable<T>(this ISpawnable<T> spawnable) =>
            spawnable is null ? throw new ArgumentNullException(nameof(spawnable))
                              : spawnable;
    }

    partial class Spawnable<T>
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

#endif // ASYNC_STREAMS
