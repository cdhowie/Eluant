using System;

namespace Eluant
{
    public class LuaLightUserdata : LuaReference
    {
        internal LuaLightUserdata(LuaRuntime runtime, int reference) : base(runtime, reference) { }

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
            return "[LuaLightUserdata]";
        }

        new public LuaWeakReference<LuaLightUserdata> CreateWeakReference()
        {
            CheckDisposed();

            return Runtime.CreateWeakReference(this);
        }
    }
}

