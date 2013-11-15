//
// LuaWeakReference.cs
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
    public sealed class LuaWeakReference<T> : LuaValue
        where T : LuaReference
    {
        internal LuaTable WeakTable { get; private set; }

        internal LuaWeakReference(LuaTable weakTable)
        {
            if (weakTable == null) { throw new ArgumentNullException("weakTable"); }

            WeakTable = weakTable;
        }

        private void CheckDisposed()
        {
            if (WeakTable == null) {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        // No finalizer, because we are only disposing WeakTable.  Its own finalizer will take care of the reference if
        // necessary.  This override just serves to allow the table to be explicitly disposed.
        public override void Dispose()
        {
            if (WeakTable != null) {
                WeakTable.Dispose();
                WeakTable = null;
            }
        }

        public T CreateReferenceToTarget()
        {
            CheckDisposed();

            return WeakTable.Runtime.GetWeakReference(this);
        }

        protected override LuaValue CopyReferenceImpl()
        {
            return new LuaWeakReference<T>((LuaTable)WeakTable.CopyReference());
        }

        public override bool ToBoolean()
        {
            using (var target = CopyReferenceImpl()) {
                return target.ToBoolean();
            }
        }

        public override double? ToNumber()
        {
            using (var target = CopyReferenceImpl()) {
                return target.ToNumber();
            }
        }

        public override string ToString()
        {
            return "[LuaWeakReference]";
        }

        internal override void Push(LuaRuntime runtime)
        {
            CheckDisposed();

            WeakTable.Runtime.PushWeakReference(this);
        }

        public override bool Equals(LuaValue other)
        {
            // What should we consider -- the target object?  What if the target object is dead?
            //
            // We could compare the weak tables, but two different weak references can be independently created and
            // would have different tables.
            //
            // The only thing feasible to do is implement CLR object reference equality.
            return object.ReferenceEquals(other, this);
        }
    }
}

