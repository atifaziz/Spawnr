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

    public enum StandardOutputKind { Output, Error }

    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public struct OutputLine
    {
        public static OutputLine Output(string line) =>
            new OutputLine(StandardOutputKind.Output, line);

        public static OutputLine Error(string line) =>
            new OutputLine(StandardOutputKind.Error, line);

        public bool IsOutput => Kind == StandardOutputKind.Output;
        public bool IsError  => Kind == StandardOutputKind.Error;

        public StandardOutputKind Kind { get; }
        public string Value { get; }

        public OutputLine(StandardOutputKind kind, string value)
        {
            Kind = kind;
            Value = value ?? throw new System.ArgumentNullException(nameof(value));
        }

        public override string ToString() => Value ?? string.Empty;

        public void Deconstruct(out StandardOutputKind kind, out string line) =>
            (kind, line) = (Kind, Value);

        public T Match<T>(Func<string, T> output, Func<string, T> error)
            => output is null ? throw new ArgumentNullException(nameof(output))
             : error is null ? throw new ArgumentNullException(nameof(error))
             : IsOutput ? output(Value) : error(Value);

        string GetDebuggerDisplay() => $"{Kind}: {Value}";
    }
}
