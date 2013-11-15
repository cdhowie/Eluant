//
// Types.cs
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
using Eluant.ObjectBinding;

namespace Eluant.Tests
{
    [TestFixture]
    public class Types
    {
        [Test]
        public void Boolean()
        {
            using (var runtime = new LuaRuntime()) {
                runtime.Globals["t"] = true;
                using (var t = runtime.Globals["t"]) { Assert.AreEqual(t, LuaBoolean.True); }

                runtime.Globals["t"] = false;
                using (var t = runtime.Globals["t"]) { Assert.AreEqual(t, LuaBoolean.False); }
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
                var objValue = new LuaOpaqueClrObject(this);
                runtime.Globals["o"] = objValue;

                using (var o = (LuaClrObjectReference)runtime.Globals["o"]) {
                    Assert.AreSame(objValue, o.ClrObjectValue, "o.ClrObjectValue");
                    Assert.AreSame(this, o.ClrObject, "o.ClrObject");
                }
            }
        }

        [Test]
        public void OpaqueClrObjectAsArgument()
        {
            object object1 = new object();
            object object2 = null;

            using (var runtime = new LuaRuntime()) {
                runtime.Globals["o"] = new LuaOpaqueClrObject(object1);

                using (var d = runtime.CreateFunctionFromDelegate(new Action<object>(o => object2 = o))) {
                    runtime.Globals["fn"] = d;
                }

                runtime.DoString("fn(o)").Dispose();
            }

            Assert.AreSame(object1, object2);
        }

        [Test]
        public void Table()
        {
            using (var runtime = new LuaRuntime()) {
                using (var table = runtime.CreateTable()) {
                    table["foo"] = "bar";
                    runtime.Globals["t"] = table;
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
                    Assert.Throws<InvalidOperationException>(() => runtime2.Globals["foo"] = table);

                    using (var table2 = runtime2.CreateTable()) {
                        Assert.Throws<InvalidOperationException>(() => table["foo"] = table2);
                    }
                }
            }
        }

        [Test]
        public void AdditionBinding()
        {
            using (var runtime = new LuaRuntime()) {
                runtime.DoString("function fn(o) return (2 + o) + 3 end").Dispose();

                using (var fn = (LuaFunction)runtime.Globals["fn"]) {
                    using (var results = fn.Call(new LuaCustomClrObject(new MathBindingObject(5)))) {
                        Console.WriteLine(results[0]);
                    }
                }
            }
        }

        private class MathBindingObject : ILuaAdditionBinding
        {
            public double Value { get; private set; }

            public MathBindingObject(double value)
            {
                Value = value;
            }

            private static double ToNumber(LuaValue value)
            {
                return value.ToNumber() ?? ((MathBindingObject)value.GetClrObject()).Value;
            }

            #region ILuaAdditionBinding implementation

            public LuaValue Add(LuaRuntime runtime, LuaValue left, LuaValue right)
            {
                return new LuaCustomClrObject(new MathBindingObject(1 + ToNumber(left) + ToNumber(right)));
            }

            #endregion
        }

        [Test]
        public void TransparentBinding()
        {
            using (var runtime = new LuaRuntime()) {
                var script = @"
function fn(o)
    local r = {
        two = o.two(),
        three = o.three,
        four = o.Four(),
        five = o.add_two(3),
        GetType = o.GetType
    }

    return r
end
";

                runtime.DoString(script).Dispose();

                using (var fn = (LuaFunction)runtime.Globals["fn"]) {
                    var results = (LuaTable)fn.Call(new LuaTransparentClrObject(new TransparentBindingObject()))[0];

                    Assert.AreEqual(new LuaNumber(2), results["two"], "two");
                    Assert.AreEqual(new LuaNumber(3), results["three"], "three");
                    Assert.AreEqual(new LuaNumber(4), results["four"], "four");
                    Assert.AreEqual(new LuaNumber(5), results["five"], "five");
                    Assert.AreEqual(LuaNil.Instance, results["GetType"], "GetType");
                }
            }
        }

        private class TransparentBindingObject
        {
            [LuaMember("two")]
            public int Two()
            {
                return 2;
            }

            [LuaMember("three")]
            public int Three
            {
                get { return 3; }
            }

            [LuaMember]
            public int Four()
            {
                return 4;
            }

            [LuaMember("add_two")]
            public int AddTwo(int i)
            {
                return i + 2;
            }
        }

        [Test]
        public void TransparentObjectEquality()
        {
            using (var runtime = new LuaRuntime()) {
                var script = @"
function fn(a, b)
    return {
        ['a == b'] = (a == b),
        ['a == 1'] = (a == 1),
        ['1 == b'] = (1 == b),
    }
end
";

                runtime.DoString(script).Dispose();

                using (var fn = (LuaFunction)runtime.Globals["fn"])
                using (var results = fn.Call(new LuaTransparentClrObject(this), new LuaTransparentClrObject(this))) {
                    var table = (LuaTable)results[0];

                    Assert.AreEqual(LuaBoolean.True, table["a == b"], "a == b");
                    Assert.AreEqual(LuaBoolean.False, table["a == 1"], "a == 1");
                    Assert.AreEqual(LuaBoolean.False, table["1 == b"], "1 == b");
                }
            }
        }
    }
}
