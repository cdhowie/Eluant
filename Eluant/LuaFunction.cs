using System;
using System.Collections.Generic;

namespace Eluant
{
    public class LuaFunction : LuaReference
    {
        internal LuaFunction(LuaRuntime runtime, int reference) : base(runtime, reference) { }

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
            return "[LuaFunction]";
        }

        public LuaVararg Call(IList<LuaValue> args)
        {
            return Runtime.Call(this, args);
        }

        public LuaVararg Call(params LuaValue[] args)
        {
            return Call((IList<LuaValue>)args);
        }

        new public LuaWeakReference<LuaFunction> CreateWeakReference()
        {
            CheckDisposed();

            return Runtime.CreateWeakReference(this);
        }
    }
}

