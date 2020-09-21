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

    public enum OutputOrErrorKind { Output, Error }

    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public struct OutputOrErrorLine
    {
        public static OutputOrErrorLine Output(string value) =>
            new OutputOrErrorLine(OutputOrErrorKind.Output, value);

        public static OutputOrErrorLine Error(string value) =>
            new OutputOrErrorLine(OutputOrErrorKind.Error, value);

        public bool IsOutput => Kind == OutputOrErrorKind.Output;
        public bool IsError  => Kind == OutputOrErrorKind.Error;

        public OutputOrErrorKind Kind { get; }
        public string Value { get; }

        public OutputOrErrorLine(OutputOrErrorKind kind, string value)
        {
            Kind = kind;
            Value = value ?? throw new System.ArgumentNullException(nameof(value));
        }

        public override string ToString() => Value ?? string.Empty;

        public void Deconstruct(out OutputOrErrorKind kind, out string value) =>
            (kind, value) = (Kind, Value);

        public T Match<T>(Func<string, T> output, Func<string, T> error)
            => output is null ? throw new ArgumentNullException(nameof(output))
             : error is null ? throw new ArgumentNullException(nameof(error))
             : IsOutput ? output(Value) : error(Value);

        string GetDebuggerDisplay() => $"{Kind}: {Value}";
    }
}
