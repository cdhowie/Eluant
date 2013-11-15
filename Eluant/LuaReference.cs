//
// LuaReference.cs
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
    public abstract class LuaReference : LuaValue, IEquatable<LuaReference>
    {
        public LuaRuntime Runtime { get; private set; }

        internal int Reference { get; private set; }

        internal LuaReference(LuaRuntime runtime, int reference)
        {
            if (runtime == null) { throw new ArgumentNullException("runtime"); }

            Runtime = runtime;
            Reference = reference;
        }

        ~LuaReference()
        {
            Dispose(false);
        }

        public sealed override void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Runtime != null) {
                Runtime.DisposeReference(Reference, disposing);
                Runtime = null;
            }
        }

        protected internal void CheckDisposed()
        {
            if (Runtime == null) {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        internal override void Push(LuaRuntime runtime)
        {
            CheckDisposed();

            AssertRuntimeIs(runtime);

            runtime.PushReference(Reference);
        }

        protected override LuaValue CopyReferenceImpl()
        {
            CheckDisposed();

            // We need to take a new reference, so we will let Runtime.Wrap() build the copy.
            Runtime.Push(this);
            var copy = Runtime.Wrap(-1);
            LuaApi.lua_pop(Runtime.LuaState, 1);

            return copy;
        }

        protected internal void AssertRuntimeIs(LuaRuntime runtime)
        {
            if (runtime != Runtime) {
                throw new InvalidOperationException("Attempt to use a LuaRuntimeBoundValue with the wrong runtime.");
            }
        }

        public LuaWeakReference<LuaReference> CreateWeakReference()
        {
            CheckDisposed();

            return Runtime.CreateWeakReference(this);
        }

        public override int GetHashCode()
        {
            // If the reference has been disposed then this object is not equal to any other reference object.  To make
            // sure that GetHashCode()'s contract is upheld in the face of possible reference ID reuse, we have to throw
            // an exception if the object was disposed.
            //
            // (Protip: Don't dispose LuaReference objects that are used as keys in dictionaries!)
            CheckDisposed();

            return Reference;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LuaReference);
        }

        // References are easy -- if the reference ID is the same, the objects are equal.
        public virtual bool Equals(LuaReference r)
        {
            if (r == this) { return true; }
            if (r == null) { return false; }

            // But if the reference has been disposed, the reference ID could be reused!  So a disposed reference is
            // never equal to anything but itself (which we already checked).
            if (Runtime == null || r.Runtime == null) { return false; }

            return Reference == r.Reference;
        }

        public override bool Equals(LuaValue other)
        {
            return Equals(other as LuaReference);
        }
    }
}

