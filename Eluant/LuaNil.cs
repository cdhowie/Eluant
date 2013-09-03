using System;

namespace Eluant
{
    public sealed class LuaNil : LuaValueType
    {
        private static readonly LuaNil instance = new LuaNil();

        public static LuaNil Instance
        {
            get { return instance; }
        }

        private LuaNil() { }

        internal override void Push(LuaRuntime runtime)
        {
            LuaApi.lua_pushnil(runtime.LuaState);
        }

        public override bool ToBoolean()
        {
            return false;
        }

        public override double? ToNumber()
        {
            return null;
        }

        public override string ToString()
        {
            return "nil";
        }

        public override bool Equals(LuaValue other)
        {
            return other == null || other == this;
        }

        internal override object ToClrType(Type type)
        {
            if (type == null) { throw new ArgumentNullException("type"); }

            if (!type.IsValueType || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))) {
                return null;
            }

            return base.ToClrType(type);
        }
    }
}

