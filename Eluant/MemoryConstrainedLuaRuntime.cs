using System;
using System.Runtime.InteropServices;

namespace Eluant
{
    public class MemoryConstrainedLuaRuntime : LuaRuntime
    {
        public long MemoryUse { get; private set; }

        private long maxMemoryUse = long.MaxValue;

        public long MaxMemoryUse {
            get { return maxMemoryUse; }
            set { maxMemoryUse = value; }
        }

        private bool inLua = false;

        public MemoryConstrainedLuaRuntime() { }

        protected override LuaAllocator CreateAllocatorDelegate()
        {
            return Allocate;
        }

        protected override void OnEnterClr()
        {
            inLua = false;
        }

        protected override void OnEnterLua()
        {
            inLua = true;
        }

        // We can't ever fail when in the CLR, because that would cause a Lua error (and therefore a longjmp) so we
        // maintain a flag indicating which runtime we are in.  If in the CLR then we never fail, but we still keep
        // track of memory allocation.
        //
        // Note that we can never fail when newSize < oldSize; Lua makes the assumption that failure is not possible in
        // that case.
        private IntPtr Allocate(IntPtr userData, IntPtr ptr, IntPtr oldSize, IntPtr newSize)
        {
            long newUse = MemoryUse;

            try {
                if (oldSize == newSize) {
                    // Do nothing, will return ptr.
                } else if (oldSize == IntPtr.Zero) {
                    // New allocation.
                    newUse += newSize.ToInt64();

                    if (inLua && newUse > MaxMemoryUse) {
                        newUse = MemoryUse; // Reset newUse.
                        ptr = IntPtr.Zero;
                    } else {
                        ptr = Marshal.AllocHGlobal(newSize);
                    }
                } else if (newSize == IntPtr.Zero) {
                    // Free allocation.
                    Marshal.FreeHGlobal(ptr);

                    newUse -= oldSize.ToInt64();

                    ptr = IntPtr.Zero;
                } else {
                    // Resizing existing allocation.
                    newUse += newSize.ToInt64() - oldSize.ToInt64();

                    // We can't fail when newSize < oldSize, Lua depends on that.
                    if (inLua && newSize.ToInt64() > oldSize.ToInt64() && newUse > MaxMemoryUse) {
                        newUse = MemoryUse; // Reset newUse.
                        ptr = IntPtr.Zero;
                    } else {
                        ptr = Marshal.ReAllocHGlobal(ptr, newSize);
                    }
                }
            } catch {
                newUse = MemoryUse;
                ptr = IntPtr.Zero;
            }

            MemoryUse = newUse;
            return ptr;
        }
    }
}

