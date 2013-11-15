//
// LuaValueExtensions.cs
//
// Author:
//       Chris Howie <me@chrishowie.com>
//
// Copyright (c) 2013 Chris Howie
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;

namespace Eluant
{
    public static class LuaValueExtensions
    {
        public static bool IsNil(this LuaValue self)
        {
            return self == null || self == LuaNil.Instance;
        }

        public static IEnumerable<LuaValue> EnumerateArray(this LuaTable self)
        {
            if (self == null) { throw new ArgumentNullException("self"); }

            return CreateEnumerateArrayEnumerable(self);
        }

        private static IEnumerable<LuaValue> CreateEnumerateArrayEnumerable(LuaTable self)
        {
            // By convention, the 'n' field refers to the array length, if present.
            using (var n = self["n"]) {
                var num = n as LuaNumber;
                if (num != null) {
                    var length = (int)num.Value;

                    for (int i = 1; i <= length; ++i) {
                        yield return self[i];
                    }

                    yield break;
                }
            }

            // If no 'n' then stop at the first nil element.
            for (int i = 1; ; ++i) {
                var value = self[i];
                if (value.IsNil()) {
                    yield break;
                }

                yield return value;
            }
        }

        public static void Dispose(this KeyValuePair<LuaValue, LuaValue> self)
        {
            if (self.Key != null) { self.Key.Dispose(); }
            if (self.Value != null) { self.Value.Dispose(); }
        }
    }
}

