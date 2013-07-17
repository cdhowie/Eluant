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

