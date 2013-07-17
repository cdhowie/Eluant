using System;

namespace Eluant
{
    public static class LuaValueExtensions
    {
        public static bool IsNil(this LuaValue self)
        {
            return self == null || self == LuaNil.Instance;
        }
    }
}

