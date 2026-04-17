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
        private static void Mul64(ulong a, ulong b, out ulong lo, out ulong hi)
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

            if (b._u3 == 0 && b._u2 == 0 && b._u1 == 0 && b._u0 <= 0xFFFFFFFF)
            {
                ulong d = b._u0;
                ulong rem = 0;
                ulong q3 = 0, q2 = 0, q1 = 0, q0 = 0;

                if (a._u3 != 0) { q3 = a._u3 / d; rem = a._u3 % d; }
                ulong n2 = (rem << 32) | (a._u2 >> 32);
                ulong qh2 = n2 / d; rem = n2 % d;
                ulong n2b = (rem << 32) | (a._u2 & 0xFFFFFFFF);
                ulong ql2 = n2b / d; rem = n2b % d;
                q2 = (qh2 << 32) | ql2;

                ulong n1 = (rem << 32) | (a._u1 >> 32);
                ulong qh1 = n1 / d; rem = n1 % d;
                ulong n1b = (rem << 32) | (a._u1 & 0xFFFFFFFF);
                ulong ql1 = n1b / d; rem = n1b % d;
                q1 = (qh1 << 32) | ql1;

                ulong n0 = (rem << 32) | (a._u0 >> 32);
                ulong qh0 = n0 / d; rem = n0 % d;
                ulong n0b = (rem << 32) | (a._u0 & 0xFFFFFFFF);
                ulong ql0 = n0b / d; rem = n0b % d;
                q0 = (qh0 << 32) | ql0;

                quotient = new EvmUInt256(q3, q2, q1, q0);
                remainder = new EvmUInt256(rem);
                return;
            }

            // Knuth's Algorithm D for multi-limb division using 32-bit digits
            DivmodKnuth(a, b, out quotient, out remainder);
        }

        // Knuth Algorithm D — multi-limb division using base 2^32 digits
        private static void DivmodKnuth(EvmUInt256 a, EvmUInt256 b, out EvmUInt256 quotient, out EvmUInt256 remainder)
        {
            // Convert to base-2^32 digits (little-endian: index 0 = least significant)
            uint[] u = ToUint32LE(a); // 8 digits
            uint[] v = ToUint32LE(b); // 8 digits

            // Find actual lengths (number of significant digits)
            int n = 8; while (n > 1 && v[n - 1] == 0) n--;
            int m = 8; while (m > 1 && u[m - 1] == 0) m--;

            if (m < n) { quotient = Zero; remainder = a; return; }

            // n == 1: single-digit divisor (already handled above, but safety)
            if (n == 1)
            {
                ulong rem1 = 0;
                var qd = new uint[8];
                for (int j = m - 1; j >= 0; j--)
                {
                    ulong cur = (rem1 << 32) | u[j];
                    qd[j] = (uint)(cur / v[0]);
                    rem1 = cur % v[0];
                }
                quotient = FromUint32LE(qd);
                remainder = new EvmUInt256(0, 0, 0, rem1);
                return;
            }

            // D1: Normalize — shift so high bit of v[n-1] is set
            int shift = Clz32(v[n - 1]);
            var un = new uint[m + 1]; // m+1 digits for shifted dividend
            var vn = new uint[n];

            if (shift > 0)
            {
                for (int i = n - 1; i > 0; i--)
                    vn[i] = (v[i] << shift) | (v[i - 1] >> (32 - shift));
                vn[0] = v[0] << shift;

                un[m] = u[m - 1] >> (32 - shift);
                for (int i = m - 1; i > 0; i--)
                    un[i] = (u[i] << shift) | (u[i - 1] >> (32 - shift));
                un[0] = u[0] << shift;
            }
            else
            {
                Array.Copy(v, vn, n);
                Array.Copy(u, un, m);
                un[m] = 0;
            }

            var q = new uint[8];

            // D2-D7: Main loop
            for (int j = m - n; j >= 0; j--)
            {
                // D3: Compute trial quotient qhat
                ulong uHi = ((ulong)un[j + n] << 32) | un[j + n - 1];
                ulong qhat = uHi / vn[n - 1];
                ulong rhat = uHi % vn[n - 1];

                // Refine qhat
                while (qhat > 0xFFFFFFFF || (n >= 2 && qhat * vn[n - 2] > ((rhat << 32) | un[j + n - 2])))
                {
                    qhat--;
                    rhat += vn[n - 1];
                    if (rhat > 0xFFFFFFFF) break;
                }

                // D4: Multiply and subtract
                long borrow = 0;
                for (int i = 0; i < n; i++)
                {
                    ulong p = qhat * vn[i];
                    long diff = (long)un[j + i] - (long)(uint)p - borrow;
                    un[j + i] = (uint)diff;
                    borrow = (long)(p >> 32) - (diff >> 32);
                }
                long finalBorrow = (long)un[j + n] - borrow;
                un[j + n] = (uint)finalBorrow;

                q[j] = (uint)qhat;

                // D6: Add back if we subtracted too much
                if (finalBorrow < 0)
                {
                    q[j]--;
                    ulong carry = 0;
                    for (int i = 0; i < n; i++)
                    {
                        carry += (ulong)un[j + i] + vn[i];
                        un[j + i] = (uint)carry;
                        carry >>= 32;
                    }
                    un[j + n] += (uint)carry;
                }
            }

            quotient = FromUint32LE(q);

            // D8: Unnormalize remainder
            if (shift > 0)
            {
                var r = new uint[8];
                for (int i = 0; i < n - 1; i++)
                    r[i] = (un[i] >> shift) | (un[i + 1] << (32 - shift));
                r[n - 1] = un[n - 1] >> shift;
                remainder = FromUint32LE(r);
            }
            else
            {
                var r = new uint[8];
                Array.Copy(un, r, Math.Min(n, 8));
                remainder = FromUint32LE(r);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Clz32(uint x)
        {
            if (x == 0) return 32;
#if NET6_0_OR_GREATER
            return System.Numerics.BitOperations.LeadingZeroCount(x);
#else
            int n = 0;
            if (x <= 0x0000FFFF) { n += 16; x <<= 16; }
            if (x <= 0x00FFFFFF) { n += 8; x <<= 8; }
            if (x <= 0x0FFFFFFF) { n += 4; x <<= 4; }
            if (x <= 0x3FFFFFFF) { n += 2; x <<= 2; }
            if (x <= 0x7FFFFFFF) { n += 1; }
            return n;
#endif
        }

        private static uint[] ToUint32LE(EvmUInt256 v)
        {
            return new uint[]
            {
                (uint)v._u0, (uint)(v._u0 >> 32),
                (uint)v._u1, (uint)(v._u1 >> 32),
                (uint)v._u2, (uint)(v._u2 >> 32),
                (uint)v._u3, (uint)(v._u3 >> 32),
            };
        }

        private static EvmUInt256 FromUint32LE(uint[] d)
        {
            return new EvmUInt256(
                (ulong)d[7] << 32 | d[6],
                (ulong)d[5] << 32 | d[4],
                (ulong)d[3] << 32 | d[2],
                (ulong)d[1] << 32 | d[0]
            );
        }

        // --- Modular Arithmetic ---

        public static EvmUInt256 AddMod(EvmUInt256 a, EvmUInt256 b, EvmUInt256 mod)
        {
            if (mod.IsZero) return Zero;
            EvmUInt256 sum = a + b;
            bool carry = sum < a;

            if (!carry)
                return sum % mod;

            EvmUInt256 pow256Mod = (MaxValue % mod + One) % mod;
            EvmUInt256 sumMod = sum % mod;
            EvmUInt256 result = sumMod + pow256Mod;
            bool carry2 = result < sumMod;
            if (!carry2)
                return result >= mod ? result % mod : result;
            EvmUInt256 r2 = result % mod;
            return (r2 + pow256Mod) % mod;
        }

        public static EvmUInt256 BigMul(EvmUInt256 a, EvmUInt256 b, out EvmUInt256 lower)
        {
            ulong[] al = { a._u0, a._u1, a._u2, a._u3 };
            ulong[] bl = { b._u0, b._u1, b._u2, b._u3 };
            ulong[] r = new ulong[8];

            for (int i = 0; i < 4; i++)
            {
                ulong carry = 0;
                for (int j = 0; j < 4; j++)
                {
                    Mul64(al[i], bl[j], out ulong lo, out ulong hi);
                    lo += carry;
                    carry = hi + (lo < carry ? 1UL : 0UL);
                    r[i + j] += lo;
                    carry += (r[i + j] < lo ? 1UL : 0UL);
                }
                r[i + 4] += carry;
            }

            lower = new EvmUInt256(r[3], r[2], r[1], r[0]);
            return new EvmUInt256(r[7], r[6], r[5], r[4]);
        }

        public static EvmUInt256 MulMod(EvmUInt256 a, EvmUInt256 b, EvmUInt256 mod)
        {
            if (mod.IsZero) return Zero;
            var upper = BigMul(a, b, out var lower512);

            if (upper.IsZero)
                return lower512 % mod;

            return Mod512(upper, lower512, mod);
        }

        private static EvmUInt256 Mod512(EvmUInt256 upper, EvmUInt256 lower, EvmUInt256 mod)
        {
            EvmUInt256 pow256Mod = (MaxValue % mod + One) % mod;

            EvmUInt256 upperReduced = Zero;
            EvmUInt256 factor = pow256Mod;
            EvmUInt256 u = upper;
            while (!u.IsZero)
            {
                if ((u._u0 & 1) == 1)
                    upperReduced = AddMod(upperReduced, factor, mod);
                factor = AddMod(factor, factor, mod);
                u = u >> 1;
            }

            EvmUInt256 lowerReduced = lower % mod;
            return AddMod(upperReduced, lowerReduced, mod);
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
