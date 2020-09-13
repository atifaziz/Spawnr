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

    public static class LineExtensions
    {
        public static IObservable<OutputLine>
            AsOutput(this IObservable<string> source) =>
            source.AsOutputKind(StandardOutputKind.Output);

        public static IObservable<OutputLine>
            AsError(this IObservable<string> source) =>
            source.AsOutputKind(StandardOutputKind.Error);

        static IObservable<OutputLine>
            AsOutputKind(this IObservable<string> source, StandardOutputKind kind) =>
            from line in source ?? throw new ArgumentNullException(nameof(source))
            select new OutputLine(kind, line);

        public static IObservable<string>
            FilterOutput(this IObservable<OutputLine> source) =>
            source.Filter(StandardOutputKind.Error);

        public static IObservable<string>
            FilterError(this IObservable<OutputLine> source) =>
            source.Filter(StandardOutputKind.Error);

        static IObservable<string>
            Filter(this IObservable<OutputLine> source, StandardOutputKind kind) =>
            from line in source ?? throw new ArgumentNullException(nameof(source))
            where line.Kind == kind
            select line.Value;
    }
}
