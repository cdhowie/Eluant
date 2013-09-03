using System;

namespace Eluant
{
    public abstract class LuaClrObjectValue : LuaValueType, IClrObject
    {
        public object ClrObject { get; private set; }

        public LuaClrObjectValue(object obj)
        {
            ClrObject = obj;
        }

        public override bool ToBoolean()
        {
            return ClrObject != null;
        }

        public override double? ToNumber()
        {
            return null;
        }

        public override string ToString()
        {
            return string.Format("[{0}: ClrObject={1}]", GetType().Name, ClrObject);
        }

        internal abstract object BackingCustomObject { get; }

        internal override object ToClrType(Type type)
        {
            if (type == null) { throw new ArgumentNullException("type"); }

            if (ClrObject == null) {
                if (!type.IsValueType || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))) {
                    return null;
                }
            } else {
                if (type.IsAssignableFrom(ClrObject.GetType())) {
                    return ClrObject;
                }
            }

            return base.ToClrType(type);
        }
    }
}

