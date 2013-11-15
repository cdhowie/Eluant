//
// LuaVararg.cs
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
using System.Linq;

namespace Eluant
{
    public sealed class LuaVararg : IList<LuaValue>, IDisposable
    {
        private List<LuaValue> values;

        public LuaVararg(IEnumerable<LuaValue> values, bool takeOwnership)
        {
            if (values == null) { throw new ArgumentNullException("values"); }

            values = values.Select(i => i == null ? LuaNil.Instance : i);
            if (!takeOwnership) {
                // Caller wants to retain ownership, so we have to take new references where applicable.
                values = values.Select(i => i.CopyReference());
            }

            this.values = values.ToList();
        }

        private void CheckDisposed()
        {
            if (values == null) {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        // We don't need a finalizer since the values have their own. Dispose() here is a convenience to explicitly
        // dispose the entire result list; implicit disposal will happen already.
        public void Dispose()
        {
            if (values != null) {
                foreach (var v in values) {
                    v.Dispose();
                }

                values = null;
            }
        }

        #region IEnumerable implementation

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region IEnumerable implementation

        public IEnumerator<LuaValue> GetEnumerator()
        {
            CheckDisposed();

            return values.GetEnumerator();
        }

        #endregion

        #region ICollection implementation

        void ICollection<LuaValue>.Add(LuaValue item)
        {
            throw new NotSupportedException();
        }

        void ICollection<LuaValue>.Clear()
        {
            throw new NotSupportedException();
        }

        public bool Contains(LuaValue item)
        {
            CheckDisposed();

            return values.Contains(item);
        }

        public void CopyTo(LuaValue[] array, int arrayIndex)
        {
            CheckDisposed();

            values.CopyTo(array, arrayIndex);
        }

        bool ICollection<LuaValue>.Remove(LuaValue item)
        {
            throw new NotSupportedException();
        }

        public int Count
        {
            get { CheckDisposed(); return values.Count; }
        }

        bool ICollection<LuaValue>.IsReadOnly
        {
            get { return true; }
        }

        #endregion

        #region IList implementation

        public int IndexOf(LuaValue item)
        {
            CheckDisposed();
            return values.IndexOf(item);
        }

        void IList<LuaValue>.Insert(int index, LuaValue item)
        {
            throw new NotSupportedException();
        }

        void IList<LuaValue>.RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        public LuaValue this[int index]
        {
            get { CheckDisposed(); return values[index]; }
        }

        LuaValue IList<LuaValue>.this[int index]
        {
            get { CheckDisposed(); return values[index]; }
            set { throw new NotSupportedException(); }
        }

        #endregion

    }
}

