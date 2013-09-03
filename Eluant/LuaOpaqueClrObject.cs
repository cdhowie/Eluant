using System;

namespace Eluant
{
    public sealed class LuaOpaqueClrObject : LuaClrObjectValue, IEquatable<LuaOpaqueClrObject>
    {
        public LuaOpaqueClrObject(object obj) : base(obj) { }

        internal override void Push(LuaRuntime runtime)
        {
            runtime.PushOpaqueClrObject(this);
        }

        public override bool Equals(LuaValue other)
        {
            return Equals(other as LuaOpaqueClrObject);
        }

        public bool Equals(LuaOpaqueClrObject obj)
        {
            return obj != null && obj.ClrObject == ClrObject;
        }

        internal override object BackingCustomObject
        {
            get { return null; }
        }
    }
}

