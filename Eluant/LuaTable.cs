using System;

namespace Eluant
{
    public sealed class LuaTable : LuaReference
    {
        internal LuaTable(LuaRuntime runtime, int reference) : base(runtime, reference) { }

        public override bool ToBoolean()
        {
            return true;
        }

        public override double? ToNumber()
        {
            return null;
        }

        public override string ToString()
        {
            return "[LuaTable]";
        }

        public override LuaValue this[LuaValue key] {
            get {
                CheckDisposed();

                var top = LuaApi.lua_gettop(Runtime.LuaState);

                Runtime.Push(this);
                Runtime.Push(key);
                LuaApi.lua_gettable(Runtime.LuaState, -2);

                LuaValue val;

                try {
                    val = Runtime.Wrap(-1);
                } finally {
                    LuaApi.lua_settop(Runtime.LuaState, top);
                }

                return val;
            }
            set {
                CheckDisposed();

                var top = LuaApi.lua_gettop(Runtime.LuaState);

                Runtime.Push(this);
                Runtime.Push(key);
                Runtime.Push(value);

                LuaApi.lua_settable(Runtime.LuaState, -3);

                LuaApi.lua_settop(Runtime.LuaState, top);
            }
        }

        new public LuaWeakReference<LuaTable> CreateWeakReference()
        {
            CheckDisposed();

            return Runtime.CreateWeakReference(this);
        }
    }
}

