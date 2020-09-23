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

    public interface IOutputOrErrorLine
    {
        OutputOrErrorKind Kind { get; }
        string Value { get; }
    }

    [DebuggerDisplay("{" + nameof(Value) + "}")]
    public readonly struct OutputLine : IOutputOrErrorLine
    {
        public OutputLine(string value) =>
            Value = value ?? throw new ArgumentNullException(nameof(value));

        public OutputOrErrorKind Kind => OutputOrErrorKind.Output;
        public string Value { get; }

        public override string ToString() => Value ?? string.Empty;

        public static implicit operator string(OutputLine value) => value.Value;

        string GetDebuggerDisplay() => $"{Kind}: {Value}";
    }

    [DebuggerDisplay("{" + nameof(Value) + "}")]
    public readonly struct ErrorLine : IOutputOrErrorLine
    {
        public ErrorLine(string value) =>
            Value = value ?? throw new ArgumentNullException(nameof(value));

        public OutputOrErrorKind Kind => OutputOrErrorKind.Output;
        public string Value { get; }

        public override string ToString() => Value ?? string.Empty;

        public static implicit operator string(ErrorLine value) => value.Value;
    }

    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public readonly struct OutputOrErrorLine : IOutputOrErrorLine
    {
        public static OutputOrErrorLine Output(string value) =>
            new OutputOrErrorLine(OutputOrErrorKind.Output, value);

        public static OutputOrErrorLine Error(string value) =>
            new OutputOrErrorLine(OutputOrErrorKind.Error, value);

        public OutputOrErrorLine(OutputOrErrorKind kind, string value)
        {
            Kind = kind;
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public OutputOrErrorKind Kind { get; }
        public string Value { get; }

        public override string ToString() => Value ?? string.Empty;

        string GetDebuggerDisplay() => $"{Kind}: {Value}";
    }

    public static class OutputOrErrorLineExtensions
    {
        public static bool IsOutput<TLine>(this TLine line)
            where TLine : IOutputOrErrorLine =>
            line.Kind == OutputOrErrorKind.Output;

        public static bool IsError<TLine>(this TLine line)
            where TLine : IOutputOrErrorLine =>
            line.Kind == OutputOrErrorKind.Error;

        public static void Deconstruct<TLine>(this TLine line,
                                              out OutputOrErrorKind kind,
                                              out string value)
            where TLine : IOutputOrErrorLine =>
            (kind, value) = line is null
                          ? throw new ArgumentNullException(nameof(line))
                          : (line.Kind, line.Value);

        public static OutputOrErrorLine ToOutputOrErrorLine<TLine>(this TLine line)
            where TLine : IOutputOrErrorLine =>
            line switch { null => throw new ArgumentNullException(nameof(line)),
                          (OutputOrErrorKind.Output, var s) => OutputOrErrorLine.Output(s),
                          (_, var s) => OutputOrErrorLine.Error(s) };

        public static T Match<T>(this IOutputOrErrorLine line,
                                 Func<string, T> output, Func<string, T> error)
            => line is null ? throw new ArgumentNullException(nameof(line))
             : output is null ? throw new ArgumentNullException(nameof(output))
             : error is null ? throw new ArgumentNullException(nameof(error))
             : line.Kind == OutputOrErrorKind.Output ? output(line.Value) : error(line.Value);
    }
}
