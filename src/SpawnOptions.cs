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
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using SysProcess = System.Diagnostics.Process;

    public sealed class ExitCodeErrorArgs
    {
        public string Path { get; }
        public ProgramArguments Arguments { get; }
        public int Pid { get; }
        public int ExitCode { get; }

        public ExitCodeErrorArgs(string path, ProgramArguments args, int pid, int exitCode)
        {
            Path = path;
            Arguments = args;
            Pid = pid;
            ExitCode = exitCode;
        }
    }

    public sealed class SpawnOptions
    {
        public static SpawnOptions Create() =>
            new SpawnOptions(ProgramArguments.Empty,
                             System.Environment.CurrentDirectory,
                             ImmutableArray.CreateRange(
                                 from DictionaryEntry e in System.Environment.GetEnvironmentVariables()
                                 select KeyValuePair.Create((string)e.Key, (string)e.Value)),
                             input: null,
                             exitCodeErrorFunction: null,
                             psi => new Process(new SysProcess { StartInfo = psi }),
                             _ => null);

        SpawnOptions(ProgramArguments arguments, string workingDirectory,
                     ImmutableArray<KeyValuePair<string, string>> environment,
                     IObservable<OutputOrErrorLine>? input,
                     Func<ExitCodeErrorArgs, Exception?>? exitCodeErrorFunction,
                     Func<ProcessStartInfo, IProcess> processFactory,
                     Func<Exception, Exception?> killErrorFunction)
        {
            Arguments = arguments;
            WorkingDirectory = workingDirectory;
            Environment = environment;
            Input = input;
            ExitCodeErrorFunction = exitCodeErrorFunction;
            ProcessFactory = processFactory;
            KillErrorFunction = killErrorFunction;
        }

        public SpawnOptions(SpawnOptions other) :
            this(other.Arguments, other.WorkingDirectory, other.Environment,
                 other.Input,
                 other.ExitCodeErrorFunction,
                 other.ProcessFactory, other.KillErrorFunction) {}

        public ProgramArguments Arguments { get; private set; }
        public string WorkingDirectory { get; private set; }
        public ImmutableArray<KeyValuePair<string, string>> Environment { get; private set; }
        public IObservable<OutputOrErrorLine>? Input { get; private set; }
        public Func<ExitCodeErrorArgs, Exception?>? ExitCodeErrorFunction { get; private set; }
        internal Func<ProcessStartInfo, IProcess> ProcessFactory { get; private set; }
        internal Func<Exception, Exception?> KillErrorFunction { get; private set; }

        public SpawnOptions WithEnvironment(ImmutableArray<KeyValuePair<string, string>> value) =>
            Environment.IsEmpty && value.IsEmpty ? this : new SpawnOptions(this) { Environment = value };

        public SpawnOptions WithWorkingDirectory(string value)
            => value is null ? throw new ArgumentNullException(nameof(value))
             : value == WorkingDirectory ? this
             : new SpawnOptions(this) { WorkingDirectory = value };

        public SpawnOptions WithArguments(ProgramArguments value)
            => value is null ? throw new ArgumentNullException(nameof(value))
             : value == Arguments || value.Count == 0 && Arguments.Count == 0 ? this
             : new SpawnOptions(this) { Arguments = value };

        public SpawnOptions WithInput(IObservable<OutputOrErrorLine>? value)
            => value == Input ? this
             : new SpawnOptions(this) { Input = value };

        public SpawnOptions WithExitCodeErrorFunction(Func<ExitCodeErrorArgs, Exception?>? value)
            => value == ExitCodeErrorFunction ? this
             : new SpawnOptions(this) { ExitCodeErrorFunction = value };

        internal SpawnOptions WithProcessFactory(Func<ProcessStartInfo, IProcess> value)
            => value is null ? throw new ArgumentNullException(nameof(value))
             : value == ProcessFactory ? this
             : new SpawnOptions(this) { ProcessFactory = value };

        internal SpawnOptions WithKillErrorFunction(Func<Exception, Exception?> value)
            => value is null ? throw new ArgumentNullException(nameof(value))
             : value == KillErrorFunction ? this
             : new SpawnOptions(this) { KillErrorFunction = value };
    }

    public static class SpawnOptionsExtensions
    {
        public static SpawnOptions AddArgument(this SpawnOptions options, string value)
            => options is null
             ? throw new ArgumentNullException(nameof(options))
             : options.WithArguments(ProgramArguments.From(options.Arguments.Append(value)));

        public static SpawnOptions AddArgument(this SpawnOptions options, params string[] values) =>
            options.AddArguments(values);

        public static SpawnOptions AddArguments(this SpawnOptions options, IEnumerable<string> values)
            => options is null
             ? throw new ArgumentNullException(nameof(options))
             : options.WithArguments(ProgramArguments.From(options.Arguments.Concat(values)));

        public static SpawnOptions ClearArguments(this SpawnOptions options)
            => options is null
             ? throw new ArgumentNullException(nameof(options))
             : options.WithArguments(ProgramArguments.Empty);

        public static SpawnOptions SetCommandLine(this SpawnOptions options, string value)
            => options is null
             ? throw new ArgumentNullException(nameof(options))
             : options.WithArguments(ProgramArguments.Parse(value));

        public static SpawnOptions AddEnvironment(this SpawnOptions options, string name, string value)
        {
            if (options is null) throw new ArgumentNullException(nameof(options));
            if (name is null) throw new ArgumentNullException(nameof(name));
            if (name.Length == 0) throw new ArgumentException(null, nameof(name));
            if (value is null) throw new ArgumentNullException(nameof(value));

            return options.WithEnvironment(options.Environment.Add(KeyValuePair.Create(name, value)));
        }

        public static SpawnOptions ClearEnvironment(this SpawnOptions options)
            => options is null ? throw new ArgumentNullException(nameof(options))
             : options.WithEnvironment(ImmutableArray<KeyValuePair<string, string>>.Empty);

        public static SpawnOptions SetEnvironment(this SpawnOptions options, string name, string value)
        {
            if (options is null) throw new ArgumentNullException(nameof(options));
            var updateOptions = options.UnsetEnvironment(name);
            return value is null ? updateOptions : updateOptions.AddEnvironment(name, value);
        }

        public static SpawnOptions UnsetEnvironment(this SpawnOptions options, string name)
        {
            if (options is null) throw new ArgumentNullException(nameof(options));
            if (name is null) throw new ArgumentNullException(nameof(name));
            if (name.Length == 0) throw new ArgumentException(null, nameof(name));

            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var comparison = isWindows ? StringComparison.OrdinalIgnoreCase
                                       : StringComparison.Ordinal;

            var found = false;
            foreach (var e in options.Environment)
            {
                if (found = string.Equals(e.Key, name, comparison))
                    break;
            }

            return found
                 ? options.WithEnvironment(ImmutableArray.CreateRange(
                       from e in options.Environment
                       where !string.Equals(e.Key, name, comparison)
                       select e))
                 : options;
        }

        public static SpawnOptions SuppressNonZeroExitCodeError(this SpawnOptions options)
            => options is null
             ? throw new ArgumentNullException(nameof(options))
             : options.WithExitCodeErrorFunction(_ => null);

        public static void Update(this SpawnOptions options, ProcessStartInfo startInfo)
        {
            if (options is null) throw new ArgumentNullException(nameof(options));
            if (startInfo is null) throw new ArgumentNullException(nameof(startInfo));

            startInfo.WorkingDirectory = options.WorkingDirectory;

            var environment = startInfo.Environment;
            environment.Clear();
            foreach (var e in options.Environment)
                environment.Add(e.Key, e.Value);

#if PROCESS_ARG_LIST
            var args = startInfo.ArgumentList;
            args.Clear();
            foreach (var arg in options.Arguments)
                args.Add(arg);
#else
            startInfo.Arguments = options.Arguments.ToString();
#endif
        }
    }
}
