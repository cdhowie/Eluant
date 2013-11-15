//
// LuaGlobalsTable.cs
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
    public class LuaGlobalsTable : LuaTable
    {
        internal LuaGlobalsTable(LuaRuntime runtime) : base(runtime, 0)
        {
            // Finalization not required for this special reference.
            GC.SuppressFinalize(this);
        }

        internal override void Push(LuaRuntime runtime)
        {
            LuaApi.lua_pushvalue(runtime.LuaState, LuaApi.LUA_GLOBALSINDEX);
        }

        protected override void Dispose(bool disposing)
        {
            // Do nothing.
        }

        protected override LuaValue CopyReferenceImpl()
        {
            // This is a singleton per runtime, no need to copy anything.
            return this;
        }

        public override bool Equals(LuaReference r)
        {
            // This singleton table is only ever equal to itself.
            return object.ReferenceEquals(r, this);
        }
    }
}

