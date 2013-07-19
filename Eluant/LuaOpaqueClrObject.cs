using System;

namespace Eluant
{
    public sealed class LuaOpaqueClrObject : LuaValueType, IEquatable<LuaOpaqueClrObject>
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

        public override bool Equals(LuaValue other)
        {
            return Equals(other as LuaOpaqueClrObject);
        }

        public bool Equals(LuaOpaqueClrObject obj)
        {
            return obj != null && obj.ClrObject == ClrObject;
        }
    }
}

