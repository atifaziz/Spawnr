#region Copyright (c) 2020 Atif Aziz. All rights reserved.
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
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using SysProcess = System.Diagnostics.Process;

    interface IProcess : IDisposable
    {
        int Id { get; }
        int ExitCode { get; }

        bool EnableRaisingEvents { get; set; }

        event EventHandler Exited;
        event DataReceivedEventHandler ErrorDataReceived;
        event DataReceivedEventHandler OutputDataReceived;

        StreamWriter StandardInput { get; }

        void Start();
        void Kill();
        void BeginErrorReadLine();
        void BeginOutputReadLine();
    }

    static class ProcessExtensions
    {
        public static bool TryKill(this IProcess process, [MaybeNullWhen(true)] out Exception exception)
        {
            try
            {
                process.Kill();
                exception = null;
                return true;
            }
            catch (InvalidOperationException e)
            {
                // Occurs when:
                // - process has already exited.
                // - no process is associated with this Process object.
                exception = e;
                return false;
            }
            catch (Win32Exception e)
            {
                // Occurs when:
                // - associated process could not be terminated.
                // - process is terminating.
                // - associated process is a Win16 executable.
                exception = e;
                return false;
            }
        }
    }

    sealed class Process : IProcess
    {
        readonly SysProcess _process;

        public Process(SysProcess process) =>
            _process = process;

        public int Id => _process.Id;
        public int ExitCode => _process.ExitCode;

        public bool EnableRaisingEvents
        {
            get => _process.EnableRaisingEvents;
            set => _process.EnableRaisingEvents = value;
        }

        public void Start() => _ = _process.Start();
        public void Kill() => _process.Kill();

        public void BeginErrorReadLine() => _process.BeginErrorReadLine();
        public void BeginOutputReadLine() => _process.BeginOutputReadLine();

        public event EventHandler Exited
        {
            add    => _process.Exited += value;
            remove => _process.Exited -= value;
        }
        public event DataReceivedEventHandler ErrorDataReceived
        {
            add    => _process.ErrorDataReceived += value;
            remove => _process.ErrorDataReceived -= value;
        }
        public event DataReceivedEventHandler OutputDataReceived
        {
            add    => _process.OutputDataReceived += value;
            remove => _process.OutputDataReceived -= value;
        }

        public StreamWriter StandardInput => _process.StandardInput;

        public void Dispose() => _process.Dispose();
    }
}
