using System;

namespace Eluant
{
    public class LuaException : Exception
    {
        public LuaException(string message) : base(message) { }

        public LuaException(string message, Exception inner) : base(message, inner) { }
    }
}

