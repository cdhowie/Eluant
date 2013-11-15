//
// IClrObject.cs
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
using Eluant.ObjectBinding;

namespace Eluant
{
    public interface IClrObject
    {
        object ClrObject { get; }
    }

    public static class ClrObject
    {
        public static bool TryGetClrObject(this IClrObject self, out object obj)
        {
            if (self != null) {
                obj = self.ClrObject;
                return true;
            }

            obj = null;
            return false;
        }

        public static object GetClrObject(this IClrObject self)
        {
            if (self == null) { throw new ArgumentNullException("self"); }

            return self.ClrObject;
        }

        public static bool TryGetClrObject(this LuaValue self, out object obj)
        {
            var clrObject = self as IClrObject;
            if (clrObject != null) {
                obj = GetClrObject(clrObject);
                return true;
            }

            obj = null;
            return false;
        }

        public static object GetClrObject(this LuaValue self)
        {
            object obj;
            if (!TryGetClrObject(self, out obj)) {
                throw new ArgumentException("Does not represent a CLR object.", "self");
            }

            return obj;
        }
    }
}

