using System;
using System.Runtime.CompilerServices;

namespace Nethereum.Util.Poseidon
{
    /// <summary>
    /// BN254 scalar field element in Montgomery form (a*R mod p).
    /// All arithmetic uses Montgomery CIOS — no division, just multiply+shift.
    /// </summary>
    public readonly struct BN254FieldElement
    {
        internal readonly ulong L0, L1, L2, L3;

        // BN254 scalar field prime p
        private const ulong P0 = 0x43E1F593F0000001UL;
        private const ulong P1 = 0x2833E84879B97091UL;
        private const ulong P2 = 0xB85045B68181585DUL;
        private const ulong P3 = 0x30644E72E131A029UL;

        // R^2 mod p — for encoding standard → Montgomery
        private const ulong R2_0 = 0x1BB8E645AE216DA7UL;
        private const ulong R2_1 = 0x53FE3AB1E35C59E3UL;
        private const ulong R2_2 = 0x8C49833D53BB8085UL;
        private const ulong R2_3 = 0x0216D0B17F4E44A5UL;

        // p' = -p^(-1) mod 2^64
        private const ulong INV = 0xC2E1F593EFFFFFFFUL;

        public static readonly BN254FieldElement Zero = default;
        public static readonly BN254FieldElement One = FromStandardLimbs(1, 0, 0, 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal BN254FieldElement(ulong l0, ulong l1, ulong l2, ulong l3)
        {
            L0 = l0; L1 = l1; L2 = l2; L3 = l3;
        }

        // --- Arithmetic (all operate on Montgomery-form values) ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BN254FieldElement Add(BN254FieldElement a, BN254FieldElement b)
        {
            ulong carry = 0;
            ulong r0 = AddCarry(a.L0, b.L0, ref carry);
            ulong r1 = AddCarry(a.L1, b.L1, ref carry);
            ulong r2 = AddCarry(a.L2, b.L2, ref carry);
            ulong r3 = AddCarry(a.L3, b.L3, ref carry);

            return SubtractIfGreaterOrEqual(r0, r1, r2, r3);
        }

        public static BN254FieldElement Multiply(BN254FieldElement a, BN254FieldElement b)
        {
            // Montgomery CIOS: 4 rounds of multiply-accumulate + reduction
            ulong t0 = 0, t1 = 0, t2 = 0, t3 = 0, t4 = 0;

            // Round 0: a.L0 * b
            MulAccRound(a.L0, b.L0, b.L1, b.L2, b.L3,
                ref t0, ref t1, ref t2, ref t3, ref t4);
            ReduceRound(ref t0, ref t1, ref t2, ref t3, ref t4);

            // Round 1: a.L1 * b
            MulAccRound(a.L1, b.L0, b.L1, b.L2, b.L3,
                ref t0, ref t1, ref t2, ref t3, ref t4);
            ReduceRound(ref t0, ref t1, ref t2, ref t3, ref t4);

            // Round 2: a.L2 * b
            MulAccRound(a.L2, b.L0, b.L1, b.L2, b.L3,
                ref t0, ref t1, ref t2, ref t3, ref t4);
            ReduceRound(ref t0, ref t1, ref t2, ref t3, ref t4);

            // Round 3: a.L3 * b
            MulAccRound(a.L3, b.L0, b.L1, b.L2, b.L3,
                ref t0, ref t1, ref t2, ref t3, ref t4);
            ReduceRound(ref t0, ref t1, ref t2, ref t3, ref t4);

            return SubtractIfGreaterOrEqual(t0, t1, t2, t3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BN254FieldElement Pow5(BN254FieldElement x)
        {
            var x2 = Multiply(x, x);
            var x4 = Multiply(x2, x2);
            return Multiply(x4, x);
        }

        // --- Conversion ---

        public static BN254FieldElement FromEvmUInt256(EvmUInt256 v)
            => FromStandardLimbs(v.U0, v.U1, v.U2, v.U3);

        public EvmUInt256 ToEvmUInt256()
        {
            // Multiply by 1 (Montgomery decode): a*R * R^(-1) = a
            var one = new BN254FieldElement(1, 0, 0, 0);
            var decoded = Multiply(this, one);
            return new EvmUInt256(decoded.L3, decoded.L2, decoded.L1, decoded.L0);
        }

        public static BN254FieldElement FromBytes(byte[] data)
        {
            if (data == null || data.Length < 32) return Zero;
            return FromBytes(data, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BN254FieldElement FromBytes(byte[] data, int offset)
        {
            if (data == null || data.Length < offset + 32) return Zero;
            ulong l3 = ReadUInt64BE(data, offset);
            ulong l2 = ReadUInt64BE(data, offset + 8);
            ulong l1 = ReadUInt64BE(data, offset + 16);
            ulong l0 = ReadUInt64BE(data, offset + 24);
            return FromStandardLimbs(l0, l1, l2, l3);
        }

        public byte[] ToBytes()
        {
            var decoded = Multiply(this, new BN254FieldElement(1, 0, 0, 0));
            var result = new byte[32];
            WriteUInt64BE(result, 0, decoded.L3);
            WriteUInt64BE(result, 8, decoded.L2);
            WriteUInt64BE(result, 16, decoded.L1);
            WriteUInt64BE(result, 24, decoded.L0);
            return result;
        }

        // --- Internal helpers ---

        private static BN254FieldElement FromStandardLimbs(ulong l0, ulong l1, ulong l2, ulong l3)
        {
            // Encode: a → a*R mod p by multiplying by R^2
            var a = new BN254FieldElement(l0, l1, l2, l3);
            var r2 = new BN254FieldElement(R2_0, R2_1, R2_2, R2_3);
            return Multiply(a, r2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MulAccRound(
            ulong ai, ulong b0, ulong b1, ulong b2, ulong b3,
            ref ulong t0, ref ulong t1, ref ulong t2, ref ulong t3, ref ulong t4)
        {
            ulong carry = 0;
            ulong lo;

            lo = Mac(t0, ai, b0, ref carry); t0 = lo;
            lo = Mac(t1, ai, b1, ref carry); t1 = lo;
            lo = Mac(t2, ai, b2, ref carry); t2 = lo;
            lo = Mac(t3, ai, b3, ref carry); t3 = lo;
            t4 += carry;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ReduceRound(
            ref ulong t0, ref ulong t1, ref ulong t2, ref ulong t3, ref ulong t4)
        {
            ulong m = unchecked(t0 * INV);
            ulong carry = 0;
            ulong lo;

            lo = Mac(t0, m, P0, ref carry); // result discarded (shifted out)
            lo = Mac(t1, m, P1, ref carry); t0 = lo;
            lo = Mac(t2, m, P2, ref carry); t1 = lo;
            lo = Mac(t3, m, P3, ref carry); t2 = lo;
            t3 = t4 + carry;
            t4 = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static BN254FieldElement SubtractIfGreaterOrEqual(ulong r0, ulong r1, ulong r2, ulong r3)
        {
            // if r >= p, return r - p
            if (r3 > P3 || (r3 == P3 && (r2 > P2 || (r2 == P2 && (r1 > P1 || (r1 == P1 && r0 >= P0))))))
            {
                ulong borrow = 0;
                r0 = SubBorrow(r0, P0, ref borrow);
                r1 = SubBorrow(r1, P1, ref borrow);
                r2 = SubBorrow(r2, P2, ref borrow);
                r3 = SubBorrow(r3, P3, ref borrow);
            }
            return new BN254FieldElement(r0, r1, r2, r3);
        }

        // Multiply-accumulate: (hi, lo) = a + b * c + carry
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Mac(ulong a, ulong b, ulong c, ref ulong carry)
        {
#if NET5_0_OR_GREATER
            ulong hi = Math.BigMul(b, c, out ulong lo);
#else
            Mul128(b, c, out ulong hi, out ulong lo);
#endif
            ulong sum1 = lo + a;
            ulong c1 = sum1 < lo ? 1UL : 0UL;
            ulong sum2 = sum1 + carry;
            ulong c2 = sum2 < sum1 ? 1UL : 0UL;
            carry = hi + c1 + c2;
            return sum2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong AddCarry(ulong a, ulong b, ref ulong carry)
        {
            ulong sum = a + b + carry;
            carry = ((a & b) | ((a | b) & ~sum)) >> 63;
            return sum;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong SubBorrow(ulong a, ulong b, ref ulong borrow)
        {
            ulong diff = a - b - borrow;
            borrow = (a < b + borrow || (borrow > 0 && b == ulong.MaxValue)) ? 1UL : 0UL;
            return diff;
        }

#if !NET5_0_OR_GREATER
        private static void Mul128(ulong a, ulong b, out ulong hi, out ulong lo)
        {
            ulong aHi = a >> 32, aLo = a & 0xFFFFFFFF;
            ulong bHi = b >> 32, bLo = b & 0xFFFFFFFF;
            ulong p0 = aLo * bLo;
            ulong p1 = aHi * bLo;
            ulong p2 = aLo * bHi;
            ulong p3 = aHi * bHi;
            ulong mid = (p0 >> 32) + (p1 & 0xFFFFFFFF) + (p2 & 0xFFFFFFFF);
            lo = (mid << 32) | (p0 & 0xFFFFFFFF);
            hi = p3 + (p1 >> 32) + (p2 >> 32) + (mid >> 32);
        }
#endif

        private static ulong ReadUInt64BE(byte[] data, int offset)
        {
            return ((ulong)data[offset] << 56) | ((ulong)data[offset + 1] << 48) |
                   ((ulong)data[offset + 2] << 40) | ((ulong)data[offset + 3] << 32) |
                   ((ulong)data[offset + 4] << 24) | ((ulong)data[offset + 5] << 16) |
                   ((ulong)data[offset + 6] << 8) | data[offset + 7];
        }

        private static void WriteUInt64BE(byte[] data, int offset, ulong value)
        {
            data[offset] = (byte)(value >> 56); data[offset + 1] = (byte)(value >> 48);
            data[offset + 2] = (byte)(value >> 40); data[offset + 3] = (byte)(value >> 32);
            data[offset + 4] = (byte)(value >> 24); data[offset + 5] = (byte)(value >> 16);
            data[offset + 6] = (byte)(value >> 8); data[offset + 7] = (byte)value;
        }
    }
}
