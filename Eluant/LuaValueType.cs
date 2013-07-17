using System;

namespace Eluant
{
    public abstract class LuaValueType : LuaValue
    {
        internal LuaValueType() { }

        public override void Dispose()
        {
            // No-op for value types.
        }

        protected override LuaValue CopyReferenceImpl()
        {
            // No need to copy anything since value types don't hold a runtime-specific reference.
            return this;
        }
    }
}

