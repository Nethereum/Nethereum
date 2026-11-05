using System;
using System.Runtime.CompilerServices;

namespace Nethereum.Signer.Crypto.BN128.Managed
{
    internal readonly struct Fp : IEquatable<Fp>
    {
        public const int ByteLength = 32;

        private readonly ulong _l0;
        private readonly ulong _l1;
        private readonly ulong _l2;
        private readonly ulong _l3;

        private Fp(ulong l0, ulong l1, ulong l2, ulong l3)
        {
            _l0 = l0;
            _l1 = l1;
            _l2 = l2;
            _l3 = l3;
        }

        public static readonly Fp Zero = default;
        public static readonly Fp One = FromLimbs(1, 0, 0, 0);
        public static readonly Fp Three = FromLimbs(3, 0, 0, 0);
        private static readonly Fp RadixCubed = MontgomeryProduct(
            new Fp(Bn254Constants.RSquared0, Bn254Constants.RSquared1, Bn254Constants.RSquared2, Bn254Constants.RSquared3),
            new Fp(Bn254Constants.RSquared0, Bn254Constants.RSquared1, Bn254Constants.RSquared2, Bn254Constants.RSquared3));
        public static readonly Fp Beta = FromLimbs(Bn254Constants.Beta0, Bn254Constants.Beta1, Bn254Constants.Beta2, Bn254Constants.Beta3);

        public bool IsZero => (_l0 | _l1 | _l2 | _l3) == 0;

        public static bool TryFromBigEndian(byte[] data, int offset, out Fp value)
        {
            value = default;
            var l3 = BigEndianBytes.ReadUInt64(data, offset);
            var l2 = BigEndianBytes.ReadUInt64(data, offset + 8);
            var l1 = BigEndianBytes.ReadUInt64(data, offset + 16);
            var l0 = BigEndianBytes.ReadUInt64(data, offset + 24);
            if (!IsBelowModulus(l0, l1, l2, l3)) return false;
            value = FromLimbs(l0, l1, l2, l3);
            return true;
        }

        public void WriteBigEndian(byte[] destination, int offset)
        {
            var canonical = MontgomeryProduct(this, new Fp(1, 0, 0, 0));
            BigEndianBytes.WriteUInt64(destination, offset, canonical._l3);
            BigEndianBytes.WriteUInt64(destination, offset + 8, canonical._l2);
            BigEndianBytes.WriteUInt64(destination, offset + 16, canonical._l1);
            BigEndianBytes.WriteUInt64(destination, offset + 24, canonical._l0);
        }

        public static Fp operator +(Fp a, Fp b)
        {
            var carry = LimbArithmetic.AddWithCarry(a._l0, b._l0, 0, out var s0);
            carry = LimbArithmetic.AddWithCarry(a._l1, b._l1, carry, out var s1);
            carry = LimbArithmetic.AddWithCarry(a._l2, b._l2, carry, out var s2);
            LimbArithmetic.AddWithCarry(a._l3, b._l3, carry, out var s3);
            return ReduceOnce(s0, s1, s2, s3);
        }

        public static Fp operator -(Fp a, Fp b)
        {
            var borrow = LimbArithmetic.SubtractWithBorrow(a._l0, b._l0, 0, out var d0);
            borrow = LimbArithmetic.SubtractWithBorrow(a._l1, b._l1, borrow, out var d1);
            borrow = LimbArithmetic.SubtractWithBorrow(a._l2, b._l2, borrow, out var d2);
            borrow = LimbArithmetic.SubtractWithBorrow(a._l3, b._l3, borrow, out var d3);
            return borrow == 0 ? new Fp(d0, d1, d2, d3) : AddModulus(d0, d1, d2, d3);
        }

        public static Fp operator *(Fp a, Fp b) => MontgomeryProduct(a, b);

        public static Fp operator -(Fp a) => Zero - a;

        public Fp Square() => MontgomeryProduct(this, this);

        public Fp Double() => this + this;

        public Fp Inverse()
        {
            if (IsZero) return Zero;
            var inverse = SafeGcdInversion.InverseModuloP(new Limbs256(_l0, _l1, _l2, _l3));
            return MontgomeryProduct(new Fp(inverse.L0, inverse.L1, inverse.L2, inverse.L3), RadixCubed);
        }

        public bool Equals(Fp other) =>
            _l0 == other._l0 && _l1 == other._l1 && _l2 == other._l2 && _l3 == other._l3;

        public override bool Equals(object obj) => obj is Fp other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = _l0.GetHashCode();
                hash = hash * 31 + _l1.GetHashCode();
                hash = hash * 31 + _l2.GetHashCode();
                return hash * 31 + _l3.GetHashCode();
            }
        }

        private static Fp FromLimbs(ulong l0, ulong l1, ulong l2, ulong l3) =>
            MontgomeryProduct(
                new Fp(l0, l1, l2, l3),
                new Fp(Bn254Constants.RSquared0, Bn254Constants.RSquared1, Bn254Constants.RSquared2, Bn254Constants.RSquared3));

        private static Fp MontgomeryProduct(Fp a, Fp b)
        {
            ulong t0 = 0, t1 = 0, t2 = 0, t3 = 0;
            MontgomeryRound(a, b._l0, ref t0, ref t1, ref t2, ref t3);
            MontgomeryRound(a, b._l1, ref t0, ref t1, ref t2, ref t3);
            MontgomeryRound(a, b._l2, ref t0, ref t1, ref t2, ref t3);
            MontgomeryRound(a, b._l3, ref t0, ref t1, ref t2, ref t3);
            return ReduceOnce(t0, t1, t2, t3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MontgomeryRound(Fp a, ulong multiplier, ref ulong t0, ref ulong t1, ref ulong t2, ref ulong t3)
        {
            MultiplyAdd(a._l0, multiplier, t0, 0, out t0, out var carry);
            MultiplyAdd(a._l1, multiplier, t1, carry, out t1, out carry);
            MultiplyAdd(a._l2, multiplier, t2, carry, out t2, out carry);
            MultiplyAdd(a._l3, multiplier, t3, carry, out t3, out var top);

            var m = t0 * Bn254Constants.NegatedModulusInverse;
            MultiplyAdd(m, Bn254Constants.Modulus0, t0, 0, out _, out carry);
            MultiplyAdd(m, Bn254Constants.Modulus1, t1, carry, out t0, out carry);
            MultiplyAdd(m, Bn254Constants.Modulus2, t2, carry, out t1, out carry);
            MultiplyAdd(m, Bn254Constants.Modulus3, t3, carry, out t2, out carry);
            t3 = top + carry;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MultiplyAdd(ulong a, ulong b, ulong c, ulong d, out ulong low, out ulong high)
        {
            high = LimbArithmetic.Multiply(a, b, out low);
            low += c;
            high += low < c ? 1UL : 0UL;
            low += d;
            high += low < d ? 1UL : 0UL;
        }

        private static Fp ReduceOnce(ulong l0, ulong l1, ulong l2, ulong l3)
        {
            if (!IsBelowModulus(l0, l1, l2, l3))
            {
                var borrow = LimbArithmetic.SubtractWithBorrow(l0, Bn254Constants.Modulus0, 0, out l0);
                borrow = LimbArithmetic.SubtractWithBorrow(l1, Bn254Constants.Modulus1, borrow, out l1);
                borrow = LimbArithmetic.SubtractWithBorrow(l2, Bn254Constants.Modulus2, borrow, out l2);
                LimbArithmetic.SubtractWithBorrow(l3, Bn254Constants.Modulus3, borrow, out l3);
            }
            return new Fp(l0, l1, l2, l3);
        }

        private static Fp AddModulus(ulong l0, ulong l1, ulong l2, ulong l3)
        {
            var carry = LimbArithmetic.AddWithCarry(l0, Bn254Constants.Modulus0, 0, out l0);
            carry = LimbArithmetic.AddWithCarry(l1, Bn254Constants.Modulus1, carry, out l1);
            carry = LimbArithmetic.AddWithCarry(l2, Bn254Constants.Modulus2, carry, out l2);
            LimbArithmetic.AddWithCarry(l3, Bn254Constants.Modulus3, carry, out l3);
            return new Fp(l0, l1, l2, l3);
        }

        private static bool IsBelowModulus(ulong l0, ulong l1, ulong l2, ulong l3)
        {
            if (l3 != Bn254Constants.Modulus3) return l3 < Bn254Constants.Modulus3;
            if (l2 != Bn254Constants.Modulus2) return l2 < Bn254Constants.Modulus2;
            if (l1 != Bn254Constants.Modulus1) return l1 < Bn254Constants.Modulus1;
            return l0 < Bn254Constants.Modulus0;
        }
    }
}
