using System;

namespace Eluant.ObjectBinding
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple=true, Inherited=true)]
    public class LuaMemberAttribute : Attribute
    {
        public string LuaKey { get; private set; }

        public LuaMemberAttribute() : this(null) { }

        public LuaMemberAttribute(string luaKey)
        {
            LuaKey = luaKey;
        }
    }
}

