using System;

namespace Eluant
{
    public sealed class LuaBoolean : LuaValueType, IEquatable<LuaBoolean>, IEquatable<bool>
    {
        private static readonly LuaBoolean falseBoolean = new LuaBoolean(false);
        private static readonly LuaBoolean trueBoolean = new LuaBoolean(true);

        public static LuaBoolean False
        {
            get { return falseBoolean; }
        }

        public static LuaBoolean True
        {
            get { return trueBoolean; }
        }

        public static LuaBoolean Get(bool v)
        {
            return v ? True : False;
        }

        public bool Value { get; private set; }

        private LuaBoolean(bool value)
        {
            Value = value;

            hashCode = typeof(LuaBoolean).GetHashCode() ^ (value ? 1 : 2);
        }

        private int hashCode;

        public override int GetHashCode()
        {
            return hashCode;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LuaBoolean);
        }

        public bool Equals(LuaBoolean b)
        {
            return b != null && b.Value == Value;
        }

        public bool Equals(bool b)
        {
            return b == Value;
        }

        internal override void Push(LuaRuntime runtime)
        {
            LuaApi.lua_pushboolean(runtime.LuaState, Value ? 1 : 0);
        }

        public override bool ToBoolean()
        {
            return Value;
        }

        public override double? ToNumber()
        {
            return Value ? 1 : 0;
        }

        public override string ToString()
        {
            return Value ? "true" : "false";
        }

        public static implicit operator LuaBoolean(bool v)
        {
            return Get(v);
        }

        public static bool operator==(LuaBoolean a, LuaBoolean b)
        {
            if (object.ReferenceEquals(a, b)) { return true; }
            if (object.ReferenceEquals(a, null)) { return object.ReferenceEquals(b, null); }

            return a.Equals(b);
        }

        public static bool operator!=(LuaBoolean a, LuaBoolean b)
        {
            return !(a == b);
        }

        public static bool operator==(LuaBoolean a, bool b)
        {
            if (object.ReferenceEquals(a, null)) { return false; }

            return a.Equals(b);
        }

        public static bool operator!=(LuaBoolean a, bool b)
        {
            return !(a == b);
        }

        public static bool operator==(bool a, LuaBoolean b)
        {
            return b == a;
        }

        public static bool operator!=(bool a, LuaBoolean b)
        {
            return !(b == a);
        }
    }
}

