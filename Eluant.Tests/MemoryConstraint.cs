//
// MemoryConstraint.cs
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
                    runtime.Globals["callback"] = callback;
                }

                runtime.DoString("callback()").Dispose();
            }
        }
    }
}

