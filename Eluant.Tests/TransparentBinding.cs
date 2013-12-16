//
// TransparentBinding.cs
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
    public class TransparentBinding
    {
        private class BasicTestObject
        {
            [LuaMember] public int a;
            [LuaMember] public double b;

            [LuaMember("c")] public string C;

            private string d;

            [LuaMember("e")] public string E
            {
                get { return d; }
                set { d = value; }
            }

            [LuaMember("square")]
            [LuaMember("sqr")]
            public double Square(double a)
            {
                return a * a;
            }
        }

        [Test]
        public void Basic()
        {
            var obj = new BasicTestObject();

            obj.a = 42;

            using (var runtime = new LuaRuntime()) {
                runtime.Globals["obj"] = new LuaTransparentClrObject(obj);

                var script = @"
                    local old_a = obj.a

                    obj.a = 50
                    obj.b = 51
                    obj.c = 'foo'
                    obj.e = 'bar'

                    return { a=old_a, n=obj.square(4), o=obj.sqr(5) }
                ";

                using (var result = runtime.DoString(script)) {
                    var t = (LuaTable)result[0];

                    Assert.AreEqual(50, obj.a, "obj.a");
                    Assert.AreEqual(51, obj.b, "obj.b");
                    Assert.AreEqual("foo", obj.C, "obj.C");
                    Assert.AreEqual("bar", obj.E, "obj.E");

                    Assert.AreEqual(42, ((LuaNumber)t["a"]).Value, "t.a");
                    Assert.AreEqual(4 * 4, ((LuaNumber)t["n"]).Value, "t.n");
                    Assert.AreEqual(5 * 5, ((LuaNumber)t["o"]).Value, "t.o");
                }
            }
        }
    }
}

