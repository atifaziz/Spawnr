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
    using System.Diagnostics;

    public enum StandardOutputKind { Output, Error }

    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public struct OutputLine
    {
        public static OutputLine Output(string line) =>
            new OutputLine(StandardOutputKind.Output, line);

        public static OutputLine Error(string line) =>
            new OutputLine(StandardOutputKind.Error, line);

        public StandardOutputKind Kind { get; }
        public string Line { get; }

        public OutputLine(StandardOutputKind kind, string line)
        {
            Kind = kind;
            Line = line ?? throw new System.ArgumentNullException(nameof(line));
        }

        public override string ToString() => Line ?? string.Empty;

        public void Deconstruct(out StandardOutputKind kind, out string line) =>
            (kind, line) = (Kind, Line);

        string GetDebuggerDisplay() => $"{Kind}: {Line}";
    }
}