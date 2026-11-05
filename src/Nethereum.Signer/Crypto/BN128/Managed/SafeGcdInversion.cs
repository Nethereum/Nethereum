namespace Nethereum.Signer.Crypto.BN128.Managed
{
    internal static class SafeGcdInversion
    {
        private const int LimbCount = 5;
        private const int LimbBits = 62;
        private const long Mask62 = (long)(ulong.MaxValue >> 2);

        private static readonly long[] Modulus62 = ToSigned62(new Limbs256(
            Bn254Constants.Modulus0, Bn254Constants.Modulus1, Bn254Constants.Modulus2, Bn254Constants.Modulus3));

        private static readonly ulong ModulusInverse62 = unchecked(0UL - Bn254Constants.NegatedModulusInverse) & (ulong)Mask62;

        private struct Transition
        {
            public long U;
            public long V;
            public long Q;
            public long R;
        }

        public static Limbs256 InverseModuloP(Limbs256 value)
        {
            var d = new long[LimbCount];
            var e = new long[LimbCount];
            e[0] = 1;
            var f = (long[])Modulus62.Clone();
            var g = ToSigned62(value);
            var length = LimbCount;
            var eta = -1L;

            while (true)
            {
                eta = Divsteps62(eta, (ulong)f[0], (ulong)g[0], out var transition);
                UpdateDE(d, e, transition);
                UpdateFG(length, f, g, transition);
                if (IsZero(g, length)) break;
                length = ShrinkLength(length, f, g);
            }

            return FromSigned62(Normalize(d, f[length - 1]));
        }

        private static bool IsZero(long[] g, int length)
        {
            if (g[0] != 0) return false;
            for (var i = 1; i < length; i++)
            {
                if (g[i] != 0) return false;
            }
            return true;
        }

        private static int ShrinkLength(int length, long[] f, long[] g)
        {
            var fTop = f[length - 1];
            var gTop = g[length - 1];
            var onlySignExtension = length > 1 && (fTop ^ (fTop >> 63)) == 0 && (gTop ^ (gTop >> 63)) == 0;
            if (!onlySignExtension) return length;
            f[length - 2] |= (long)((ulong)fTop << LimbBits);
            g[length - 2] |= (long)((ulong)gTop << LimbBits);
            return length - 1;
        }

        private static long Divsteps62(long eta, ulong f0, ulong g0, out Transition transition)
        {
            ulong u = 1, v = 0, q = 0, r = 1;
            ulong f = f0, g = g0;
            var remaining = LimbBits;

            while (true)
            {
                var zeros = TrailingZeros(g | (ulong.MaxValue << remaining));
                g >>= zeros;
                u <<= zeros;
                v <<= zeros;
                eta -= zeros;
                remaining -= zeros;
                if (remaining == 0) break;

                ulong multiple;
                if (eta < 0)
                {
                    eta = -eta;
                    var swap = f; f = g; g = 0UL - swap;
                    swap = u; u = q; q = 0UL - swap;
                    swap = v; v = r; r = 0UL - swap;
                    var mask = CancellableBitsMask(eta, remaining, 63UL);
                    multiple = (f * g * (f * f - 2)) & mask;
                }
                else
                {
                    var mask = CancellableBitsMask(eta, remaining, 15UL);
                    multiple = f + (((f + 1) & 4) << 1);
                    multiple = ((0UL - multiple) * g) & mask;
                }
                g += f * multiple;
                q += u * multiple;
                r += v * multiple;
            }

            transition = new Transition { U = (long)u, V = (long)v, Q = (long)q, R = (long)r };
            return eta;
        }

        private static ulong CancellableBitsMask(long eta, int remaining, ulong widest)
        {
            var limit = (int)eta + 1 > remaining ? remaining : (int)eta + 1;
            return (ulong.MaxValue >> (64 - limit)) & widest;
        }

        private static int TrailingZeros(ulong value)
        {
#if NET5_0_OR_GREATER && !BN254_PORTABLE_MULTIPLY
            return System.Numerics.BitOperations.TrailingZeroCount(value);
#else
            var count = 0;
            while ((value & 1UL) == 0)
            {
                value >>= 1;
                count++;
            }
            return count;
#endif
        }

        private static void UpdateDE(long[] d, long[] e, Transition t)
        {
            var dSign = d[LimbCount - 1] >> 63;
            var eSign = e[LimbCount - 1] >> 63;
            var md = (t.U & dSign) + (t.V & eSign);
            var me = (t.Q & dSign) + (t.R & eSign);

            var cd = new SignedProductAccumulator();
            var ce = new SignedProductAccumulator();
            cd.AddProduct(t.U, d[0]);
            cd.AddProduct(t.V, e[0]);
            ce.AddProduct(t.Q, d[0]);
            ce.AddProduct(t.R, e[0]);

            md = (long)((ulong)md - ((ModulusInverse62 * cd.LowBits + (ulong)md) & (ulong)Mask62));
            me = (long)((ulong)me - ((ModulusInverse62 * ce.LowBits + (ulong)me) & (ulong)Mask62));

            cd.AddProduct(Modulus62[0], md);
            ce.AddProduct(Modulus62[0], me);
            cd.ShiftRight62();
            ce.ShiftRight62();

            for (var i = 1; i < LimbCount; i++)
            {
                var di = d[i];
                var ei = e[i];
                cd.AddProduct(t.U, di);
                cd.AddProduct(t.V, ei);
                cd.AddProduct(Modulus62[i], md);
                ce.AddProduct(t.Q, di);
                ce.AddProduct(t.R, ei);
                ce.AddProduct(Modulus62[i], me);
                d[i - 1] = (long)cd.LowBits & Mask62;
                e[i - 1] = (long)ce.LowBits & Mask62;
                cd.ShiftRight62();
                ce.ShiftRight62();
            }

            d[LimbCount - 1] = cd.LowSigned;
            e[LimbCount - 1] = ce.LowSigned;
        }

        private static void UpdateFG(int length, long[] f, long[] g, Transition t)
        {
            var cf = new SignedProductAccumulator();
            var cg = new SignedProductAccumulator();
            cf.AddProduct(t.U, f[0]);
            cf.AddProduct(t.V, g[0]);
            cg.AddProduct(t.Q, f[0]);
            cg.AddProduct(t.R, g[0]);
            cf.ShiftRight62();
            cg.ShiftRight62();

            for (var i = 1; i < length; i++)
            {
                var fi = f[i];
                var gi = g[i];
                cf.AddProduct(t.U, fi);
                cf.AddProduct(t.V, gi);
                cg.AddProduct(t.Q, fi);
                cg.AddProduct(t.R, gi);
                f[i - 1] = (long)cf.LowBits & Mask62;
                g[i - 1] = (long)cg.LowBits & Mask62;
                cf.ShiftRight62();
                cg.ShiftRight62();
            }

            f[length - 1] = cf.LowSigned;
            g[length - 1] = cg.LowSigned;
        }

        private static long[] Normalize(long[] value, long sign)
        {
            AddModulusIfNegative(value);
            NegateIfNegative(value, sign);
            PropagateCarries(value);
            AddModulusIfNegative(value);
            PropagateCarries(value);
            return value;
        }

        private static void AddModulusIfNegative(long[] value)
        {
            var negative = value[LimbCount - 1] >> 63;
            for (var i = 0; i < LimbCount; i++) value[i] += Modulus62[i] & negative;
        }

        private static void NegateIfNegative(long[] value, long sign)
        {
            var negate = sign >> 63;
            for (var i = 0; i < LimbCount; i++) value[i] = (value[i] ^ negate) - negate;
        }

        private static void PropagateCarries(long[] value)
        {
            for (var i = 0; i < LimbCount - 1; i++)
            {
                value[i + 1] += value[i] >> LimbBits;
                value[i] &= Mask62;
            }
        }

        private static long[] ToSigned62(Limbs256 value)
        {
            return new[]
            {
                (long)(value.L0 & (ulong)Mask62),
                (long)(((value.L0 >> 62) | (value.L1 << 2)) & (ulong)Mask62),
                (long)(((value.L1 >> 60) | (value.L2 << 4)) & (ulong)Mask62),
                (long)(((value.L2 >> 58) | (value.L3 << 6)) & (ulong)Mask62),
                (long)(value.L3 >> 56)
            };
        }

        private static Limbs256 FromSigned62(long[] v)
        {
            return new Limbs256(
                (ulong)v[0] | ((ulong)v[1] << 62),
                ((ulong)v[1] >> 2) | ((ulong)v[2] << 60),
                ((ulong)v[2] >> 4) | ((ulong)v[3] << 58),
                ((ulong)v[3] >> 6) | ((ulong)v[4] << 56));
        }
    }
}
