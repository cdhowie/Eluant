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
                using (var collect = (LuaFunction)runtime["collectgarbage"]) {
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
                    using (var clrRef = (LuaOpaqueClrObjectReference)t2["fixture"]) {
                        Assert.AreSame(this, clrRef.ClrObject);
                    }
                }
            }
        }
    }
}

