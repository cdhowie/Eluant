//
// MemoryConstrainedLuaRuntime.cs
//
// Author:
//       Chris Howie <me@chrishowie.com>
//
// Copyright (c) 2013 Chris Howie
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Eluant
{
    public class MemoryConstrainedLuaRuntime : LuaRuntime
    {
        private static readonly LuaAllocator allocateCallback;

        static MemoryConstrainedLuaRuntime()
        {
            allocateCallback = Allocate;
        }
                
        private long memoryUse;
        private long maxMemoryUse = long.MaxValue;
        private bool inLua = false;

        public long MemoryUse
        {
            get {
                CheckDisposed();
                return memoryUse;
            }
        }

        public long MaxMemoryUse {
            get {
                CheckDisposed();
                return maxMemoryUse;
            }
            set {
                CheckDisposed();
                maxMemoryUse = value;
            }
        }

        public MemoryConstrainedLuaRuntime()
        {
        }

        protected override LuaAllocator CreateAllocatorDelegate(out IntPtr customState)
        {
            customState = (IntPtr)SelfHandle;

            return allocateCallback;
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
#if (__IOS__ || MONOTOUCH)
        [MonoTouch.MonoPInvokeCallback(typeof(LuaAllocator))]
#endif
        private static IntPtr Allocate(IntPtr userData, IntPtr ptr, IntPtr oldSize, IntPtr newSize)
        {
            var runtime = ((GCHandle)userData).Target as MemoryConstrainedLuaRuntime;

            long newUse = runtime.memoryUse;

            try {
                if (oldSize == newSize) {
                    // Do nothing, will return ptr.
                } else if (oldSize == IntPtr.Zero) {
                    // New allocation.
                    newUse += newSize.ToInt64();

                    if (runtime.inLua && newUse > runtime.maxMemoryUse) {
                        newUse = runtime.memoryUse; // Reset newUse.
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
                    if (runtime.inLua && newSize.ToInt64() > oldSize.ToInt64() && newUse > runtime.maxMemoryUse) {
                        newUse = runtime.memoryUse; // Reset newUse.
                        ptr = IntPtr.Zero;
                    } else {
                        ptr = Marshal.ReAllocHGlobal(ptr, newSize);
                    }
                }
            } catch {
                newUse = runtime.memoryUse;
                ptr = IntPtr.Zero;
            }

            runtime.memoryUse = newUse;
            return ptr;
        }
    }
}

