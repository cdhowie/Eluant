using System;

namespace Eluant
{
    public sealed class LuaOpaqueClrObjectReference : LuaLightUserdata
    {
        public LuaOpaqueClrObjectReference(LuaRuntime runtime, int reference) : base(runtime, reference) { }

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
            return "[LuaOpaqueClrObjectReference]";
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            cachedClrObject = null;
        }

        private bool isClrObjectCached = false;
        private object cachedClrObject = null;

        public object ClrObject
        {
            get {
                CheckDisposed();

                if (!isClrObjectCached) {
                    Runtime.Push(this);
                    cachedClrObject = Runtime.GetOpaqueClrObject(-1);
                    LuaApi.lua_pop(Runtime.LuaState, 1);

                    isClrObjectCached = true;
                }

                return cachedClrObject;
            }
        }

        new public LuaWeakReference<LuaOpaqueClrObjectReference> CreateWeakReference()
        {
            CheckDisposed();

            return Runtime.CreateWeakReference(this);
        }
    }
}

