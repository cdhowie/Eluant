using System;

namespace Eluant
{
    public sealed class LuaOpaqueClrObject : LuaValueType
    {
        public object ClrObject { get; private set; }

        public LuaOpaqueClrObject(object obj)
        {
            ClrObject = obj;
        }

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
            return string.Format("[LuaOpaqueClrObject: ClrObject={0}]", ClrObject);
        }

        internal override void Push(LuaRuntime runtime)
        {
            runtime.PushOpaqueClrObject(ClrObject);
        }
    }
}

