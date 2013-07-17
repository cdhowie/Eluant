using System;

namespace Eluant
{
    public sealed class LuaThread : LuaReference
    {
        internal LuaThread(LuaRuntime runtime, int reference) : base(runtime, reference) { }

        public override bool ToBoolean()
        {
            return true;
        }

        public override double? ToNumber()
        {
            return null;
        }

        public override string ToString()
        {
            return "[LuaThread]";
        }

        new public LuaWeakReference<LuaThread> CreateWeakReference()
        {
            CheckDisposed();

            return Runtime.CreateWeakReference(this);
        }
    }
}

