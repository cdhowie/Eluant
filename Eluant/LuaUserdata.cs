using System;

namespace Eluant
{
    public class LuaUserdata : LuaReference
    {
        internal LuaUserdata(LuaRuntime runtime, int reference) : base(runtime, reference) { }

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
            return "[LuaUserdata]";
        }

        new public LuaWeakReference<LuaUserdata> CreateWeakReference()
        {
            CheckDisposed();

            return Runtime.CreateWeakReference(this);
        }
    }
}

