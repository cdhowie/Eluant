using System;
using System.Linq;
using NUnit.Framework;

namespace Eluant.Tests
{
    [TestFixture]
    public class Functions
    {
        [Test]
        public void BasicFunction()
        {
            using (var runtime = new LuaRuntime()) {
                runtime.DoString("function basic_function(x) return x * 2 + 1 end");

                using (var fn = (LuaFunction)runtime.Globals["basic_function"]) {
                    using (var result = fn.Call(5)) {
                        Assert.AreEqual(1, result.Count, "result.Count");
                        Assert.AreEqual(11, result[0].ToNumber(), "result[0]");
                    }
                }
            }
        }

        [Test]
        public void Callback()
        {
            int? cbValue = null;
            Action<int> callback = x => cbValue = x;

            using (var runtime = new LuaRuntime()) {
                using (var wrapper = runtime.CreateFunctionFromDelegate(callback)) {
                    runtime.Globals["callback"] = wrapper;
                }

                runtime.DoString("callback(42)");
            }

            Assert.AreEqual(42, cbValue, "cbValue");
        }

        [Test]
        [ExpectedException(typeof(LuaException), ExpectedMessage="$TEST$", MatchType=MessageMatch.Contains)]
        public void LuaErrorPropagation()
        {
            using (var runtime = new LuaRuntime()) {
                runtime.DoString("error('$TEST$')");
            }
        }

        [Test]
        [ExpectedException(typeof(LuaException), ExpectedMessage="$TEST$", MatchType=MessageMatch.Contains)]
        public void ClrErrorPropagation()
        {
            Action thrower = () => { throw new LuaException("$TEST$"); };

            using (var runtime = new LuaRuntime()) {
                using (var wrapper = runtime.CreateFunctionFromDelegate(thrower)) {
                    runtime.Globals["callback"] = wrapper;
                }

                runtime.DoString("callback()");
            }
        }

        [Test]
        [ExpectedException(typeof(LuaException), ExpectedMessage="InvalidOperationException", MatchType=MessageMatch.Contains)]
        public void ClrExceptionPropagation()
        {
            Action thrower = () => { throw new InvalidOperationException(); };

            using (var runtime = new LuaRuntime()) {
                using (var wrapper = runtime.CreateFunctionFromDelegate(thrower)) {
                    runtime.Globals["callback"] = wrapper;
                }

                runtime.DoString("callback()");
            }
        }

        private delegate void TypeMappingTestDelegate(
            int a, ulong b, double c, string d, LuaTable e, bool f, LuaTable g);

        [Test]
        public void TypeMapping()
        {
            bool called = false;
            TypeMappingTestDelegate cb = (a, b, c, d, e, f, g) => {
                Assert.AreEqual(10, a, "a");
                Assert.AreEqual(20, b, "b");
                Assert.AreEqual(0.5, c, "c");
                Assert.AreEqual("foobar", d, "d");
                Assert.AreEqual("dingus", e["widget"].ToString(), "e");
                Assert.AreEqual(true, f, "f");
                Assert.IsNull(g, "g");

                called = true;
            };

            using (var runtime = new LuaRuntime()) {
                using (var wrapper = runtime.CreateFunctionFromDelegate(cb)) {
                    runtime.Globals["callback"] = wrapper;
                }

                runtime.DoString("callback(10, 20, 0.5, 'foobar', { widget='dingus' }, true, nil)");
            }

            Assert.IsTrue(called, "called");
        }

        [Test]
        public void HugeResultList()
        {
            var range = Enumerable.Range(1, 1000);

            Func<LuaVararg> fn = () => new LuaVararg(range.Select(i => (LuaNumber)i), true);

            using (var runtime = new LuaRuntime()) {
                using (var f = runtime.CreateFunctionFromDelegate(fn)) {
                    runtime.Globals["callback"] = f;
                }

                using (var results = runtime.DoString("return callback()")) {
                    Assert.AreEqual(range.Sum(), results.Select(i => (int)i.ToNumber().Value).Sum());
                }
            }
        }

        [Test]
        public void CallbackOnCoroutineFails()
        {
            using (var runtime = new LuaRuntime()) {
                using (var callback = runtime.CreateFunctionFromDelegate(new Action(() => { Assert.Fail("Function called."); }))) {
                    runtime.Globals["callback"] = callback;
                }

                using (var r = runtime.DoString("return coroutine.resume(coroutine.create(callback))")) {
                    Assert.IsFalse(r[0].ToBoolean(), "Call succeeded.");
                }
            }
        }
    }
}

