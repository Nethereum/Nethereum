using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nethereum.Util
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly partial struct EvmInt256 : IEquatable<EvmInt256>, IComparable<EvmInt256>
    {
        private readonly EvmUInt256 _value;

        public static readonly EvmInt256 Zero = default;
        public static readonly EvmInt256 One = new(EvmUInt256.One);
        public static readonly EvmInt256 MinusOne = new(EvmUInt256.MaxValue);

        // INT256_MIN = -(2^255) = 0x8000...0000
        public static readonly EvmInt256 MinValue = new(new EvmUInt256(1UL << 63, 0, 0, 0));
        // INT256_MAX = 2^255 - 1 = 0x7FFF...FFFF
        public static readonly EvmInt256 MaxValue = new(new EvmUInt256((1UL << 63) - 1, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal EvmInt256(EvmUInt256 value) { _value = value; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EvmInt256(long value) { _value = new EvmUInt256(value); }

        // --- Sign Detection ---

        public bool IsNegative => _value.IsHighBitSet;
        public bool IsZero => _value.IsZero;
        public bool IsPositive => !IsNegative && !IsZero;

        // --- Access to underlying value ---

        public EvmUInt256 AsUInt256 => _value;
        public ulong U0 => _value.U0;
        public ulong U1 => _value.U1;
        public ulong U2 => _value.U2;
        public ulong U3 => _value.U3;

        // --- Abs ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EvmUInt256 Abs()
        {
            return IsNegative ? _value.Negate() : _value;
        }

        // --- Arithmetic ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EvmInt256 operator +(EvmInt256 a, EvmInt256 b)
            => new(a._value + b._value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EvmInt256 operator -(EvmInt256 a, EvmInt256 b)
            => new(a._value - b._value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EvmInt256 operator -(EvmInt256 a)
            => new(a._value.Negate());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EvmInt256 operator ++(EvmInt256 value)
            => new(value._value + EvmUInt256.One);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EvmInt256 operator --(EvmInt256 value)
            => new(value._value - EvmUInt256.One);

        public static EvmInt256 operator *(EvmInt256 a, EvmInt256 b)
        {
            // Signed multiplication: result sign = XOR of signs
            // Magnitude = |a| * |b|, then negate if signs differ
            bool aNeg = a.IsNegative;
            bool bNeg = b.IsNegative;
            var aAbs = aNeg ? a._value.Negate() : a._value;
            var bAbs = bNeg ? b._value.Negate() : b._value;
            var result = aAbs * bAbs;
            return new EvmInt256(aNeg != bNeg ? result.Negate() : result);
        }

        // Signed division following EVM SDIV semantics
        public static EvmInt256 operator /(EvmInt256 a, EvmInt256 b)
        {
            if (b._value.IsZero) return Zero;

            // Special case: INT256_MIN / -1 = INT256_MIN (overflow, like EVM)
            if (a == MinValue && b == MinusOne) return MinValue;

            bool aNeg = a.IsNegative;
            bool bNeg = b.IsNegative;
            var aAbs = aNeg ? a._value.Negate() : a._value;
            var bAbs = bNeg ? b._value.Negate() : b._value;
            var result = aAbs / bAbs;
            return new EvmInt256(aNeg != bNeg ? result.Negate() : result);
        }

        // Signed modulo following EVM SMOD semantics: result sign = sign of dividend
        public static EvmInt256 operator %(EvmInt256 a, EvmInt256 b)
        {
            if (b._value.IsZero) return Zero;

            bool aNeg = a.IsNegative;
            bool bNeg = b.IsNegative;
            var aAbs = aNeg ? a._value.Negate() : a._value;
            var bAbs = bNeg ? b._value.Negate() : b._value;
            var result = aAbs % bAbs;
            return new EvmInt256(aNeg ? result.Negate() : result);
        }

        // --- Signed Comparison (following Int128 pattern) ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(EvmInt256 a, EvmInt256 b)
        {
            bool aNeg = a.IsNegative;
            bool bNeg = b.IsNegative;
            if (aNeg != bNeg) return aNeg; // negative < positive
            return a._value < b._value; // same sign: unsigned comparison works
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(EvmInt256 a, EvmInt256 b) => b < a;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(EvmInt256 a, EvmInt256 b) => !(b < a);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(EvmInt256 a, EvmInt256 b) => !(a < b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(EvmInt256 a, EvmInt256 b) => a._value == b._value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(EvmInt256 a, EvmInt256 b) => !(a == b);

        // --- Comparison with int/long ---

        public static bool operator >(EvmInt256 a, long b) => a > new EvmInt256(b);
        public static bool operator <(EvmInt256 a, long b) => a < new EvmInt256(b);
        public static bool operator >=(EvmInt256 a, long b) => a >= new EvmInt256(b);
        public static bool operator <=(EvmInt256 a, long b) => a <= new EvmInt256(b);
        public static bool operator ==(EvmInt256 a, long b) => a == new EvmInt256(b);
        public static bool operator !=(EvmInt256 a, long b) => !(a == b);

        // --- Shifts ---

        public static EvmInt256 operator <<(EvmInt256 a, int shift)
            => new(a._value << shift);

        public static EvmInt256 operator >>(EvmInt256 a, int shift)
            => new(a._value.ArithmeticRightShift(shift));

        // --- Implicit Conversions ---

        public static implicit operator EvmInt256(int value) => new((long)value);
        public static implicit operator EvmInt256(long value) => new(value);

        // --- Explicit Conversions ---

        public static explicit operator EvmInt256(EvmUInt256 value) => new(value);
        public static explicit operator EvmUInt256(EvmInt256 value) => value._value;
        public static explicit operator int(EvmInt256 value) => (int)value._value.U0;
        public static explicit operator long(EvmInt256 value) => (long)value._value.U0;

        // --- Min/Max ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EvmInt256 Min(EvmInt256 a, EvmInt256 b) => a < b ? a : b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EvmInt256 Max(EvmInt256 a, EvmInt256 b) => a > b ? a : b;

        // --- Object Overrides ---

        public override bool Equals(object obj) => obj is EvmInt256 other && this == other;
        public bool Equals(EvmInt256 other) => this == other;
        public override int GetHashCode() => _value.GetHashCode();

        public int CompareTo(EvmInt256 other)
        {
            if (this < other) return -1;
            if (this > other) return 1;
            return 0;
        }

        public override string ToString()
        {
            if (IsZero) return "0";
            if (!IsNegative) return _value.ToDecimalString();
            return "-" + _value.Negate().ToDecimalString();
        }
    }
}
