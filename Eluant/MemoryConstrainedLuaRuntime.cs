using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Eluant
{
    public class MemoryConstrainedLuaRuntime : LuaRuntime
    {
        // This gets complicated because of GC rules and how the runtime collects delegates.  To make things a bit
        // easier, we will maintain a strong reference to the object that has the allocator method until the runtime
        // gets disposed or finalized.  This way the delegate in question is not eligible for collection until after
        // the runtime is done with it.
        //
        // This doesn't make the "runtime shutting down" case better since all bets are off at that point, but it does
        // keep the runtime and the allocator delegate from being eligible at the same moment.
        private static HashSet<AllocatorState> allocatorStates = new HashSet<AllocatorState>();

        private AllocatorState allocatorState;

        public long MemoryUse
        {
            get {
                CheckDisposed();
                return allocatorState.MemoryUse;
            }
        }

        public long MaxMemoryUse {
            get {
                CheckDisposed();
                return allocatorState.MaxMemoryUse;
            }
            set {
                CheckDisposed();
                allocatorState.MaxMemoryUse = value;
            }
        }

        public MemoryConstrainedLuaRuntime()
        {
        }

        protected override void Dispose(bool disposing)
        {
            try {
                base.Dispose(disposing);
            } finally {
                lock (allocatorStates) {
                    allocatorStates.Remove(allocatorState);
                    allocatorState = null;
                }
            }
        }

        protected override LuaAllocator CreateAllocatorDelegate()
        {
            if (allocatorState == null) {
                allocatorState = new AllocatorState();

                lock (allocatorStates) {
                    allocatorStates.Add(allocatorState);
                }
            }

            return allocatorState.Allocator;
        }

        protected override void OnEnterClr()
        {
            allocatorState.InLua = false;
        }

        protected override void OnEnterLua()
        {
            allocatorState.InLua = true;
        }

        private class AllocatorState
        {
            public long MemoryUse;
            public long MaxMemoryUse = long.MaxValue;
            public bool InLua = false;

            public LuaRuntime.LuaAllocator Allocator { get; private set; }

            public AllocatorState()
            {
                Allocator = Allocate;
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

                        if (InLua && newUse > MaxMemoryUse) {
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
                        if (InLua && newSize.ToInt64() > oldSize.ToInt64() && newUse > MaxMemoryUse) {
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
}

