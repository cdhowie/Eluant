using System;
using NUnit.Framework;

namespace Eluant.Tests
{
    [TestFixture]
    public class Types
    {
        [Test]
        public void Boolean()
        {
            using (var runtime = new LuaRuntime()) {
                runtime["t"] = true;
                using (var t = runtime["t"]) { Assert.AreEqual(t, LuaBoolean.True); }

                runtime["t"] = false;
                using (var t = runtime["t"]) { Assert.AreEqual(t, LuaBoolean.False); }
            }
        }

        [Test]
        public void StringOperators()
        {
            LuaString s = "Hello, World!";

            Assert.IsTrue(s == "Hello, World!"); // (LS, s)
            Assert.IsTrue(s == new LuaString("Hello, World!")); // (LS, LS)

            Assert.IsTrue("Hello, World!" == s); // (s, LS)
            Assert.IsTrue(new LuaString("Hello, World!") == s); // (LS, LS)

            Assert.IsFalse(s != "Hello, World!"); // (LS, s)
            Assert.IsFalse(s != new LuaString("Hello, World!")); // (LS, LS)

            Assert.IsFalse("Hello, World!" != s); // (s, LS)
            Assert.IsFalse(new LuaString("Hello, World!") != s); // (LS, LS)
        }

        [Test]
        public void NumberOperators()
        {
            LuaNumber ln = 5;

            Assert.IsTrue(6 == ln + 1);
            Assert.IsTrue(1 + ln == 6);
            Assert.IsTrue(6 == (LuaNumber)1 + ln);

            Assert.IsTrue(20 == ln * 4);

            Assert.IsNull((LuaNumber)null + 5);
        }

        [Test]
        public void OpaqueClrObject()
        {
            using (var runtime = new LuaRuntime()) {
                runtime["o"] = new LuaOpaqueClrObject(this);

                using (var o = (LuaOpaqueClrObjectReference)runtime["o"]) {
                    Assert.AreSame(this, o.ClrObject);
                }
            }
        }

        [Test]
        public void Table()
        {
            using (var runtime = new LuaRuntime()) {
                using (var table = runtime.CreateTable()) {
                    table["foo"] = "bar";
                    runtime["t"] = table;
                }

                using (var results = runtime.DoString("return t['foo']")) {
                    Assert.AreEqual(1, results.Count, "results.Count");
                    Assert.AreEqual("bar", results[0].ToString(), "results[0]");
                }
            }
        }

        [Test]
        public void DifferentReferencesToSameClrObjectAreUnequal()
        {
            using (var runtime = new LuaRuntime()) {
                var obj = new LuaOpaqueClrObject(this);

                LuaFunction fn;
                using (var r = runtime.DoString("return function(a, b) return a == b end")) {
                    fn = (LuaFunction)r[0].CopyReference();
                }

                using (var r = fn.Call(obj, obj)) {
                    Assert.AreEqual(1, r.Count, "r.Count");
                    Assert.IsFalse(r[0].ToBoolean(), "r[0]");
                }
            }
        }

        [Test]
        public void WeakReferences()
        {
            using (var runtime = new LuaRuntime()) {
                LuaWeakReference<LuaTable> tableWeakRef;

                using (var table = runtime.CreateTable()) {
                    table["foo"] = "bar";

                    tableWeakRef = table.CreateWeakReference();

                    using (var tableRef = tableWeakRef.CreateReferenceToTarget()) {
                        Assert.IsNotNull(tableRef, "tableRef");
                        Assert.AreEqual("bar", tableRef["foo"].ToString(), "tableRef[\"foo\"]");
                    }
                }

                runtime.DoString("collectgarbage()");

                Assert.IsNull(tableWeakRef.CreateReferenceToTarget(), "tableWeakRef.Target");

                tableWeakRef.Dispose();
            }
        }

        [Test]
        public void DisallowCrossRuntimeReferences()
        {
            using (var runtime1 = new LuaRuntime())
            using (var runtime2 = new LuaRuntime()) {
                using (var table = runtime1.CreateTable()) {
                    Assert.Throws<InvalidOperationException>(() => runtime2["foo"] = table);

                    using (var table2 = runtime2.CreateTable()) {
                        Assert.Throws<InvalidOperationException>(() => table["foo"] = table2);
                    }
                }
            }
        }
    }
}

