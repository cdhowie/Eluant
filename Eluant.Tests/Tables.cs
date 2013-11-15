//
// Tables.cs
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

