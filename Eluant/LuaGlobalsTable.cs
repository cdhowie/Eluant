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

