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
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using SysProcess = System.Diagnostics.Process;

    interface IProcess : IDisposable
    {
        int Id { get; }
        int ExitCode { get; }

        bool EnableRaisingEvents { get; set; }

        event EventHandler Exited;
        event DataReceivedEventHandler ErrorDataReceived;
        event DataReceivedEventHandler OutputDataReceived;

        void Start();
        bool TryKill([MaybeNullWhen(true)] out Exception exception);
        void BeginErrorReadLine();
        void BeginOutputReadLine();
    }

    sealed class Process : IProcess
    {
        SysProcess _process;

        public Process(SysProcess process) =>
            _process = process;

        public int Id { get; }
        public int ExitCode { get; }

        public bool EnableRaisingEvents { get; set; }

        public void Start() => _ = _process.Start();
        public bool TryKill([MaybeNullWhen(true)] out Exception exception) =>
            _process.TryKill(out exception);

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

        public void Dispose() => _process.Dispose();
    }
}
