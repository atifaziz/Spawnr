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
    using System.Reactive.Linq;

    static class LineExtensions
    {
        public static IObservable<OutputOrErrorLine>
            AsOutput(this IObservable<string> source) =>
            source.AsOutputKind(OutputOrErrorKind.Output);

        public static IObservable<OutputOrErrorLine>
            AsError(this IObservable<string> source) =>
            source.AsOutputKind(OutputOrErrorKind.Error);

        static IObservable<OutputOrErrorLine>
            AsOutputKind(this IObservable<string> source, OutputOrErrorKind kind) =>
            from line in source ?? throw new ArgumentNullException(nameof(source))
            select new OutputOrErrorLine(kind, line);

        public static IObservable<string>
            FilterOutput(this IObservable<OutputOrErrorLine> source) =>
            source.Filter(OutputOrErrorKind.Error);

        public static IObservable<string>
            FilterError(this IObservable<OutputOrErrorLine> source) =>
            source.Filter(OutputOrErrorKind.Error);

        static IObservable<string>
            Filter(this IObservable<OutputOrErrorLine> source, OutputOrErrorKind kind) =>
            from line in source ?? throw new ArgumentNullException(nameof(source))
            where line.Kind == kind
            select line.Value;
    }
}
