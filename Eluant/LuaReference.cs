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

