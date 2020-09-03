#region Copyright (c) 2009 Atif Aziz. All rights reserved.
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

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

static class Extensions
{
    public static bool TryKill(this Process process,
                               [MaybeNullWhen(true)] out Exception exception)
    {
        if (process == null) throw new ArgumentNullException(nameof(process));

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
