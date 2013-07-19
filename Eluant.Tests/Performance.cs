using System;
using NUnit.Framework;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;

namespace Eluant.Tests
{
    [TestFixture]
    [Explicit]
    public class Performance
    {
        [Test]
        public void ClrCallLoopVsVarargVsTable()
        {
            // Measures three approaches to processing 4,000 items through a Lua function fn().
            //
            // 1. Call fn() 4,000 times from C#.
            // 2. Call with 4,000 arguments a Lua function that passes each argument through fn(), returning all 4,000
            //    results.
            // 3. Build a table with 4,000 elements in C#, and pass that to a function that calls fn() on each element,
            //    replacing it with the result, and returning nothing.
            //
            // The results appear to be that 1 is fastest, 3 is very slightly slower than 1, and 2 is almost 20x slower
            // than 1.  This is good, as it means that the most straightforward and intuitive way of using Eluant in
            // such a scenario is the fastest, and that tricky optimization techniques are not required.
            const int ITEMS = 4000;

            var clrSw = new Stopwatch();
            var varargSw = new Stopwatch();
            var tableSw = new Stopwatch();

            IList<double> clrResults, varargResults, tableResults;

            var inputs = Enumerable.Range(1, ITEMS);
            using (var runtime = new LuaRuntime()) {
                runtime.DoString(@"
-- Main loop function
function fn(x) return x * x end

-- Function for iterating the loop in Lua and collecting the results
function loop(...)
    local function iter(x, ...)
        if x then return fn(x), iter(...) end
    end

    return iter(...)
end

-- Same, but using tables
function loop_table(t)
    for k,v in ipairs(t) do
        t[k] = fn(v)
    end
end
");
                using (var fn = (LuaFunction)runtime.Globals["fn"])
                using (var loop = (LuaFunction)runtime.Globals["loop"])
                using (var loop_table = (LuaFunction)runtime.Globals["loop_table"]) {
                    // CLR loop.
                    clrResults = new List<double>(ITEMS);
                    clrSw.Start();
                    foreach (var i in inputs) {
                        using (var r = fn.Call(i)) {
                            clrResults.Add(r[0].ToNumber().Value);
                        }
                    }
                    clrSw.Stop();

                    // Lua vararg.
                    varargSw.Start();
                    using (var r = loop.Call(inputs.Select(i => (LuaValue)i).ToList())) {
                        varargResults = r.Select(i => i.ToNumber().Value).ToList();
                    }
                    varargSw.Stop();

                    // Lua loop over table.
                    tableResults = new double[ITEMS];
                    tableSw.Start();
                    using (var table = runtime.CreateTable(inputs.Select(i => (LuaValue)i))) {
                        loop_table.Call(table).Dispose();

                        for (int i = 0; i < ITEMS; ++i) {
                            tableResults[i] = table[i + 1].ToNumber().Value;
                        }
                    }
                    tableSw.Stop();
                }
            }

            Assert.IsTrue(clrResults.SequenceEqual(varargResults), "Results equal (CLR/vararg)");
            Assert.IsTrue(clrResults.SequenceEqual(tableResults), "Results equal (CLR/table)");

            Assert.Inconclusive("CLR time: {0}, Vararg time: {1}, Table time: {2}", clrSw.Elapsed, varargSw.Elapsed, tableSw.Elapsed);
        }
    }
}

