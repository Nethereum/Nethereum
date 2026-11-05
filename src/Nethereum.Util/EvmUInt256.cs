using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nethereum.Util
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly partial struct EvmUInt256 : IEquatable<EvmUInt256>, IComparable<EvmUInt256>
    {
        // Limb storage (4 × u64, little-endian limb order). Private fields exposed
        // as PascalCase properties for external access; internal code uses the
        // fields directly to avoid property-getter churn in hot paths.
        private readonly ulong _u3; // most significant
        private readonly ulong _u2;
        private readonly ulong _u1;
        private readonly ulong _u0; // least significant

        /// <summary>Least-significant 64-bit limb (bits 0–63).</summary>
        public ulong U0 => _u0;
        /// <summary>Second limb (bits 64–127).</summary>
        public ulong U1 => _u1;
        /// <summary>Third limb (bits 128–191).</summary>
        public ulong U2 => _u2;
        /// <summary>Most-significant 64-bit limb (bits 192–255).</summary>
        public ulong U3 => _u3;

        public static readonly EvmUInt256 Zero = default;
        public static readonly EvmUInt256 One = new(0, 0, 0, 1);
        public static readonly EvmUInt256 MaxValue = new(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EvmUInt256(ulong u3, ulong u2, ulong u1, ulong u0)
        {
            _u3 = u3; _u2 = u2; _u1 = u1; _u0 = u0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EvmUInt256(ulong value) : this(0, 0, 0, value) { }

        // Following UInt128 pattern: sign-extend via arithmetic right shift
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EvmUInt256(long value)
        {
            ulong extend = (ulong)(value >> 63); // 0 for positive, 0xFFFFFFFFFFFFFFFF for negative
            _u3 = extend; _u2 = extend; _u1 = extend;
            _u0 = (ulong)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EvmUInt256 FromHeaderScalar(long value) => new EvmUInt256((ulong)value);

        // --- Factory Methods ---

        public static EvmUInt256 FromHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Zero;
            if (hex.StartsWith("0x") || hex.StartsWith("0X"))
                hex = hex.Substring(2);
            if (hex.Length == 0) return Zero;
            if (hex.Length % 2 != 0) hex = "0" + hex;
            var bytes = Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.HexToByteArray(hex);
            return FromBigEndian(bytes);
        }

        // --- Byte Array Conversions (Big-Endian, 32 bytes) ---

        public static EvmUInt256 FromBigEndian(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return Zero;
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            Span<byte> padded = stackalloc byte[32];
            int srcLen = Math.Min(bytes.Length, 32);
            bytes.AsSpan(bytes.Length - srcLen, srcLen).CopyTo(padded.Slice(32 - srcLen));
#else
            var padded = new byte[32];
            int srcLen = Math.Min(bytes.Length, 32);
            Array.Copy(bytes, bytes.Length - srcLen, padded, 32 - srcLen, srcLen);
#endif
            return new EvmUInt256(
                ReadU64BE(padded, 0),
                ReadU64BE(padded, 8),
                ReadU64BE(padded, 16),
                ReadU64BE(padded, 24)
            );
        }

        [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.CoreFoundation, "util-evmuint256-slice", "EvmUInt256.FromBigEndian slice")]
        public static EvmUInt256 FromBigEndian(byte[] source, int offset, int length)
        {
            if (length < 0 || length > 32) throw new ArgumentOutOfRangeException(nameof(length));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (source != null && offset + length <= source.Length)
            {
                int end = offset + length;
                return new EvmUInt256(
                    BigEndianLimbEndingAt(source, offset, end - 24),
                    BigEndianLimbEndingAt(source, offset, end - 16),
                    BigEndianLimbEndingAt(source, offset, end - 8),
                    BigEndianLimbEndingAt(source, offset, end));
            }
            ulong u0 = 0, u1 = 0, u2 = 0, u3 = 0;
            var available = source == null ? 0 : Math.Max(0, Math.Min(length, source.Length - offset));
            for (var i = 0; i < available; i++)
            {
                var place = length - 1 - i;
                var shifted = (ulong)source[offset + i] << ((place & 7) * 8);
                switch (place >> 3)
                {
                    case 0: u0 |= shifted; break;
                    case 1: u1 |= shifted; break;
                    case 2: u2 |= shifted; break;
                    default: u3 |= shifted; break;
                }
            }
            return new EvmUInt256(u3, u2, u1, u0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong BigEndianLimbEndingAt(byte[] source, int lowest, int end)
        {
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            if (end - 8 >= lowest)
                return System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(new ReadOnlySpan<byte>(source, end - 8, 8));
#endif
            ulong limb = 0;
            for (int i = Math.Max(lowest, end - 8); i < end; i++) limb = (limb << 8) | source[i];
            return limb;
        }

        public byte[] ToBigEndian()
        {
            var result = new byte[32];
            WriteU64BE(result, 0, _u3);
            WriteU64BE(result, 8, _u2);
            WriteU64BE(result, 16, _u1);
            WriteU64BE(result, 24, _u0);
            return result;
        }

        public byte[] PadTo32Bytes() => ToBigEndian();

        // --- Byte Array Conversions (Little-Endian, 32 bytes) ---
        // Used by SSZ / zkVM-friendly paths: pure ulong ops, no BigInteger.

        public static EvmUInt256 FromLittleEndian(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return Zero;
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            Span<byte> padded = stackalloc byte[32];
            int srcLen = Math.Min(bytes.Length, 32);
            bytes.AsSpan(0, srcLen).CopyTo(padded);
#else
            var padded = new byte[32];
            int srcLen = Math.Min(bytes.Length, 32);
            Array.Copy(bytes, 0, padded, 0, srcLen);
#endif
            return new EvmUInt256(
                ReadU64LE(padded, 24),
                ReadU64LE(padded, 16),
                ReadU64LE(padded, 8),
                ReadU64LE(padded, 0)
            );
        }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        public static EvmUInt256 FromLittleEndian(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length == 0) return Zero;
            Span<byte> padded = stackalloc byte[32];
            int srcLen = Math.Min(bytes.Length, 32);
            bytes.Slice(0, srcLen).CopyTo(padded);
            return new EvmUInt256(
                ReadU64LE(padded, 24),
                ReadU64LE(padded, 16),
                ReadU64LE(padded, 8),
                ReadU64LE(padded, 0)
            );
        }
#endif

        public byte[] ToLittleEndian()
        {
            var result = new byte[32];
            WriteU64LE(result, 0, _u0);
            WriteU64LE(result, 8, _u1);
            WriteU64LE(result, 16, _u2);
            WriteU64LE(result, 24, _u3);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        private static ulong ReadU64BE(ReadOnlySpan<byte> buf, int off)
#else
        private static ulong ReadU64BE(byte[] buf, int off)
#endif
        {
            return ((ulong)buf[off] << 56) | ((ulong)buf[off + 1] << 48) |
                   ((ulong)buf[off + 2] << 40) | ((ulong)buf[off + 3] << 32) |
                   ((ulong)buf[off + 4] << 24) | ((ulong)buf[off + 5] << 16) |
                   ((ulong)buf[off + 6] << 8) | buf[off + 7];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteU64BE(byte[] buf, int off, ulong val)
        {
            buf[off] = (byte)(val >> 56); buf[off + 1] = (byte)(val >> 48);
            buf[off + 2] = (byte)(val >> 40); buf[off + 3] = (byte)(val >> 32);
            buf[off + 4] = (byte)(val >> 24); buf[off + 5] = (byte)(val >> 16);
            buf[off + 6] = (byte)(val >> 8); buf[off + 7] = (byte)val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        private static ulong ReadU64LE(ReadOnlySpan<byte> buf, int off)
#else
        private static ulong ReadU64LE(byte[] buf, int off)
#endif
        {
            return buf[off] | ((ulong)buf[off + 1] << 8) |
                   ((ulong)buf[off + 2] << 16) | ((ulong)buf[off + 3] << 24) |
                   ((ulong)buf[off + 4] << 32) | ((ulong)buf[off + 5] << 40) |
                   ((ulong)buf[off + 6] << 48) | ((ulong)buf[off + 7] << 56);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteU64LE(byte[] buf, int off, ulong val)
        {
            buf[off] = (byte)val; buf[off + 1] = (byte)(val >> 8);
            buf[off + 2] = (byte)(val >> 16); buf[off + 3] = (byte)(val >> 24);
            buf[off + 4] = (byte)(val >> 32); buf[off + 5] = (byte)(val >> 40);
            buf[off + 6] = (byte)(val >> 48); buf[off + 7] = (byte)(val >> 56);
        }

        // --- Arithmetic (following UInt128 patterns) ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EvmUInt256 operator +(EvmUInt256 a, EvmUInt256 b)
        {
            ulong r0 = a._u0 + b._u0;
            ulong c0 = r0 < a._u0 ? 1UL : 0UL;
            ulong r1 = a._u1 + b._u1 + c0;
            ulong c1 = (r1 < a._u1 || (c0 == 1 && r1 == a._u1)) ? 1UL : 0UL;
            ulong r2 = a._u2 + b._u2 + c1;
            ulong c2 = (r2 < a._u2 || (c1 == 1 && r2 == a._u2)) ? 1UL : 0UL;
            ulong r3 = a._u3 + b._u3 + c2;
            return new EvmUInt256(r3, r2, r1, r0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EvmUInt256 operator -(EvmUInt256 a, EvmUInt256 b)
        {
            ulong r0 = a._u0 - b._u0;
            ulong borrow0 = a._u0 < b._u0 ? 1UL : 0UL;
            ulong r1 = a._u1 - b._u1 - borrow0;
            ulong borrow1 = (a._u1 < b._u1 || (borrow0 == 1 && a._u1 == b._u1)) ? 1UL : 0UL;
            ulong r2 = a._u2 - b._u2 - borrow1;
            ulong borrow2 = (a._u2 < b._u2 || (borrow1 == 1 && a._u2 == b._u2)) ? 1UL : 0UL;
            ulong r3 = a._u3 - b._u3 - borrow2;
            return new EvmUInt256(r3, r2, r1, r0);
        }

        // Following UInt128 pattern: BigMul for low bits, then add cross-terms
        public static EvmUInt256 operator *(EvmUInt256 a, EvmUInt256 b)
        {
            BigMul(a, b, out var lower);
            return lower;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Mul64(ulong a, ulong b, out ulong lo, out ulong hi)
        {
#if NET5_0_OR_GREATER
            hi = Math.BigMul(a, b, out lo);
#else
            ulong aHi = a >> 32, aLo = a & 0xFFFFFFFF;
            ulong bHi = b >> 32, bLo = b & 0xFFFFFFFF;
            ulong p0 = aLo * bLo;
            ulong p1 = aHi * bLo;
            ulong p2 = aLo * bHi;
            ulong p3 = aHi * bHi;
            ulong mid = p1 + (p0 >> 32);
            mid += p2;
            if (mid < p2) p3 += 1UL << 32;
            lo = (mid << 32) | (p0 & 0xFFFFFFFF);
            hi = p3 + (mid >> 32);
#endif
        }

        public static EvmUInt256 operator /(EvmUInt256 a, EvmUInt256 b)
        {
            if (b.IsZero) return Zero;
            Divmod(a, b, out var q, out _);
            return q;
        }

        public static EvmUInt256 operator %(EvmUInt256 a, EvmUInt256 b)
        {
            if (b.IsZero) return Zero;
            Divmod(a, b, out _, out var r);
            return r;
        }

        public static void Divmod(EvmUInt256 a, EvmUInt256 b, out EvmUInt256 quotient, out EvmUInt256 remainder)
        {
            if (b.IsZero) { quotient = Zero; remainder = Zero; return; }
            if (a < b) { quotient = Zero; remainder = a; return; }
            if (a == b) { quotient = One; remainder = Zero; return; }

            if ((a._u1 | a._u2 | a._u3 | b._u1 | b._u2 | b._u3) == 0)
            {
                quotient = new EvmUInt256(a._u0 / b._u0);
                remainder = new EvmUInt256(a._u0 % b._u0);
                return;
            }

            Reduce(a, Zero, b, out quotient, out remainder);
        }

        // --- Modular Arithmetic ---

        public static EvmUInt256 AddMod(EvmUInt256 a, EvmUInt256 b, EvmUInt256 mod)
        {
            if (mod.IsZero) return Zero;
            EvmUInt256 sum = a + b;
            Reduce(sum, sum < a ? One : Zero, mod, out _, out var remainder);
            return remainder;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MulAddCarry(ulong ai, ulong bj, ref ulong acc, ref ulong carry)
        {
            Mul64(ai, bj, out ulong lo, out ulong hi);
            lo += carry;
            carry = hi + (lo < carry ? 1UL : 0UL);
            acc += lo;
            carry += (acc < lo ? 1UL : 0UL);
        }

        public static EvmUInt256 BigMul(EvmUInt256 a, EvmUInt256 b, out EvmUInt256 lower)
        {
            ulong a0 = a._u0, a1 = a._u1, a2 = a._u2, a3 = a._u3;
            ulong b0 = b._u0, b1 = b._u1, b2 = b._u2, b3 = b._u3;
            ulong r0 = 0, r1 = 0, r2 = 0, r3 = 0, r4 = 0, r5 = 0, r6 = 0, r7 = 0, carry;

            carry = 0;
            MulAddCarry(a0, b0, ref r0, ref carry);
            MulAddCarry(a0, b1, ref r1, ref carry);
            MulAddCarry(a0, b2, ref r2, ref carry);
            MulAddCarry(a0, b3, ref r3, ref carry);
            r4 += carry;

            carry = 0;
            MulAddCarry(a1, b0, ref r1, ref carry);
            MulAddCarry(a1, b1, ref r2, ref carry);
            MulAddCarry(a1, b2, ref r3, ref carry);
            MulAddCarry(a1, b3, ref r4, ref carry);
            r5 += carry;

            carry = 0;
            MulAddCarry(a2, b0, ref r2, ref carry);
            MulAddCarry(a2, b1, ref r3, ref carry);
            MulAddCarry(a2, b2, ref r4, ref carry);
            MulAddCarry(a2, b3, ref r5, ref carry);
            r6 += carry;

            carry = 0;
            MulAddCarry(a3, b0, ref r3, ref carry);
            MulAddCarry(a3, b1, ref r4, ref carry);
            MulAddCarry(a3, b2, ref r5, ref carry);
            MulAddCarry(a3, b3, ref r6, ref carry);
            r7 += carry;

            lower = new EvmUInt256(r3, r2, r1, r0);
            return new EvmUInt256(r7, r6, r5, r4);
        }

        public static EvmUInt256 MulMod(EvmUInt256 a, EvmUInt256 b, EvmUInt256 mod)
        {
            if (mod.IsZero) return Zero;
            var upper = BigMul(a, b, out var lower);
            Reduce(lower, upper, mod, out _, out var remainder);
            return remainder;
        }

        public static EvmUInt256 ModPow(EvmUInt256 baseVal, EvmUInt256 exponent, EvmUInt256 modulus)
        {
            if (modulus.IsZero) return Zero;
            if (modulus == One) return Zero;
            EvmUInt256 result = One;
            baseVal = baseVal % modulus;
            while (!exponent.IsZero)
            {
                if ((exponent._u0 & 1) == 1)
                    result = MulMod(result, baseVal, modulus);
                exponent = exponent >> 1;
                baseVal = MulMod(baseVal, baseVal, modulus);
            }
            return result;
        }

        // --- Comparison (following UInt128 pattern: lexicographic) ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(EvmUInt256 a, EvmUInt256 b)
        {
            return (a._u3 < b._u3)
                || (a._u3 == b._u3 && ((a._u2 < b._u2)
                || (a._u2 == b._u2 && ((a._u1 < b._u1)
                || (a._u1 == b._u1 && a._u0 < b._u0)))));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(EvmUInt256 a, EvmUInt256 b) => b < a;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(EvmUInt256 a, EvmUInt256 b) => !(b < a);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(EvmUInt256 a, EvmUInt256 b) => !(a < b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(EvmUInt256 a, EvmUInt256 b)
            => (a._u0 == b._u0) && (a._u1 == b._u1) && (a._u2 == b._u2) && (a._u3 == b._u3);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(EvmUInt256 a, EvmUInt256 b) => !(a == b);

        // --- Bitwise ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EvmUInt256 operator &(EvmUInt256 a, EvmUInt256 b)
            => new(a._u3 & b._u3, a._u2 & b._u2, a._u1 & b._u1, a._u0 & b._u0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EvmUInt256 operator |(EvmUInt256 a, EvmUInt256 b)
            => new(a._u3 | b._u3, a._u2 | b._u2, a._u1 | b._u1, a._u0 | b._u0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EvmUInt256 operator ^(EvmUInt256 a, EvmUInt256 b)
            => new(a._u3 ^ b._u3, a._u2 ^ b._u2, a._u1 ^ b._u1, a._u0 ^ b._u0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EvmUInt256 operator ~(EvmUInt256 a)
            => new(~a._u3, ~a._u2, ~a._u1, ~a._u0);

        // Following UInt128 pattern: no array allocations, direct limb computation
        public static EvmUInt256 operator <<(EvmUInt256 a, int shift)
        {
            shift &= 255;
            if (shift == 0) return a;

            int limbShift = shift >> 6; // shift / 64
            int bitShift = shift & 63;  // shift % 64

            ulong r0 = 0, r1 = 0, r2 = 0, r3 = 0;

            if (bitShift == 0)
            {
                switch (limbShift)
                {
                    case 0: r0 = a._u0; r1 = a._u1; r2 = a._u2; r3 = a._u3; break;
                    case 1: r1 = a._u0; r2 = a._u1; r3 = a._u2; break;
                    case 2: r2 = a._u0; r3 = a._u1; break;
                    case 3: r3 = a._u0; break;
                }
            }
            else
            {
                int rshift = 64 - bitShift;
                switch (limbShift)
                {
                    case 0:
                        r0 = a._u0 << bitShift;
                        r1 = (a._u1 << bitShift) | (a._u0 >> rshift);
                        r2 = (a._u2 << bitShift) | (a._u1 >> rshift);
                        r3 = (a._u3 << bitShift) | (a._u2 >> rshift);
                        break;
                    case 1:
                        r1 = a._u0 << bitShift;
                        r2 = (a._u1 << bitShift) | (a._u0 >> rshift);
                        r3 = (a._u2 << bitShift) | (a._u1 >> rshift);
                        break;
                    case 2:
                        r2 = a._u0 << bitShift;
                        r3 = (a._u1 << bitShift) | (a._u0 >> rshift);
                        break;
                    case 3:
                        r3 = a._u0 << bitShift;
                        break;
                }
            }

            return new EvmUInt256(r3, r2, r1, r0);
        }

        public static EvmUInt256 operator >>(EvmUInt256 a, int shift)
        {
            shift &= 255;
            if (shift == 0) return a;

            int limbShift = shift >> 6;
            int bitShift = shift & 63;

            ulong r0 = 0, r1 = 0, r2 = 0, r3 = 0;

            if (bitShift == 0)
            {
                switch (limbShift)
                {
                    case 0: r0 = a._u0; r1 = a._u1; r2 = a._u2; r3 = a._u3; break;
                    case 1: r0 = a._u1; r1 = a._u2; r2 = a._u3; break;
                    case 2: r0 = a._u2; r1 = a._u3; break;
                    case 3: r0 = a._u3; break;
                }
            }
            else
            {
                int lshift = 64 - bitShift;
                switch (limbShift)
                {
                    case 0:
                        r0 = (a._u0 >> bitShift) | (a._u1 << lshift);
                        r1 = (a._u1 >> bitShift) | (a._u2 << lshift);
                        r2 = (a._u2 >> bitShift) | (a._u3 << lshift);
                        r3 = a._u3 >> bitShift;
                        break;
                    case 1:
                        r0 = (a._u1 >> bitShift) | (a._u2 << lshift);
                        r1 = (a._u2 >> bitShift) | (a._u3 << lshift);
                        r2 = a._u3 >> bitShift;
                        break;
                    case 2:
                        r0 = (a._u2 >> bitShift) | (a._u3 << lshift);
                        r1 = a._u3 >> bitShift;
                        break;
                    case 3:
                        r0 = a._u3 >> bitShift;
                        break;
                }
            }

            return new EvmUInt256(r3, r2, r1, r0);
        }

        // --- Properties ---

        public bool IsZero => _u0 == 0 && _u1 == 0 && _u2 == 0 && _u3 == 0;
        public bool IsOne => _u0 == 1 && _u1 == 0 && _u2 == 0 && _u3 == 0;
        public bool FitsInULong => _u1 == 0 && _u2 == 0 && _u3 == 0;
        public bool FitsInInt => FitsInULong && _u0 <= int.MaxValue;
        public bool IsHighBitSet => (_u3 & (1UL << 63)) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ToInt() => (int)_u0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ToLong() => (long)_u0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ToLongSafe() =>
            (_u1 == 0 && _u2 == 0 && _u3 == 0 && _u0 <= (ulong)long.MaxValue)
                ? (long)_u0
                : long.MaxValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ToULong() => _u0;

        public int BitLength()
        {
            if (_u3 != 0) return 192 + BitLen64(_u3);
            if (_u2 != 0) return 128 + BitLen64(_u2);
            if (_u1 != 0) return 64 + BitLen64(_u1);
            return BitLen64(_u0);
        }

        private static int BitLen64(ulong x)
        {
#if NET6_0_OR_GREATER
            return 64 - System.Numerics.BitOperations.LeadingZeroCount(x);
#else
            int n = 0;
            if (x >= (1UL << 32)) { n += 32; x >>= 32; }
            if (x >= (1UL << 16)) { n += 16; x >>= 16; }
            if (x >= (1UL << 8)) { n += 8; x >>= 8; }
            if (x >= (1UL << 4)) { n += 4; x >>= 4; }
            if (x >= (1UL << 2)) { n += 2; x >>= 2; }
            if (x >= (1UL << 1)) { n += 1; x >>= 1; }
            if (x > 0) n++;
            return n;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetByte(int index)
        {
            if ((uint)index >= 32) return 0;
            int limb = 3 - (index / 8);
            int bytePos = 7 - (index % 8);
            ulong val = limb switch { 0 => _u0, 1 => _u1, 2 => _u2, _ => _u3 };
            return (byte)(val >> (bytePos * 8));
        }

        // --- Signed Helpers (Two's Complement) ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EvmUInt256 Negate() => ~this + One;

        public EvmUInt256 ArithmeticRightShift(int shift)
        {
            if (shift == 0) return this;
            if (!IsHighBitSet) return this >> shift;
            if (shift >= 256) return MaxValue;
            var shifted = this >> shift;
            var mask = MaxValue << (256 - shift);
            return shifted | mask;
        }

        // --- Implicit Conversions (following UInt128 pattern) ---
        // Unsigned: widening, always safe
        public static implicit operator EvmUInt256(ulong value) => new(value);
        // Signed: sign-extend (matches UInt128's explicit operator, but implicit here for EVM convenience)
        public static implicit operator EvmUInt256(int value) => new((long)value);
        public static implicit operator EvmUInt256(long value) => new(value);

        public static EvmUInt256 operator -(EvmUInt256 a) => a.Negate();

        // --- Explicit Conversions (following UInt128 pattern: truncate to lowest limb) ---

        public static explicit operator int(EvmUInt256 value) => (int)value._u0;
        public static explicit operator long(EvmUInt256 value) => (long)value._u0;
        public static explicit operator ulong(EvmUInt256 value) => value._u0;

        // --- Comparison with int ---

        public static bool operator >(EvmUInt256 a, int b) => b >= 0 && a > new EvmUInt256((ulong)b);
        public static bool operator <(EvmUInt256 a, int b) => b >= 0 && a < new EvmUInt256((ulong)b);
        public static bool operator >=(EvmUInt256 a, int b) => b >= 0 && a >= new EvmUInt256((ulong)b);
        public static bool operator <=(EvmUInt256 a, int b) => b >= 0 && a <= new EvmUInt256((ulong)b);
        public static bool operator ==(EvmUInt256 a, int b) => b >= 0 && a == new EvmUInt256((ulong)b);
        public static bool operator !=(EvmUInt256 a, int b) => !(a == b);
        public static bool operator >(int a, EvmUInt256 b) => b < a;
        public static bool operator <(int a, EvmUInt256 b) => b > a;
        public static bool operator >=(int a, EvmUInt256 b) => b <= a;
        public static bool operator <=(int a, EvmUInt256 b) => b >= a;

        // --- Increment/Decrement (UInt128 pattern) ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EvmUInt256 operator ++(EvmUInt256 value) => value + One;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EvmUInt256 operator --(EvmUInt256 value) => value - One;

        // --- Static Utilities (UInt128 pattern) ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EvmUInt256 Min(EvmUInt256 a, EvmUInt256 b) => a < b ? a : b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EvmUInt256 Max(EvmUInt256 a, EvmUInt256 b) => a > b ? a : b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EvmUInt256 Clamp(EvmUInt256 value, EvmUInt256 min, EvmUInt256 max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static void DivRem(EvmUInt256 a, EvmUInt256 b, out EvmUInt256 quotient, out EvmUInt256 remainder)
        {
            Divmod(a, b, out quotient, out remainder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPow2(EvmUInt256 value)
        {
            return !value.IsZero && (value & (value - One)).IsZero;
        }

        public static int Log2(EvmUInt256 value)
        {
            if (value.IsZero) return 0;
            return value.BitLength() - 1;
        }

        public static int LeadingZeroCount(EvmUInt256 value)
        {
            return 256 - value.BitLength();
        }

        public static int TrailingZeroCount(EvmUInt256 value)
        {
            if (value.IsZero) return 256;
            if (value._u0 != 0) return Tzc64(value._u0);
            if (value._u1 != 0) return 64 + Tzc64(value._u1);
            if (value._u2 != 0) return 128 + Tzc64(value._u2);
            return 192 + Tzc64(value._u3);
        }

        private static int Tzc64(ulong x)
        {
#if NET6_0_OR_GREATER
            return System.Numerics.BitOperations.TrailingZeroCount(x);
#else
            if (x == 0) return 64;
            int n = 0;
            if ((x & 0xFFFFFFFF) == 0) { n += 32; x >>= 32; }
            if ((x & 0xFFFF) == 0) { n += 16; x >>= 16; }
            if ((x & 0xFF) == 0) { n += 8; x >>= 8; }
            if ((x & 0xF) == 0) { n += 4; x >>= 4; }
            if ((x & 0x3) == 0) { n += 2; x >>= 2; }
            if ((x & 0x1) == 0) { n += 1; }
            return n;
#endif
        }

        public static int PopCount(EvmUInt256 value)
        {
#if NET6_0_OR_GREATER
            return System.Numerics.BitOperations.PopCount(value._u0)
                 + System.Numerics.BitOperations.PopCount(value._u1)
                 + System.Numerics.BitOperations.PopCount(value._u2)
                 + System.Numerics.BitOperations.PopCount(value._u3);
#else
            return Popcnt64(value._u0) + Popcnt64(value._u1)
                 + Popcnt64(value._u2) + Popcnt64(value._u3);
#endif
        }

#if !NET6_0_OR_GREATER
        private static int Popcnt64(ulong x)
        {
            x -= (x >> 1) & 0x5555555555555555UL;
            x = (x & 0x3333333333333333UL) + ((x >> 2) & 0x3333333333333333UL);
            x = (x + (x >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((x * 0x0101010101010101UL) >> 56);
        }
#endif

        // --- Parse (UInt128 pattern) ---

        public static EvmUInt256 Parse(string s)
        {
            if (string.IsNullOrEmpty(s))
                throw new FormatException("Input string was not in a correct format.");
            if (s.StartsWith("0x") || s.StartsWith("0X"))
                return FromHex(s);
            return ParseDecimal(s);
        }

        public static bool TryParse(string s, out EvmUInt256 result)
        {
            if (string.IsNullOrEmpty(s)) { result = Zero; return false; }
            try
            {
                if (s.StartsWith("0x") || s.StartsWith("0X"))
                    result = FromHex(s);
                else
                    result = ParseDecimal(s);
                return true;
            }
            catch
            {
                result = Zero;
                return false;
            }
        }

        private static EvmUInt256 ParseDecimal(string s)
        {
            var result = Zero;
            var ten = new EvmUInt256(10);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c < '0' || c > '9')
                    throw new FormatException($"Invalid character '{c}' in decimal string.");
                result = result * ten + new EvmUInt256((ulong)(c - '0'));
            }
            return result;
        }

        // --- Conversions: uint/ushort/byte (UInt128 pattern) ---

        public static implicit operator EvmUInt256(uint value) => new(0, 0, 0, value);
        public static implicit operator EvmUInt256(ushort value) => new(0, 0, 0, value);
        public static implicit operator EvmUInt256(byte value) => new(0, 0, 0, value);

        public static explicit operator uint(EvmUInt256 value) => (uint)value._u0;
        public static explicit operator ushort(EvmUInt256 value) => (ushort)value._u0;
        public static explicit operator byte(EvmUInt256 value) => (byte)value._u0;

        // --- Object Overrides ---

        public override bool Equals(object obj) => obj is EvmUInt256 other && this == other;
        public bool Equals(EvmUInt256 other) => this == other;

        public override int GetHashCode()
            => _u0.GetHashCode() ^ _u1.GetHashCode() ^ _u2.GetHashCode() ^ _u3.GetHashCode();

        public int CompareTo(EvmUInt256 other)
        {
            if (this < other) return -1;
            if (this > other) return 1;
            return 0;
        }

        public override string ToString() => ToDecimalString();

        public string ToHexString()
        {
            if (IsZero) return "0x0";
            var bytes = ToBigEndian();
            int start = 0;
            while (start < 31 && bytes[start] == 0) start++;
            var sb = new System.Text.StringBuilder("0x");
            for (int i = start; i < 32; i++)
                sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }

        public string ToDecimalString()
        {
            if (IsZero) return "0";
            if (FitsInULong) return _u0.ToString();

            var digits = new char[78];
            int pos = 77;
            var val = this;
            var ten = new EvmUInt256(10);
            while (!val.IsZero)
            {
                Divmod(val, ten, out var q, out var r);
                digits[pos--] = (char)('0' + (int)r._u0);
                val = q;
            }
            return new string(digits, pos + 1, 77 - pos);
        }
    }
}
