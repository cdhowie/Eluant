using System;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Eluant.Tests
{
    [TestFixture]
    public class MemoryConstraint
    {
        [Test]
        [ExpectedException(typeof(LuaException), ExpectedMessage="not enough memory")]
        public void BasicMemoryConstraint()
        {
            using (var runtime = new MemoryConstrainedLuaRuntime()) {
                runtime.MaxMemoryUse = runtime.MemoryUse + 10 * 1024 * 1024;

                // Exponentially allocate memory.
                runtime.DoString("x = '.' while true do x = x .. x end");
            }
        }

        [Test]
        public void NoMemoryErrorWhileInClr()
        {
            using (var runtime = new MemoryConstrainedLuaRuntime()) {
                Action fn = () => {
                    runtime.MaxMemoryUse = runtime.MemoryUse + 1;

                    using (var x = runtime.CreateTable()) {
                        x[1] = "This is a string that is way more than one byte long.";
                    }

                    runtime.MaxMemoryUse = long.MaxValue;
                };

                using (var callback = runtime.CreateFunctionFromDelegate(fn)) {
                    runtime["callback"] = callback;
                }

                runtime.DoString("callback()").Dispose();
            }
        }
    }
}

