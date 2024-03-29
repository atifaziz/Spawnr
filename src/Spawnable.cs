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
    using System.Reactive;
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

        public static ISpawnable
            Options(this ISpawnable spawnable,
                    Func<SpawnOptions, SpawnOptions> function)
        {
            if (spawnable is null) throw new ArgumentNullException(nameof(spawnable));
            if (function is null) throw new ArgumentNullException(nameof(function));

            return spawnable.WithOptions(function(spawnable.Options));
        }

        public static ISpawnable AddArgument(this ISpawnable spawnable, string value) =>
            spawnable.Options(opts => opts.AddArgument(value));

        public static ISpawnable AddArgument(this ISpawnable spawnable, params string[] values) =>
            spawnable.Options(opts => opts.AddArgument(values));

        public static ISpawnable AddArguments(this ISpawnable spawnable, IEnumerable<string> values) =>
            spawnable.Options(opts => opts.AddArguments(values));

        public static ISpawnable ClearArguments(this ISpawnable spawnable) =>
            spawnable.Options(opts => opts.ClearArguments());

        public static ISpawnable SetCommandLine(this ISpawnable spawnable, string value) =>
            spawnable.Options(opts => opts.SetCommandLine(value));

        public static ISpawnable ClearEnvironment(this ISpawnable spawnable) =>
            spawnable.Options(opts => opts.ClearEnvironment());

        public static ISpawnable AddEnvironment(this ISpawnable spawnable, string name, string value) =>
            spawnable.Options(opts => opts.AddEnvironment(name, value));

        public static ISpawnable SetEnvironment(this ISpawnable spawnable, string name, string value) =>
            spawnable.Options(opts => opts.SetEnvironment(name, value));

        public static ISpawnable UnsetEnvironment(this ISpawnable spawnable, string name) =>
            spawnable.Options(opts => opts.UnsetEnvironment(name));

        public static ISpawnable WorkingDirectory(this ISpawnable spawnable, string value) =>
            spawnable.Options(opts => opts.WithWorkingDirectory(value));

        public static ISpawnable CreateNoWindow(this ISpawnable spawnable) =>
            spawnable.Options(opts => opts.CreateNoWindow());

        public static ISpawnable Input(this ISpawnable spawnable,
                                       IObservable<OutputOrErrorLine>? value) =>
            spawnable.Options(opts => opts.WithInput(value));

        public static ISpawnable Input(this ISpawnable spawnable,
                                       IObservable<OutputLine>? value) =>
            spawnable.Input(value is {} lines
                            ? from line in lines
                              select OutputOrErrorLine.Output(line)
                            : null);

        public static ISpawnable Input(this ISpawnable spawnable,
                                       IObservable<string>? value) =>
            spawnable.Input(value is {} strs ? strs.AsOutput() : null);

        public static ISpawnable EmptyInput(this ISpawnable spawnable) =>
            spawnable.Options(opts => opts.EmptyInput());

        // Generic ISpawnable extensions for updating options

        public static ISpawnable<T>
            Options<T>(this ISpawnable<T> spawnable,
                       Func<SpawnOptions, SpawnOptions> modifier)
        {
            if (spawnable is null) throw new ArgumentNullException(nameof(spawnable));
            if (modifier is null) throw new ArgumentNullException(nameof(modifier));

            return spawnable.WithOptions(modifier(spawnable.Options));
        }

        public static ISpawnable<T> AddArgument<T>(this ISpawnable<T> spawnable, string value) =>
            spawnable.Options(opts => opts.AddArgument(value));

        public static ISpawnable<T> AddArgument<T>(this ISpawnable<T> spawnable, params string[] values) =>
            spawnable.Options(opts => opts.AddArgument(values));

        public static ISpawnable<T> AddArguments<T>(this ISpawnable<T> spawnable, IEnumerable<string> values) =>
            spawnable.Options(opts => opts.AddArguments(values));

        public static ISpawnable<T> ClearArguments<T>(this ISpawnable<T> spawnable) =>
            spawnable.Options(opts => opts.ClearArguments());

        public static ISpawnable<T> SetCommandLine<T>(this ISpawnable<T> spawnable, string value) =>
            spawnable.Options(opts => opts.SetCommandLine(value));

        public static ISpawnable<T> ClearEnvironment<T>(this ISpawnable<T> spawnable) =>
            spawnable.Options(opts => opts.ClearEnvironment());

        public static ISpawnable<T> AddEnvironment<T>(this ISpawnable<T> spawnable, string name, string value) =>
            spawnable.Options(opts => opts.AddEnvironment(name, value));

        public static ISpawnable<T> SetEnvironment<T>(this ISpawnable<T> spawnable, string name, string value) =>
            spawnable.Options(opts => opts.SetEnvironment(name, value));

        public static ISpawnable<T> UnsetEnvironment<T>(this ISpawnable<T> spawnable, string name) =>
            spawnable.Options(opts => opts.UnsetEnvironment(name));

        public static ISpawnable<T> WorkingDirectory<T>(this ISpawnable<T> spawnable, string value) =>
            spawnable.Options(opts => opts.WithWorkingDirectory(value));

        public static ISpawnable<T> Input<T>(this ISpawnable<T> spawnable,
                                             IObservable<OutputOrErrorLine>? value) =>
            spawnable.Options(opts => opts.WithInput(value));

        public static ISpawnable<T> Input<T>(this ISpawnable<T> spawnable,
                                             IObservable<OutputLine>? value) =>
            spawnable.Input(value is {} lines
                            ? from line in lines
                              select OutputOrErrorLine.Output(line)
                            : null);

        public static ISpawnable<T> Input<T>(this ISpawnable<T> spawnable,
                                             IObservable<string>? value) =>
            spawnable.Input(value is {} strs ? strs.AsOutput() : null);

        public static ISpawnable EmptyInput<T>(this ISpawnable<T> spawnable) =>
            spawnable.Options(opts => opts.EmptyInput());

        // Disambiguation extensions

        public static IObservable<T> AsObservable<T>(this ISpawnable<T> spawnable) =>
            spawnable is null ? throw new ArgumentNullException(nameof(spawnable))
                              : spawnable;

        public static IEnumerable<T> AsEnumerable<T>(this ISpawnable<T> spawnable) =>
            spawnable is null ? throw new ArgumentNullException(nameof(spawnable))
                              : spawnable;

        // Extensions related to capture outputs

        public static ISpawnable<OutputOrErrorLine> CaptureOutputs(this ISpawnable spawnable) =>
            spawnable switch
            {
                null => throw new ArgumentNullException(nameof(spawnable)),
                ISpawnable<OutputOrErrorLine> sp => sp,
                var sp => sp.CaptureOutputs(OutputOrErrorLine.Output, OutputOrErrorLine.Error)
            };

        public static ISpawnable<OutputLine> CaptureOutput(this ISpawnable spawnable) =>
            spawnable switch
            {
                null => throw new ArgumentNullException(nameof(spawnable)),
                ISpawnable<OutputLine> sp => sp,
                var sp => sp.CaptureOutputs(stdout: s => new OutputLine(s), stderr: null)
            };

        public static ISpawnable<ErrorLine> CaptureError(this ISpawnable spawnable) =>
            spawnable switch
            {
                null => throw new ArgumentNullException(nameof(spawnable)),
                ISpawnable<ErrorLine> sp => sp,
                var sp => sp.CaptureOutputs(stdout: null, stderr: s => new ErrorLine(s))
            };

        public static ISpawnable<OutputLine> FilterOutput(this ISpawnable<OutputOrErrorLine> spawnable) =>
            spawnable.CaptureOutput();

        public static ISpawnable<ErrorLine> FilterError(this ISpawnable<OutputOrErrorLine> spawnable) =>
            spawnable.CaptureError();

        public static ISpawnable<T>
            CaptureOutputs<T>(this ISpawnable spawnable, Func<string, T>? stdout,
                                                         Func<string, T>? stderr) =>
            new Spawnable<T>(
                spawnable,
                (spawnable, observer) =>
                    spawnable.Spawner.Spawn(spawnable.ProgramPath, spawnable.Options, stdout, stderr)
                                     .Subscribe(observer));

        // Conversion extensions

        public static ISpawnable<string> AsString(this ISpawnable<OutputOrErrorLine> spawnable) =>
            spawnable.CaptureOutputs(s => s, s => s);

        public static ISpawnable<string> AsString(this ISpawnable<OutputLine> spawnable) =>
            spawnable.CaptureOutputs(stdout: s => s, stderr: null);

        public static ISpawnable<string> AsString(this ISpawnable<ErrorLine> spawnable) =>
            spawnable.CaptureOutputs(stdout: null, stderr: s => s);

        // Extensions to setup pipes of spawnables

        public static ISpawnable<OutputOrErrorLine>
            Pipe(this ISpawnable first, ISpawnable second) =>
            second.CaptureOutputs().Input(first.CaptureOutputs());

        public static ISpawnable<OutputOrErrorLine>
            Pipe(this ISpawnable spawnable, params ISpawnable[] spawnables) =>
            spawnables.Aggregate(spawnable.CaptureOutputs(), (left, right) => left.Pipe(right));

        public static ISpawnable<OutputLine>
            Pipe(this ISpawnable<OutputLine> first, ISpawnable second) =>
            second.CaptureOutput()
                  .Input(from line in first.AsObservable()
                         select OutputOrErrorLine.Output(line));

        public static ISpawnable<OutputLine>
            Pipe(this ISpawnable<OutputLine> spawnable, params ISpawnable[] spawnables) =>
            spawnables.Aggregate(spawnable, (left, right) => left.Pipe(right));

        public static ISpawnable<OutputOrErrorLine>
            Pipe(this IObservable<string> input, ISpawnable spawnable) =>
            spawnable.CaptureOutputs().Input(input.AsOutput());

        public static ISpawnable<OutputOrErrorLine>
            Pipe(this IObservable<string> input, ISpawnable spawnable,
                 params ISpawnable[] spawnables) =>
            input.Pipe(spawnable).Pipe(spawnables);

        // Spawn methods

        public static ISpawnable Spawn(string path, ProgramArguments args) =>
            new Spawnable<Unit>(
                path,
                SpawnOptions.Create().WithArguments(args),
                Spawner.Default,
                (spawnable, observer) =>
                    spawnable.Spawner.Spawn<Unit>(spawnable.ProgramPath,
                                                  spawnable.Options, null, null)
                                     .Subscribe(observer));

        public static ISpawnable<KeyValuePair<T, string>>
            Spawn<T>(string path, ProgramArguments args, T stdout, T stderr) =>
            Spawn(path, args, null, stdout, stderr);

        public static ISpawnable<KeyValuePair<T, string>>
            Spawn<T>(string path, ProgramArguments args,
                     IObservable<OutputOrErrorLine>? stdin, T stdout, T stderr) =>
            Spawn(path, args, stdin, line => KeyValuePair.Create(stdout, line),
                                     line => KeyValuePair.Create(stderr, line));

        public static ISpawnable<T>
            Spawn<T>(string path, ProgramArguments args,
                     Func<string, T>? stdout, Func<string, T>? stderr) =>
            Spawn(path, args, null, stdout, stderr);

        public static ISpawnable<OutputOrErrorLine>
            Spawn(string path, ProgramArguments args, IObservable<OutputOrErrorLine> stdin) =>
            Spawn(path, args).Input(stdin).CaptureOutputs();

        public static ISpawnable<T>
            Spawn<T>(string path, ProgramArguments args,
                     IObservable<OutputOrErrorLine>? stdin,
                     Func<string, T>? stdout, Func<string, T>? stderr) =>
            Spawn(path, args).Input(stdin).CaptureOutputs(stdout, stderr);
    }

    [Flags]
    public enum ExecuteOptions
    {
        None,
        HideStandardOutput,
        HideStandardError,
    }

    partial class Spawnable
    {
        public static Task<ExitCode> Execute(this ISpawnable spawnable,
                                             CancellationToken cancellationToken = default) =>
            spawnable.Execute(ExecuteOptions.None, cancellationToken);

        public static Task<ExitCode> Execute(this ISpawnable spawnable,
                                             ExecuteOptions options,
                                             CancellationToken cancellationToken = default)
        {
            if (spawnable is null) throw new ArgumentNullException(nameof(spawnable));

            return _(); async Task<ExitCode> _()
            {
                static Func<string, Unit>? OptFunc(ExecuteOptions options, ExecuteOptions flags) =>
                    (options & flags) == flags ? new Func<string, Unit>(_ => default) : null;

                var tcs = new TaskCompletionSource<ExitCode>(TaskCreationOptions.RunContinuationsAsynchronously);

                using var subscription =
                    spawnable.Spawner.Spawn(spawnable.ProgramPath,
                                            spawnable.Options,
                                            OptFunc(options, ExecuteOptions.HideStandardOutput),
                                            OptFunc(options, ExecuteOptions.HideStandardError))
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
                    ? cancellationToken.Register(useSynchronizationContext: false,
                                                 callback: () =>
                                                 {
                                                     tcs.TrySetCanceled(cancellationToken);
                                                     subscription.Dispose();
                                                 })
                    : default;

                return await tcs.Task.ConfigureAwait(false);
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
