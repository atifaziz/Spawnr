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
    using System.Collections.Generic;
    using System.Reactive.Linq;

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

        public static ISpawnable<T> Input<T>(this ISpawnable<T> source, IObservable<OutputLine>? value) =>
            source.WithOptions(source.Options.WithInput(value));

        public static ISpawnable<string> Spawn(this ISpawner spawner, string path, ProgramArguments args) =>
            spawner.Spawn(path, args, output => output, null);

        public static ISpawnable<KeyValuePair<T, string>>
            Spawn<T>(this ISpawner spawner,
                     string path, ProgramArguments args, T stdout, T stderr) =>
            spawner.Spawn(path, args, null, stdout, stderr);

        public static ISpawnable<KeyValuePair<T, string>>
            Spawn<T>(this ISpawner spawner,
                     string path, ProgramArguments args,
                     IObservable<OutputLine>? stdin, T stdout, T stderr) =>
            spawner.Spawn(path, args, stdin,
                          line => KeyValuePair.Create(stdout, line),
                          line => KeyValuePair.Create(stderr, line));

        public static ISpawnable<T>
            Spawn<T>(this ISpawner spawner, string path, ProgramArguments args,
                     Func<string, T>? stdout,
                     Func<string, T>? stderr) =>
            spawner.Spawn(path, args, null, stdout, stderr);

        public static ISpawnable<T>
            Spawn<T>(this ISpawner spawner, string path, ProgramArguments args,
                     IObservable<OutputLine>? stdin,
                     Func<string, T>? stdout,
                     Func<string, T>? stderr) =>
            Spawnable.Create<T>(SpawnOptions.Create()
                                            .WithArguments(args)
                                            .WithInput(stdin),
                                (options, observer) =>
                                    spawner.Spawn(path, options, stdout, stderr)
                                           .Subscribe(observer));

        public static ISpawnable<string> Pipe(this IObservable<string> first,
                                              ISpawnable<string> second) =>
            second.Input(first);

        public static ISpawnable<OutputLine>
            Pipe(this ISpawnable<OutputLine> first, ISpawnable<OutputLine> second) =>
            second.Input(first);

        public static ISpawnable<OutputLine>
            Pipe(this IObservable<string> first, ISpawnable<OutputLine> second) =>
            second.Input(from line in first
                         select OutputLine.Output(line));
    }
}
