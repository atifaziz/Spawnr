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

    public static class SpawnModule
    {
        public static ISpawnable<string> Spawn(string path, ProgramArguments args) =>
            Spawn(path, args, null);

        public static ISpawnable<string> Spawn(string path, ProgramArguments args,
                                               IObservable<string>? stdin) =>
            Spawner.Default.Spawn(path, args, null, output => output, null)
                           .Input(stdin);

        public static ISpawnable<KeyValuePair<T, string>>
            Spawn<T>(string path, ProgramArguments args,
                     T stdout, T stderr) =>
            Spawn(path, args, null, stdout, stderr);


        public static ISpawnable<KeyValuePair<T, string>>
            Spawn<T>(string path, ProgramArguments args,
                     IObservable<string>? stdin, T stdout, T stderr) =>
            Spawner.Default.Spawn(path, args, null, stdout, stderr)
                           .Input(stdin);

        public static ISpawnable<T>
            Spawn<T>(string path,
                     ProgramArguments args,
                     Func<string, T>? stdout,
                     Func<string, T>? stderr) =>
            Spawn(path, args, null, stdout, stderr);

        public static ISpawnable<T>
            Spawn<T>(string path,
                     ProgramArguments args,
                     IObservable<string>? stdin,
                     Func<string, T>? stdout,
                     Func<string, T>? stderr) =>
            Spawner.Default.Spawn(path, args, null, stdout, stderr)
                           .Input(stdin);
    }
}
