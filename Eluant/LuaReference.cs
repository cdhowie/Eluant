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
            return Reference;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LuaReference);
        }

        public bool Equals(LuaReference r)
        {
            if (r == null) { return false; }

            return Reference == r.Reference;
        }
    }
}

