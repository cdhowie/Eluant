using System;

namespace Eluant
{
    public class LuaCustomClrObject : LuaClrObjectValue, IEquatable<LuaCustomClrObject>
    {
        public LuaCustomClrObject(object obj) : base(obj) { }

        internal override void Push(LuaRuntime runtime)
        {
            runtime.PushCustomClrObject(this);
        }

        public override bool Equals(LuaValue other)
        {
            return Equals(other as LuaCustomClrObject);
        }

        public bool Equals(LuaCustomClrObject other)
        {
            return other != null && other.ClrObject == ClrObject;
        }

        internal override object BackingCustomObject
        {
            get { return ClrObject; }
        }
    }
}

