using System.Runtime.CompilerServices;

namespace Nethereum.Util.Poseidon
{
    public readonly struct GoldilocksField : System.IEquatable<GoldilocksField>
    {
        public const ulong P = 0xFFFFFFFF00000001;
        private const ulong NEG_ORDER = 0xFFFFFFFF;

        public readonly ulong Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GoldilocksField(ulong value) => Value = value;

        public static readonly GoldilocksField Zero = new GoldilocksField(0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GoldilocksField Add(GoldilocksField a, GoldilocksField b)
        {
            ulong sum = a.Value + b.Value;
            bool over = sum < a.Value;
            if (over)
            {
                sum += NEG_ORDER;
                if (sum < NEG_ORDER) sum += NEG_ORDER;
            }
            if (sum >= P) sum -= P;
            return new GoldilocksField(sum);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GoldilocksField Sub(GoldilocksField a, GoldilocksField b)
        {
            if (a.Value >= b.Value)
                return new GoldilocksField(a.Value - b.Value);
            return new GoldilocksField(a.Value + P - b.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GoldilocksField Mul(GoldilocksField a, GoldilocksField b)
        {
            EvmUInt256.Mul64(a.Value, b.Value, out ulong lo, out ulong hi);
            return Reduce128(lo, hi);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GoldilocksField Pow7(GoldilocksField x)
        {
            var x2 = Mul(x, x);
            var x4 = Mul(x2, x2);
            var x6 = Mul(x4, x2);
            return Mul(x6, x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static GoldilocksField Reduce128(ulong lo, ulong hi)
        {
            ulong hi_hi = hi >> 32;
            ulong hi_lo = hi & 0xFFFFFFFF;

            ulong t0 = lo - hi_hi;
            bool under0 = lo < hi_hi;
            if (under0)
            {
                t0 -= NEG_ORDER;
            }

            ulong t1 = hi_lo * NEG_ORDER;
            ulong result = t0 + t1;
            bool over1 = result < t0;
            if (over1)
            {
                result += NEG_ORDER;
                if (result < NEG_ORDER) result += NEG_ORDER;
            }
            if (result >= P) result -= P;
            return new GoldilocksField(result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GoldilocksField operator +(GoldilocksField a, GoldilocksField b) => Add(a, b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GoldilocksField operator -(GoldilocksField a, GoldilocksField b) => Sub(a, b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GoldilocksField operator *(GoldilocksField a, GoldilocksField b) => Mul(a, b);

        public static bool operator ==(GoldilocksField a, GoldilocksField b) => a.Value == b.Value;
        public static bool operator !=(GoldilocksField a, GoldilocksField b) => a.Value != b.Value;
        public bool Equals(GoldilocksField other) => Value == other.Value;
        public override bool Equals(object obj) => obj is GoldilocksField g && g.Value == Value;
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
    }
}
