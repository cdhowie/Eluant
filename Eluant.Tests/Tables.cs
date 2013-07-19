using System;
using NUnit.Framework;
using System.Linq;
using System.Collections.Generic;

namespace Eluant.Tests
{
    [TestFixture]
    public class Tables
    {
        [Test]
        public void Count()
        {
            using (var runtime = new LuaRuntime()) {
                using (var t = runtime.CreateTable()) {
                    Assert.AreEqual(0, t.Count);

                    t[1] = 1;
                    t[3] = "foo";
                    t["x"] = "bar";
                    t[6] = null;

                    Assert.AreEqual(3, t.Count);
                }
            }
        }

        [Test]
        public void Clear()
        {
            using (var runtime = new LuaRuntime()) {
                using (var t = runtime.CreateTable(Enumerable.Range(1, 100).Select(i => (LuaValue)i))) {
                    Assert.AreEqual(100, t.Count);

                    t.Clear();

                    Assert.AreEqual(0, t.Count);
                }
            }
        }

        [Test]
        public void KeysAndValues()
        {
            using (var runtime = new LuaRuntime()) {
                using (var t = runtime.CreateTable()) {
                    t[1] = 2;
                    t[3] = 4;
                    t["foo"] = "bar";

                    var keys = new HashSet<LuaValue>(new LuaValue[] { 1, 3, "foo" });
                    var values = new HashSet<LuaValue>(new LuaValue[] { 2, 4, "bar" });

                    Assert.IsTrue(keys.SetEquals(t.Keys), "Keys");
                    Assert.IsTrue(values.SetEquals(t.Values), "Values");
                }
            }
        }
    }
}

