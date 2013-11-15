//
// LuaNil.cs
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

