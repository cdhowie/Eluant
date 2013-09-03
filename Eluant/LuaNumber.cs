using System;

namespace Eluant
{
    public sealed partial class LuaNumber : LuaValueType,
        IEquatable<LuaNumber>, IEquatable<double>,
        IComparable<LuaNumber>, IComparable<double>,
        IComparable
    {
        public double Value { get; private set; }

        public LuaNumber(double value)
        {
            Value = value;

            hashCode = typeof(LuaNumber).GetHashCode() ^ value.GetHashCode();
        }

        internal override void Push(LuaRuntime runtime)
        {
            LuaApi.lua_pushnumber(runtime.LuaState, Value);
        }

        public override bool ToBoolean()
        {
            return Value != 0.0;
        }

        public override double? ToNumber()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        internal override object ToClrType(Type type)
        {
            if (type == null) { throw new ArgumentNullException("type"); }

            try {
                return Convert.ChangeType(Value, type);
            } catch { }

            return base.ToClrType(type);
        }

        private int hashCode;

        public override int GetHashCode()
        {
            return hashCode;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LuaNumber);
        }

        public override bool Equals(LuaValue other)
        {
            return Equals(other as LuaNumber);
        }

        #region IEquatable<T>

        public bool Equals(LuaNumber n)
        {
            if (object.ReferenceEquals(n, null)) { return false; }

            return Value == n.Value;
        }

        public bool Equals(double n)
        {
            return Value == n;
        }

        #endregion

        #region IComparable<T>

        public int CompareTo(LuaNumber n)
        {
            if (object.ReferenceEquals(n, null)) { throw new ArgumentNullException("n"); }

            return Value.CompareTo(n.Value);
        }

        public int CompareTo(double n)
        {
            return Value.CompareTo(n);
        }

        public int CompareTo(object n)
        {
            if (n is double) { return CompareTo((double)n); }

            var luaN = n as LuaNumber;
            if (!object.ReferenceEquals(luaN, null)) { return CompareTo(luaN); }

            throw new ArgumentException("Must be a double or a LuaNumber.", "n");
        }

        #endregion

        #region Implicit conversions

        // This should be enough... all numeric types (except decimal) have an implicit conversion to double.
        public static implicit operator LuaNumber(double? n)
        {
            return n.HasValue ? new LuaNumber(n.Value) : null;
        }

        public static implicit operator double?(LuaNumber n)
        {
            if (object.ReferenceEquals(n, null)) { return null; }

            return n.Value;
        }

        #endregion

        // Some of the arithmetic operators return double instead of LuaNumber, the theory being that arithmetic can be
        // performed without creating temporary objects, and the result can be implicitly converted back to LuaNumber.
        //
        // It would also decrease the number of calls to the custom operators.

        #region Unary operators

        public static LuaNumber operator+(LuaNumber n)
        {
            return n;
        }

        public static double? operator-(LuaNumber n)
        {
            return -(double?)n;
        }

        // No ++ or --.  LuaNumber is immutable, so they don't make sense.

        #endregion
    }
}

