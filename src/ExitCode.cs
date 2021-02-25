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
    using System.Globalization;

    [Serializable]
    public readonly struct ExitCode : IEquatable<ExitCode>
    {
        readonly int _value;

        public ExitCode(int value) => _value = value;

        public bool IsSuccess => _value == 0;
        public bool IsError => !IsSuccess;

        public bool Equals(ExitCode other) => _value == other._value;
        public override bool Equals(object? obj) => obj is ExitCode other && Equals(other);
        public override int GetHashCode() => _value;

        public override string ToString() => _value.ToString(NumberFormatInfo.InvariantInfo);

        public static bool operator ==(ExitCode left, ExitCode right) => left.Equals(right);
        public static bool operator !=(ExitCode left, ExitCode right) => !left.Equals(right);

        public static implicit operator int(ExitCode value) => value._value;
        public static implicit operator ExitCode(int value) => new(value);

        public static bool operator true(ExitCode value) => value.IsSuccess;
        public static bool operator false(ExitCode value) => value.IsError;

        public void ThrowIfError()
        {
            if (this)
                return;
            throw new ExternalProcessException(this);
        }
    }
}
