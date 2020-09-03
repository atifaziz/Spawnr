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
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Reactive.Linq;
    using System.Threading.Tasks;
    using Mannex.Collections.Generic;
    using Mannex.Diagnostics;

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

    public interface ISpawnable<out T> : IObservable<T>
    {
        SpawnOptions Options { get; }
        ISpawnable<T> WithOptions(SpawnOptions options);
    }

    static class Spawnable
    {
        public static ISpawnable<T>
            Create<T>(SpawnOptions options,
                      Func<IObserver<T>, SpawnOptions, IDisposable> subscriber) =>
            new Implementation<T>(options, subscriber);

        sealed class Implementation<T> : ISpawnable<T>
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
            spawner.Spawn(path, args, stdout => stdoutKey.AsKeyTo(stdout),
                                      stderr => stderrKey.AsKeyTo(stderr));

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
                SpawnCore(path, options, stdoutSelector, stderrSelector).ToObservable();
        }

        static IEnumerable<T> SpawnCore<T>(string path, SpawnOptions options,
                                           Func<string, T>? stdoutSelector,
                                           Func<string, T>? stderrSelector)
        {
            var psi = new ProcessStartInfo(path, options.Arguments.ToString())
            {
                CreateNoWindow         = true,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };

            options.Update(psi);

            using var process = Process.Start(psi)!;

            var bc = new BlockingCollection<(T Result, Exception? Error)>();
            var drainer =
                process.BeginReadLineAsync(stdoutSelector is {} outf ? (stdout => bc.Add(ValueTuple.Create(outf(stdout), (Exception?)null))) : (Action<string>?)null,
                                           stderrSelector is {} errf ? (stderr => bc.Add(ValueTuple.Create(errf(stderr), (Exception?)null))) : (Action<string>?)null);

            Task.Run(async () => // ReSharper disable AccessToDisposedClosure
            {
                try
                {
                    var pid = process.Id;
                    var error = await
                        process.AsTask(dispose: false,
                                       p => p.ExitCode != 0 ? new Exception($"Process \"{Path.GetFileName(path)}\" (launched as the ID {pid}) ended with the non-zero exit code {p.ExitCode}.")
                                                            : null,
                                       e => e,
                                       e => e)
                               .ConfigureAwait(false);

                    await drainer(null).ConfigureAwait(false);

                    if (error != null)
                        throw error;

                    bc.CompleteAdding();
                }
                catch (Exception e)
                {
                    bc.Add((default(T), e));
                }

                // ReSharper restore AccessToDisposedClosure
            });

            foreach (var e in bc.GetConsumingEnumerable())
            {
                if (e.Error != null)
                    throw e.Error;
                yield return e.Result;
            }
        }
    }
}
