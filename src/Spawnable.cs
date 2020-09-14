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

    public partial interface ISpawnable<out T> : IObservable<T>,
                                                 IEnumerable<T>
    {
        string       ProgramPath { get; }
        SpawnOptions Options     { get; }
        ISpawner     Spawner     { get; }

        ISpawnable<T> WithProgramPath(string value);
        ISpawnable<T> WithOptions(SpawnOptions value);
        ISpawnable<T> WithSpawner(ISpawner value);
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

        public static ISpawnable<string> FilterOutput(this ISpawnable<OutputLine> source) =>
            source.Filter(StandardOutputKind.Output);

        public static ISpawnable<string> FilterError(this ISpawnable<OutputLine> source) =>
            source.Filter(StandardOutputKind.Error);

        static ISpawnable<string> Filter(this ISpawnable<OutputLine> source,
                                         StandardOutputKind kind) =>
            from e in
                source.WithOptions(source.Options.WithSuppressError(kind != StandardOutputKind.Error)
                                                 .WithSuppressOutput(kind != StandardOutputKind.Output))
            select e.Value;

        static ISpawnable<TResult>
            Select<T, TResult>(this ISpawnable<T> source, Func<T, TResult> selector) =>
            new Spawnable<TResult>(
                source.ProgramPath,
                source.Options,
                source.Spawner,
                (spawnable, observer) =>
                    source.WithProgramPath(spawnable.ProgramPath)
                          .WithOptions(spawnable.Options)
                          .WithSpawner(spawnable.Spawner)
                          .AsObservable()
                          .Select(selector)
                          .Subscribe(observer));

        public static IObservable<T> AsObservable<T>(this ISpawnable<T> spawnable) =>
            spawnable is null ? throw new ArgumentNullException(nameof(spawnable))
                              : spawnable;

        public static IEnumerable<T> AsEnumerable<T>(this ISpawnable<T> spawnable) =>
            spawnable is null ? throw new ArgumentNullException(nameof(spawnable))
                              : spawnable;

        public static ISpawnable<KeyValuePair<T, string>>
            Spawn<T>(string path, ProgramArguments args, T stdout, T stderr) =>
            Spawn(path, args, null, stdout, stderr);

        public static ISpawnable<KeyValuePair<T, string>>
            Spawn<T>(string path, ProgramArguments args,
                     IObservable<OutputLine>? stdin, T stdout, T stderr) =>
            Spawn(path, args, stdin,
                  line => KeyValuePair.Create(stdout, line),
                  line => KeyValuePair.Create(stderr, line));

        public static ISpawnable<T>
            Spawn<T>(string path, ProgramArguments args,
                     Func<string, T>? stdout,
                     Func<string, T>? stderr) =>
            Spawn(path, args, null, stdout, stderr);

        public static ISpawnable<T>
            Spawn<T>(string path, ProgramArguments args,
                     IObservable<OutputLine>? stdin,
                     Func<string, T>? stdout,
                     Func<string, T>? stderr) =>
            new Spawnable<T>(
                path,
                SpawnOptions.Create()
                            .WithArguments(args)
                            .WithInput(stdin),
                Spawner.Default,
                (spawnable, observer) =>
                    spawnable.Spawner.Spawn(spawnable.ProgramPath,
                                            spawnable.Options.WithSuppressOutput(stdout is null)
                                                             .WithSuppressError(stderr is null))
                                     .Select(e => e.IsOutput ? stdout!(e.Value) : stderr!(e.Value))
                                     .Subscribe(observer));

        public static ISpawnable<OutputLine>
            Spawn(string path, ProgramArguments args, IObservable<OutputLine> stdin) =>
            Spawn(path, args, stdin, OutputLine.Output, OutputLine.Error);

        public static ISpawnable<OutputLine>
            Spawn(string path, ProgramArguments args) =>
            Spawn(path, args, OutputLine.Output, OutputLine.Error);

        public static ISpawnable<string>
            Pipe(this IObservable<string> first, ISpawnable<string> second) =>
            second.Input(first);

        public static ISpawnable<OutputLine>
            Pipe(this ISpawnable<OutputLine> first, ISpawnable<OutputLine> second) =>
            second.Input(first);

        public static ISpawnable<OutputLine>
            Pipe(this IObservable<string> first, ISpawnable<OutputLine> second) =>
            second.Input(first.AsOutput());
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
