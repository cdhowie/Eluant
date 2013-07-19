using System;
using System.Collections.Generic;

namespace Eluant
{
    public static class LuaValueExtensions
    {
        public static bool IsNil(this LuaValue self)
        {
            return self == null || self == LuaNil.Instance;
        }

        public static IEnumerable<LuaValue> EnumerateArray(this LuaTable self)
        {
            if (self == null) { throw new ArgumentNullException("self"); }

            return CreateEnumerateArrayEnumerable(self);
        }

        private static IEnumerable<LuaValue> CreateEnumerateArrayEnumerable(LuaTable self)
        {
            // By convention, the 'n' field refers to the array length, if present.
            using (var n = self["n"]) {
                var num = n as LuaNumber;
                if (num != null) {
                    var length = (int)num.Value;

                    for (int i = 1; i <= length; ++i) {
                        yield return self[i];
                    }

                    yield break;
                }
            }

            // If no 'n' then stop at the first nil element.
            for (int i = 1; ; ++i) {
                var value = self[i];
                if (value.IsNil()) {
                    yield break;
                }

                yield return value;
            }
        }

        public static void Dispose(this KeyValuePair<LuaValue, LuaValue> self)
        {
            if (self.Key != null) { self.Key.Dispose(); }
            if (self.Value != null) { self.Value.Dispose(); }
        }
    }
}

