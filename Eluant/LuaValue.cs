//
// LuaValue.cs
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

namespace Eluant
{
    public abstract class LuaValue : IDisposable, IEquatable<LuaValue>
    {
        internal LuaValue() { }

        // Don't implement the full disposable pattern, but require sub-types to implement it.  For example, this allows
        // LuaValueType types to completely omit the finalizer with a no-op Dispose() implementation.
        public abstract void Dispose();

        public LuaValue CopyReference()
        {
            return CopyReferenceImpl();
        }

        protected abstract LuaValue CopyReferenceImpl();

        public virtual double? ToNumber()
        {
            throw new NotSupportedException("Type cannot be converted to a number.");
        }

        // All types should be able to implement this.
        public abstract bool ToBoolean();

        public abstract override string ToString();

        internal abstract void Push(LuaRuntime runtime);

        public static implicit operator LuaValue(bool v)
        {
            return (LuaBoolean)v;
        }

        public static implicit operator LuaValue(string v)
        {
            return (LuaString)v;
        }

        public static implicit operator LuaValue(double? n)
        {
            return (LuaNumber)n;
        }

        public abstract bool Equals(LuaValue other);

        internal virtual object ToClrType(Type type)
        {
            if (type == null) { throw new ArgumentNullException("type"); }

            if (type.IsAssignableFrom(GetType())) {
                return this;
            }

            throw new ArgumentException("Cannot convert to the specified type.");
        }
    }
}

