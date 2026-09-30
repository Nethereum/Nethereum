using System;
using Org.BouncyCastle.Math;

namespace Nethereum.Signer.Crypto.BN128
{
    public readonly struct Fp : IEquatable<Fp>
    {
        internal readonly ulong L0;
        internal readonly ulong L1;
        internal readonly ulong L2;
        internal readonly ulong L3;

        private Fp(ulong l0, ulong l1, ulong l2, ulong l3)
        {
            L0 = l0;
            L1 = l1;
            L2 = l2;
            L3 = l3;
        }

        private static readonly ulong M0;
        private static readonly ulong M1;
        private static readonly ulong M2;
        private static readonly ulong M3;
        private static readonly ulong ModulusInverseWord;
        private static readonly ulong R20;
        private static readonly ulong R21;
        private static readonly ulong R22;
        private static readonly ulong R23;

        public static readonly Fp Zero;
        public static readonly Fp One;

        static Fp()
        {
            var modulus = BN128Constants.P;
            var be = LeftPad32(modulus.ToByteArrayUnsigned());
            M0 = ReadU64BigEndian(be, 24);
            M1 = ReadU64BigEndian(be, 16);
            M2 = ReadU64BigEndian(be, 8);
            M3 = ReadU64BigEndian(be, 0);

            var inv = 1UL;
            for (var i = 0; i < 6; i++)
                inv = unchecked(inv * (2UL - M0 * inv));
            ModulusInverseWord = unchecked(0UL - inv);
            if (unchecked(M0 * (0UL - ModulusInverseWord)) != 1UL)
                throw new InvalidOperationException("Montgomery inverse derivation failed");

            var r = BigInteger.One.ShiftLeft(256).Mod(modulus);
            var rSquared = r.Multiply(r).Mod(modulus);
            var r2 = LeftPad32(rSquared.ToByteArrayUnsigned());
            R20 = ReadU64BigEndian(r2, 24);
            R21 = ReadU64BigEndian(r2, 16);
            R22 = ReadU64BigEndian(r2, 8);
            R23 = ReadU64BigEndian(r2, 0);

            Zero = new Fp(0, 0, 0, 0);
            MontgomeryMultiply(1, 0, 0, 0, R20, R21, R22, R23,
                out var o0, out var o1, out var o2, out var o3);
            One = new Fp(o0, o1, o2, o3);
        }

        public static Fp FromBigInteger(BigInteger value)
        {
            var reduced = value.Mod(BN128Constants.P);
            var be = LeftPad32(reduced.ToByteArrayUnsigned());
            var a0 = ReadU64BigEndian(be, 24);
            var a1 = ReadU64BigEndian(be, 16);
            var a2 = ReadU64BigEndian(be, 8);
            var a3 = ReadU64BigEndian(be, 0);
            MontgomeryMultiply(a0, a1, a2, a3, R20, R21, R22, R23,
                out var m0, out var m1, out var m2, out var m3);
            return new Fp(m0, m1, m2, m3);
        }

        public static Fp FromBytesBigEndian(byte[] bytes) => FromBigInteger(new BigInteger(1, bytes));

        public BigInteger ToBigInteger()
        {
            MontgomeryMultiply(L0, L1, L2, L3, 1, 0, 0, 0,
                out var a0, out var a1, out var a2, out var a3);
            var be = new byte[32];
            WriteU64BigEndian(be, 0, a3);
            WriteU64BigEndian(be, 8, a2);
            WriteU64BigEndian(be, 16, a1);
            WriteU64BigEndian(be, 24, a0);
            return new BigInteger(1, be);
        }

        public byte[] ToBytes32BigEndian()
        {
            var value = ToBigInteger().ToByteArrayUnsigned();
            return LeftPad32(value);
        }

        public bool IsZero => (L0 | L1 | L2 | L3) == 0;

        public Fp Add(Fp other)
        {
            AddLimbs(L0, L1, L2, L3, other.L0, other.L1, other.L2, other.L3,
                out var s0, out var s1, out var s2, out var s3, out var carry);
            if (carry != 0 || GreaterOrEqualModulus(s0, s1, s2, s3))
                SubtractModulus(ref s0, ref s1, ref s2, ref s3);
            return new Fp(s0, s1, s2, s3);
        }

        public Fp Sub(Fp other)
        {
            SubLimbs(L0, L1, L2, L3, other.L0, other.L1, other.L2, other.L3,
                out var d0, out var d1, out var d2, out var d3, out var borrow);
            if (borrow != 0)
                AddModulus(ref d0, ref d1, ref d2, ref d3);
            return new Fp(d0, d1, d2, d3);
        }

        public Fp Neg() => IsZero ? this : Zero.Sub(this);

        public Fp Mul(Fp other)
        {
            MontgomeryMultiply(L0, L1, L2, L3, other.L0, other.L1, other.L2, other.L3,
                out var r0, out var r1, out var r2, out var r3);
            return new Fp(r0, r1, r2, r3);
        }

        public Fp Sqr() => Mul(this);

        public Fp Inv()
        {
            var exponent = BN128Constants.P.Subtract(BigInteger.Two);
            var result = One;
            var baseValue = this;
            for (var bit = 0; bit < exponent.BitLength; bit++)
            {
                if (exponent.TestBit(bit))
                    result = result.Mul(baseValue);
                baseValue = baseValue.Sqr();
            }
            return result;
        }

        public bool Equals(Fp other) => L0 == other.L0 && L1 == other.L1 && L2 == other.L2 && L3 == other.L3;

        public override bool Equals(object obj) => obj is Fp other && Equals(other);

        public override int GetHashCode() => unchecked((int)(L0 ^ L1 ^ L2 ^ L3));

        private static void MontgomeryMultiply(
            ulong a0, ulong a1, ulong a2, ulong a3,
            ulong b0, ulong b1, ulong b2, ulong b3,
            out ulong r0, out ulong r1, out ulong r2, out ulong r3)
        {
            ulong t0 = 0, t1 = 0, t2 = 0, t3 = 0, t4 = 0, t5 = 0;
            MontgomeryStep(a0, a1, a2, a3, b0, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
            MontgomeryStep(a0, a1, a2, a3, b1, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
            MontgomeryStep(a0, a1, a2, a3, b2, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);
            MontgomeryStep(a0, a1, a2, a3, b3, ref t0, ref t1, ref t2, ref t3, ref t4, ref t5);

            r0 = t0;
            r1 = t1;
            r2 = t2;
            r3 = t3;
            if (GreaterOrEqualModulus(r0, r1, r2, r3))
                SubtractModulus(ref r0, ref r1, ref r2, ref r3);
        }

        private static void MontgomeryStep(
            ulong a0, ulong a1, ulong a2, ulong a3, ulong b,
            ref ulong t0, ref ulong t1, ref ulong t2, ref ulong t3, ref ulong t4, ref ulong t5)
        {
            MulAddAdd(a0, b, t0, 0, out var carry, out t0);
            MulAddAdd(a1, b, t1, carry, out carry, out t1);
            MulAddAdd(a2, b, t2, carry, out carry, out t2);
            MulAddAdd(a3, b, t3, carry, out carry, out t3);
            AddCarry(t4, carry, 0, out var overflow, out t4);
            t5 = overflow;

            var reducer = unchecked(t0 * ModulusInverseWord);
            MulAddAdd(reducer, M0, t0, 0, out carry, out _);
            MulAddAdd(reducer, M1, t1, carry, out carry, out t0);
            MulAddAdd(reducer, M2, t2, carry, out carry, out t1);
            MulAddAdd(reducer, M3, t3, carry, out carry, out t2);
            AddCarry(t4, carry, 0, out var overflow2, out t3);
            t4 = t5 + overflow2;
        }

        private static void MulAddAdd(ulong a, ulong b, ulong c, ulong d, out ulong hi, out ulong lo)
        {
            Mul64(a, b, out hi, out lo);
            var sum = lo + c;
            if (sum < lo) hi++;
            lo = sum;
            sum = lo + d;
            if (sum < lo) hi++;
            lo = sum;
        }

        private static void Mul64(ulong a, ulong b, out ulong hi, out ulong lo)
        {
#if NET5_0_OR_GREATER
            hi = Math.BigMul(a, b, out lo);
#else
            ulong aLow = (uint)a;
            ulong aHigh = a >> 32;
            ulong bLow = (uint)b;
            ulong bHigh = b >> 32;
            var ll = aLow * bLow;
            var lh = aLow * bHigh;
            var hl = aHigh * bLow;
            var hh = aHigh * bHigh;
            var cross = (ll >> 32) + (uint)lh + (uint)hl;
            lo = (ll & 0xFFFFFFFFUL) | (cross << 32);
            hi = hh + (lh >> 32) + (hl >> 32) + (cross >> 32);
#endif
        }

        private static void AddCarry(ulong a, ulong b, ulong carryIn, out ulong carryOut, out ulong sum)
        {
            var s = a + b;
            var c = s < a ? 1UL : 0UL;
            s += carryIn;
            if (s < carryIn) c++;
            sum = s;
            carryOut = c;
        }

        private static void AddLimbs(
            ulong a0, ulong a1, ulong a2, ulong a3,
            ulong b0, ulong b1, ulong b2, ulong b3,
            out ulong s0, out ulong s1, out ulong s2, out ulong s3, out ulong carry)
        {
            AddCarry(a0, b0, 0, out var c0, out s0);
            AddCarry(a1, b1, c0, out var c1, out s1);
            AddCarry(a2, b2, c1, out var c2, out s2);
            AddCarry(a3, b3, c2, out carry, out s3);
        }

        private static void SubLimbs(
            ulong a0, ulong a1, ulong a2, ulong a3,
            ulong b0, ulong b1, ulong b2, ulong b3,
            out ulong d0, out ulong d1, out ulong d2, out ulong d3, out ulong borrow)
        {
            SubBorrow(a0, b0, 0, out var br0, out d0);
            SubBorrow(a1, b1, br0, out var br1, out d1);
            SubBorrow(a2, b2, br1, out var br2, out d2);
            SubBorrow(a3, b3, br2, out borrow, out d3);
        }

        private static void SubBorrow(ulong a, ulong b, ulong borrowIn, out ulong borrowOut, out ulong diff)
        {
            var d = a - b;
            var br = a < b ? 1UL : 0UL;
            var d2 = d - borrowIn;
            if (d < borrowIn) br++;
            diff = d2;
            borrowOut = br;
        }

        private static bool GreaterOrEqualModulus(ulong l0, ulong l1, ulong l2, ulong l3)
        {
            if (l3 != M3) return l3 > M3;
            if (l2 != M2) return l2 > M2;
            if (l1 != M1) return l1 > M1;
            return l0 >= M0;
        }

        private static void SubtractModulus(ref ulong l0, ref ulong l1, ref ulong l2, ref ulong l3)
        {
            SubLimbs(l0, l1, l2, l3, M0, M1, M2, M3, out l0, out l1, out l2, out l3, out _);
        }

        private static void AddModulus(ref ulong l0, ref ulong l1, ref ulong l2, ref ulong l3)
        {
            AddLimbs(l0, l1, l2, l3, M0, M1, M2, M3, out l0, out l1, out l2, out l3, out _);
        }

        private static byte[] LeftPad32(byte[] value)
        {
            if (value.Length == 32) return value;
            var padded = new byte[32];
            Array.Copy(value, 0, padded, 32 - value.Length, value.Length);
            return padded;
        }

        private static ulong ReadU64BigEndian(byte[] bytes, int offset)
        {
            ulong value = 0;
            for (var i = 0; i < 8; i++)
                value = (value << 8) | bytes[offset + i];
            return value;
        }

        private static void WriteU64BigEndian(byte[] bytes, int offset, ulong value)
        {
            for (var i = 7; i >= 0; i--)
            {
                bytes[offset + i] = (byte)(value & 0xFF);
                value >>= 8;
            }
        }
    }
}
