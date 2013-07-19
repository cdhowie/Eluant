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

