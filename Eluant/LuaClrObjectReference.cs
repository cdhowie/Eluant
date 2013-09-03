using System;

namespace Eluant
{
    public sealed class LuaClrObjectReference : LuaUserdata, IClrObject
    {
        public LuaClrObjectReference(LuaRuntime runtime, int reference) : base(runtime, reference) { }

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
            return string.Format("[LuaClrObjectReference ClrObject:{0}]", ClrObject);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            isClrObjectValueCached = false;
            cachedClrObjectValue = null;
        }

        private bool isClrObjectValueCached = false;
        private LuaClrObjectValue cachedClrObjectValue = null;

        public LuaClrObjectValue ClrObjectValue
        {
            get {
                CheckDisposed();

                if (!isClrObjectValueCached) {
                    Runtime.Push(this);
                    cachedClrObjectValue = Runtime.GetClrObject<LuaClrObjectValue>(-1);
                    LuaApi.lua_pop(Runtime.LuaState, 1);

                    isClrObjectValueCached = true;
                }

                return cachedClrObjectValue;
            }
        }

        public object ClrObject
        {
            get { return ClrObjectValue.ClrObject; }
        }

        new public LuaWeakReference<LuaClrObjectReference> CreateWeakReference()
        {
            CheckDisposed();

            return Runtime.CreateWeakReference(this);
        }

        internal override object ToClrType(Type type)
        {
            try {
                return ClrObjectValue.ToClrType(type);
            } catch (ArgumentException) { }

            return base.ToClrType(type);
        }
    }
}

