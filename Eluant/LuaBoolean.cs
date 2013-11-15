//
// LuaBoolean.cs
//
// Author:
//       Chris Howie <me@chrishowie.com>
//
// Copyright (c) 2013 Chris Howie
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

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

        public override bool Equals(LuaValue other)
        {
            return Equals(other as LuaBoolean);
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

        internal override object ToClrType(Type type)
        {
            if (type == typeof(bool)) {
                return Value;
            }

            return base.ToClrType(type);
        }

        public static implicit operator LuaBoolean(bool v)
        {
            return Get(v);
        }

        public static explicit operator bool?(LuaBoolean v)
        {
            return v == null ? (bool?)null : v.Value;
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

