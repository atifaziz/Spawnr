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
    using System.Runtime.Serialization;

    [Serializable]
    public class ExternalProcessException : Exception, ISerializable
    {
        public ExternalProcessException(int exitCode) :
            this(exitCode, null) {}

        public ExternalProcessException(int exitCode, string? message) :
            this(exitCode, message, null) {}

        public ExternalProcessException(int exitCode, string? message, Exception? inner) :
            base(message ?? $"External process terminated with an exit code of {exitCode}.", inner) =>
            ExitCode = exitCode;

        protected ExternalProcessException(SerializationInfo info, StreamingContext context) :
            base(info, context) =>
            ExitCode = info.GetInt32(nameof(ExitCode));

        public int ExitCode { get; }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(ExitCode), ExitCode);
        }
    }
}
