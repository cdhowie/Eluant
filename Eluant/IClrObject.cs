using System;
using Eluant.ObjectBinding;

namespace Eluant
{
    public interface IClrObject
    {
        object ClrObject { get; }
    }

    public static class ClrObject
    {
        public static bool TryGetClrObject(this IClrObject self, out object obj)
        {
            if (self != null) {
                obj = self.ClrObject;
                return true;
            }

            obj = null;
            return false;
        }

        public static object GetClrObject(this IClrObject self)
        {
            if (self == null) { throw new ArgumentNullException("self"); }

            return self.ClrObject;
        }

        public static bool TryGetClrObject(this LuaValue self, out object obj)
        {
            var clrObject = self as IClrObject;
            if (clrObject != null) {
                obj = GetClrObject(clrObject);
                return true;
            }

            obj = null;
            return false;
        }

        public static object GetClrObject(this LuaValue self)
        {
            object obj;
            if (!TryGetClrObject(self, out obj)) {
                throw new ArgumentException("Does not represent a CLR object.", "self");
            }

            return obj;
        }
    }
}

