using System;

namespace Eluant
{
    public sealed class LuaString : LuaValueType,
        IEquatable<LuaString>, IEquatable<string>,
        IComparable, IComparable<LuaString>, IComparable<string>
    {
        public string Value { get; private set; }

        public LuaString(string value)
        {
            if (value == null) { throw new ArgumentNullException("value"); }

            Value = value;
        }

        public override bool ToBoolean()
        {
            return true;
        }

        public override double? ToNumber()
        {
            double number;
            if (double.TryParse(Value, out number)) {
                return number;
            }

            return null;
        }

        public override string ToString()
        {
            return Value;
        }

        internal override void Push(LuaRuntime runtime)
        {
            LuaApi.lua_pushlstring(runtime.LuaState, Value, new UIntPtr((ulong)Value.Length));
        }

        public static implicit operator LuaString(string v)
        {
            return v == null ? null : new LuaString(v);
        }

        public static implicit operator string(LuaString s)
        {
            return object.ReferenceEquals(s, null) ? null : s.Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LuaString);
        }

        public bool Equals(LuaString obj)
        {
            if (object.ReferenceEquals(obj, this)) { return true; }
            if (object.ReferenceEquals(obj, null)) { return false; }

            return obj.Value == Value;
        }

        public bool Equals(string obj)
        {
            if (obj == null) { return false; }

            return obj == Value;
        }

        // No (LuaString, LuaString) overload.  With implicit conversion to string, that creates ambiguity.

        public static bool operator==(LuaString a, string b)
        {
            return (string)a == b;
        }

        public static bool operator!=(LuaString a, string b)
        {
            return !(a == b);
        }

//        public static bool operator==(string a, LuaString b)
//        {
//            return a == (string)b;
//        }
//
//        public static bool operator!=(string a, LuaString b)
//        {
//            return !(a == b);
//        }

        public int CompareTo(LuaString s)
        {
            return CompareTo(object.ReferenceEquals(s, null) ? null : s.Value);
        }

        public int CompareTo(string s)
        {
            return Value.CompareTo(s);
        }

        public int CompareTo(object o)
        {
            var luaString = o as LuaString;
            if (!object.ReferenceEquals(luaString, null)) { return CompareTo(luaString); }

            var str = o as string;
            if (str != null) { return CompareTo(str); }

            throw new ArgumentException("Must be a LuaString or a String.", "o");
        }
    }
}

