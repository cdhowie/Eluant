//
// LuaTable.cs
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
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace Eluant
{
    public class LuaTable : LuaReference, IDictionary<LuaValue, LuaValue>
    {
        internal LuaTable(LuaRuntime runtime, int reference) : base(runtime, reference)
        {
            Keys = new KeyCollection(this);
            Values = new ValueCollection(this);
        }

        public override bool ToBoolean()
        {
            return true;
        }

        public override double? ToNumber()
        {
            return null;
        }

        public override string ToString()
        {
            return "[LuaTable]";
        }

        public LuaValue this[LuaValue key] {
            get {
                CheckDisposed();

                var top = LuaApi.lua_gettop(Runtime.LuaState);

                Runtime.Push(this);
                Runtime.Push(key);
                LuaApi.lua_gettable(Runtime.LuaState, -2);

                LuaValue val;

                try {
                    val = Runtime.Wrap(-1);
                } finally {
                    LuaApi.lua_settop(Runtime.LuaState, top);
                }

                return val;
            }
            set {
                CheckDisposed();

                // Lua allows one to query the nil index (always returning nil) but never to set it.
                if (key.IsNil()) { throw new ArgumentNullException("key"); }

                var top = LuaApi.lua_gettop(Runtime.LuaState);

                Runtime.Push(this);
                Runtime.Push(key);
                Runtime.Push(value);

                LuaApi.lua_settable(Runtime.LuaState, -3);

                LuaApi.lua_settop(Runtime.LuaState, top);
            }
        }

        new public LuaWeakReference<LuaTable> CreateWeakReference()
        {
            CheckDisposed();

            return Runtime.CreateWeakReference(this);
        }

        #region IEnumerable implementation

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region IEnumerable implementation

        public IEnumerator<KeyValuePair<LuaValue, LuaValue>> GetEnumerator()
        {
            CheckDisposed();

            return new LuaTableEnumerator(this);
        }

        #endregion

        #region ICollection implementation

        void ICollection<KeyValuePair<LuaValue, LuaValue>>.Add(KeyValuePair<LuaValue, LuaValue> item)
        {
            CheckDisposed();

            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            CheckDisposed();

            Runtime.Push(this);

            // Go over each key and remove it until the table is empty.
            for (;;) {
                LuaApi.lua_pushnil(Runtime.LuaState);

                if (LuaApi.lua_next(Runtime.LuaState, -2) == 0) {
                    // Table is empty.
                    LuaApi.lua_pop(Runtime.LuaState, 1);
                    break;
                }

                // Replace the value with nil and set the key.
                LuaApi.lua_pop(Runtime.LuaState, 1);
                LuaApi.lua_pushnil(Runtime.LuaState);
                LuaApi.lua_settable(Runtime.LuaState, -3);

                // Next iteration will start from the next key by using a nil key again.
            }
        }

        public bool Contains(KeyValuePair<LuaValue, LuaValue> item)
        {
            CheckDisposed();

            using (var v = this[item.Key]) {
                return v.Equals(item.Value);
            }
        }

        public void CopyTo(KeyValuePair<LuaValue, LuaValue>[] array, int arrayIndex)
        {
            CheckDisposed();

            foreach (var p in this) {
                array[arrayIndex++] = p;
            }
        }

        public bool Remove(KeyValuePair<LuaValue, LuaValue> item)
        {
            CheckDisposed();

            using (var v = this[item.Key]) {
                if (v.Equals(item.Key)) {
                    return Remove(item.Key);
                }
            }

            return false;
        }

        public int Count
        {
            get {
                CheckDisposed();

                // Lua provides no way to retrieve the number of items in a table short of iterating the whole thing. :(
                int count = 0;

                Runtime.Push(this);

                LuaApi.lua_pushnil(Runtime.LuaState);
                while (LuaApi.lua_next(Runtime.LuaState, -2) != 0) {
                    ++count;
                    LuaApi.lua_pop(Runtime.LuaState, 1);
                }

                LuaApi.lua_pop(Runtime.LuaState, 1);

                return count;
            }
        }

        bool ICollection<KeyValuePair<LuaValue, LuaValue>>.IsReadOnly
        {
            get { return false; }
        }

        #endregion

        #region IDictionary implementation

        public void Add(LuaValue key, LuaValue value)
        {
            CheckDisposed();

            if (key.IsNil()) { throw new ArgumentNullException("key"); }

            Runtime.Push(this);
            Runtime.Push(key);

            LuaApi.lua_gettable(Runtime.LuaState, -2);
            if (LuaApi.lua_type(Runtime.LuaState, -1) != LuaApi.LuaType.Nil) {
                // Slot is occupied.
                LuaApi.lua_pop(Runtime.LuaState, 2);

                throw new ArgumentException("Table already contains given key.", "key");
            }

            // Slot is unoccupied.  Leave the nil there for now, we'll pop ALL the things at the end.

            Runtime.Push(key);
            Runtime.Push(value);
            LuaApi.lua_settable(Runtime.LuaState, -4);

            // Pop table and nil value.
            LuaApi.lua_pop(Runtime.LuaState, 2);
        }

        public bool ContainsKey(LuaValue key)
        {
            CheckDisposed();

            using (var v = this[key]) {
                return !v.IsNil();
            }
        }

        public bool Remove(LuaValue key)
        {
            // Wouldn't it be nice to do this?
            // this[key] = null;
            //
            // But we can't, since we have to return true if the item existed. :(

            // However, we can fast-fail if the key is nil.
            if (key.IsNil()) { return false; }

            // this[] will CheckDisposed()
            using (var v = this[key]) {
                if (v.IsNil()) {
                    return false;
                }
            }

            this[key] = null;
            return true;
        }

        public bool TryGetValue(LuaValue key, out LuaValue value)
        {
            // This is tricky... since this[] never throws but instead returns nil, what do we do here?  I think that
            // it's still a good idea to return false here.
            //
            // Thankfully, the indexer makes this really easy to implement.

            value = this[key];
            return !value.IsNil();
        }

        public ICollection<LuaValue> Keys { get; private set; }

        public ICollection<LuaValue> Values { get; private set; }

        #endregion

        private class LuaTableEnumerator : IEnumerator<KeyValuePair<LuaValue, LuaValue>>
        {
            private LuaTable table;

            private bool atEnd = false;
            private LuaValue currentKey;
            private KeyValuePair<LuaValue, LuaValue> current;

            internal LuaTableEnumerator(LuaTable table)
            {
                this.table = table;
            }

            private void CheckDisposed()
            {
                if (table == null) { throw new ObjectDisposedException(GetType().FullName); }

                table.CheckDisposed();
            }

            #region IDisposable implementation

            public void Dispose()
            {
                table = null;
                if (currentKey != null) {
                    currentKey.Dispose();
                    currentKey = null;
                }
                current = new KeyValuePair<LuaValue, LuaValue>();
            }

            #endregion

            #region IEnumerator implementation

            public bool MoveNext()
            {
                CheckDisposed();

                // Fast-fail if we reached the end previously.
                if (atEnd) { return false; }

                var runtime = table.Runtime;

                runtime.Push(table);
                // Will push nil on first iteration, which is exactly what lua_next() expects.
                runtime.Push(currentKey);

                if (LuaApi.lua_next(runtime.LuaState, -2) == 0) {
                    // At the end, so only the table is on the stack now.
                    atEnd = true;
                    LuaApi.lua_pop(runtime.LuaState, 1);
                    return false;
                }

                var newValue = runtime.Wrap(-1);
                var newKey = runtime.Wrap(-2);

                current = new KeyValuePair<LuaValue, LuaValue>(newKey, newValue);

                if (currentKey != null) {
                    currentKey.Dispose();
                }

                currentKey = current.Key.CopyReference();

                LuaApi.lua_pop(runtime.LuaState, 3);

                return true;
            }

            public void Reset()
            {
                CheckDisposed();

                atEnd = false;
                if (currentKey != null) {
                    currentKey.Dispose();
                    currentKey = null;
                }
                current = new KeyValuePair<LuaValue, LuaValue>();
            }

            object IEnumerator.Current
            {
                get {
                    CheckDisposed();
                    return current;
                }
            }

            #endregion

            #region IEnumerator implementation

            public KeyValuePair<LuaValue, LuaValue> Current
            {
                get {
                    CheckDisposed();
                    return current;
                }
            }

            #endregion
        }

        private abstract class LuaValueCollection : ICollection<LuaValue>
        {
            protected LuaTable Table { get; private set; }

            internal LuaValueCollection(LuaTable table)
            {
                Table = table;
            }

            #region IEnumerable implementation

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            #endregion

            #region IEnumerable implementation

            public IEnumerator<LuaValue> GetEnumerator()
            {
                return Table.Select(pair => GetValueFromPair(pair)).GetEnumerator();
            }

            #endregion

            #region ICollection implementation

            public void Add(LuaValue item)
            {
                throw new NotSupportedException();
            }

            public void Clear()
            {
                throw new NotSupportedException();
            }

            public abstract bool Contains(LuaValue item);

            public void CopyTo(LuaValue[] array, int arrayIndex)
            {
                foreach (var pair in Table) {
                    array[arrayIndex++] = GetValueFromPair(pair);
                }
            }

            public bool Remove(LuaValue item)
            {
                throw new NotSupportedException();
            }

            public int Count
            {
                get { return Table.Count; }
            }

            public bool IsReadOnly
            {
                get { return true; }
            }

            #endregion

            protected abstract LuaValue GetValueFromPair(KeyValuePair<LuaValue, LuaValue> pair);
        }

        private class KeyCollection : LuaValueCollection
        {
            internal KeyCollection(LuaTable table) : base(table) { }

            public override bool Contains(LuaValue item)
            {
                return Table.ContainsKey(item);
            }

            protected override LuaValue GetValueFromPair(KeyValuePair<LuaValue, LuaValue> pair)
            {
                return pair.Key;
            }
        }

        private class ValueCollection : LuaValueCollection
        {
            internal ValueCollection(LuaTable table) : base(table) { }

            public override bool Contains(LuaValue item)
            {
                bool found;

                foreach (var pair in Table) {
                    found = pair.Value.Equals(item);

                    pair.Dispose();
                    if (found) { return true; }
                }

                return false;
            }

            protected override LuaValue GetValueFromPair(KeyValuePair<LuaValue, LuaValue> pair)
            {
                return pair.Value;
            }
        }
    }
}

