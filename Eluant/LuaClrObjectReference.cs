//
// LuaClrObjectReference.cs
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
    public sealed class LuaClrObjectReference : LuaUserdata, IClrObject
    {
        public LuaClrObjectReference(LuaRuntime runtime, int reference) : base(runtime, reference) { }

        public override bool ToBoolean()
        {
            return ClrObject != null;
        }

        public override double? ToNumber()
        {
            return null;
        }

        public override string ToString()
        {
            return string.Format("[LuaClrObjectReference ClrObject:{0}]", ClrObject);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            isClrObjectValueCached = false;
            cachedClrObjectValue = null;
        }

        private bool isClrObjectValueCached = false;
        private LuaClrObjectValue cachedClrObjectValue = null;

        public LuaClrObjectValue ClrObjectValue
        {
            get {
                CheckDisposed();

                if (!isClrObjectValueCached) {
                    Runtime.Push(this);
                    cachedClrObjectValue = Runtime.GetClrObject<LuaClrObjectValue>(-1);
                    LuaApi.lua_pop(Runtime.LuaState, 1);

                    isClrObjectValueCached = true;
                }

                return cachedClrObjectValue;
            }
        }

        public object ClrObject
        {
            get { return ClrObjectValue.ClrObject; }
        }

        new public LuaWeakReference<LuaClrObjectReference> CreateWeakReference()
        {
            CheckDisposed();

            return Runtime.CreateWeakReference(this);
        }

        internal override object ToClrType(Type type)
        {
            try {
                return ClrObjectValue.ToClrType(type);
            } catch (ArgumentException) { }

            return base.ToClrType(type);
        }
    }
}

