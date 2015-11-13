//
// Runtime.cs
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
using NUnit.Framework;
using System.Linq;

namespace Eluant.Tests
{
    [TestFixture]
    public class Runtime
    {
        [Test]
        public void LuaCollectsObjectsAfterReferencesAreDisposed()
        {
            // In this test we repeatedly create and destroy table references to make sure the Lua GC is able to collect
            // them.  We create a multiple of 1,000 tables since the runtime rewrites the reference table every 1,000
            // reference destructions.  This should return the runtime to exactly the same state as it was at the
            // beginning of the test.
            using (var runtime = new MemoryConstrainedLuaRuntime()) {
                using (var collect = (LuaFunction)runtime.Globals["collectgarbage"]) {
                    collect.Call().Dispose();

                    var begin = runtime.MemoryUse;

                    // Stress the GC a bit by creating and disposing tables, in batches of 100.
                    for (int i = 0; i < 1000; ++i) {
                        foreach (var t in Enumerable.Range(1, 100).Select(j => runtime.CreateTable()).ToList()) {
                            t.Dispose();
                        }
                    }

                    // Now create a whole bunch of tables all at once.
                    foreach (var t in Enumerable.Range(1, 10000).Select(j => runtime.CreateTable()).ToList()) {
                        t.Dispose();
                    }

                    collect.Call().Dispose();

                    Assert.AreEqual(begin, runtime.MemoryUse);
                }
            }
        }

        [Test]
        public void ReferenceTableRewriteDoesNotConfuseReferences()
        {
            // Make sure that after a reference table rewrite everything still points where it should.
            using (var runtime = new LuaRuntime()) {
                using (var t1 = runtime.CreateTable())
                using (var t2 = runtime.CreateTable()) {
                    t1["foo"] = "bar";
                    t2[5] = 6;
                    t2["fixture"] = new LuaOpaqueClrObject(this);

                    // 1000 cycles should trigger a rewrite.
                    foreach (var t in Enumerable.Range(1, 1000).Select(j => runtime.CreateTable()).ToList()) {
                        t.Dispose();
                    }

                    Assert.AreEqual("bar", t1["foo"].ToString());
                    Assert.AreEqual(6, t2[5].ToNumber());
                    using (var clrRef = (LuaClrObjectReference)t2["fixture"]) {
                        Assert.AreSame(this, clrRef.ClrObject);
                    }
                }
            }
        }

        [Test]
        public void NonAsciiStringMarshallingFromLua()
        {
            using (var runtime = new LuaRuntime()) {
                runtime.DoString(@"k = string.char(0xFC, 0xFF)").Dispose();

                LuaString clrString;
                using (var k = runtime.Globals["k"]) {
                    clrString = (LuaString)k;
                }

                Assert.AreEqual(2, clrString.AsByteArray().Length, "clrString.AsByteArray().Length");

                runtime.Globals["kclr"] = clrString;

                runtime.DoString("assert(k == kclr, 'k == kclr')").Dispose();
            }
        }

        [Test]
        public void NonAsciiStringMarshallingFromCLR()
        {
            var str = "a\u1234b";

            Assert.AreEqual(3, str.Length, "str.Length");

            using (var runtime = new LuaRuntime()) {
                runtime.Globals["s"] = str;

                Assert.AreEqual(str, runtime.Globals["s"].ToString(), "str == s");
            }
        }

        // This may seem like an unnecessary test, but it is actualy required to make sure that Lua runtimes can be
        // properly finalized when there are no outstanding managed references to them.  If we ever make the mistake of
        // passing something into the Lua runtime that represents a strong reference that the runtime is reachable from,
        // this will cause the runtime to become un-finalizable (it can still be disposed of, of course).
        //
        // This test helps make sure that this isn't accidentally done in binding code.
        [Test]
        public void Finalizer()
        {
            var finalized = false;
            var luaState = IntPtr.Zero;

            // The GC (at least on Mono) is a bit picky, and an object reference left in the register or somewhere on
            // the call stack can cause the object to be ineligible.  So we recurse 10 times and then run our test.
            // Because of the recursion (as well as the use of a delegate) this seems fairly certain to make sure that
            // Mono's GC can collect the object.
            RecurseAndThen(10, () => 
                new LuaRuntimeWithFinalizerCallback(state => {
                    finalized = true;
                    luaState = state;
                }));

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.IsTrue(finalized, "finalized");
            Assert.AreEqual(luaState.ToInt64(), IntPtr.Zero.ToInt64(), "luaState");
        }

        private static int RecurseAndThen(int times, Action callback)
        {
            if (times == 0) {
                callback();
                return 0;
            }

            // No tailcalls please.
            return RecurseAndThen(times - 1, callback) + 1;
        }

        // We test MemoryConstrainedLuaRuntime here because that is the most complex and has shown issues with not
        // being eligible for finalization in the past.
        private class LuaRuntimeWithFinalizerCallback : MemoryConstrainedLuaRuntime
        {
            private Action<IntPtr> finalizerCallback;

            public LuaRuntimeWithFinalizerCallback(Action<IntPtr> finalizerCallback)
            {
                this.finalizerCallback = finalizerCallback;
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);

                if (!disposing) {
                    finalizerCallback(LuaState);
                }
            }
        }
    }
}

