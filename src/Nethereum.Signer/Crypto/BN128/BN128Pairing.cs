using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;

namespace Nethereum.Signer.Crypto.BN128
{
    internal class LineFunctionResult
    {
        public Fp2 A { get; set; }
        public Fp2 B { get; set; }
        public Fp2 C { get; set; }
        public TwistPoint ROut { get; set; }
    }

    public static class BN128Pairing
    {
        /// <summary>
        /// Computes the optimal Ate pairing e(G1, G2) -> GT.
        /// Returns the pairing result in Fp12.
        /// </summary>
        public static Fp12 Pair(ECPoint g1, TwistPoint g2)
        {
            var e = Miller(g2, g1);
            var ret = FinalExponentiation(e);
            return ret;
        }

        /// <summary>
        /// Checks if the pairing product equals one.
        /// Computes ∏ e(a[i], b[i]) and returns true if result is 1 in GT.
        /// </summary>
        public static bool PairingCheck(ECPoint[] a, TwistPoint[] b)
        {
            var acc = new Fp12();
            acc.SetOne();

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].IsInfinity || b[i].IsInfinity())
                {
                    continue;
                }
                var ml = Miller(b[i], a[i]);
                acc.Mul(acc, ml);
            }

            var ret = FinalExponentiation(acc);
            return ret.IsOne();
        }

        /// <summary>
        /// Computes the Miller loop for the optimal Ate pairing.
        /// </summary>
        private static Fp12 Miller(TwistPoint q, ECPoint p)
        {
            var ret = new Fp12();
            ret.SetOne();

            var aAffine = MakeAffine(q);
            var bAffine = p.Normalize();

            if (aAffine.IsInfinity() || bAffine.IsInfinity)
            {
                return ret;
            }

            // Precompute minusA
            var minusA = new TwistPoint().Neg(aAffine);

            var r = aAffine.Copy();

            var qX = new Fp2(BigInteger.Zero, bAffine.AffineXCoord.ToBigInteger());
            var qY = new Fp2(BigInteger.Zero, bAffine.AffineYCoord.ToBigInteger());

            // Precompute aAffine.Y² for LineFunctionAdd
            var r2 = new Fp2().Square(aAffine.Y);

            // Main Miller loop using NAF representation of 6u+2
            // go-ethereum: for i := len(sixuPlus2NAF) - 1; i > 0; i-- and accesses [i-1]
            for (int i = BN128Constants.SixUPlus2NAF.Length - 1; i > 0; i--)
            {
                var lfr = LineFunctionDouble(r, qX, qY);

                // Square AFTER lineFunctionDouble, except for first iteration
                if (i != BN128Constants.SixUPlus2NAF.Length - 1)
                {
                    ret.Square(ret);
                }

                MulLine(ret, lfr.A, lfr.B, lfr.C);
                r = lfr.ROut;

                var naf = BN128Constants.SixUPlus2NAF[i - 1];
                if (naf == 1)
                {
                    var lfr1 = LineFunctionAdd(r, aAffine, qX, qY, r2);
                    MulLine(ret, lfr1.A, lfr1.B, lfr1.C);
                    r = lfr1.ROut;
                }
                else if (naf == -1)
                {
                    // (-y)² = y², so use same r2
                    var lfr1 = LineFunctionAdd(r, minusA, qX, qY, r2);
                    MulLine(ret, lfr1.A, lfr1.B, lfr1.C);
                    r = lfr1.ROut;
                }
            }

            // Two additional iterations for optimal Ate
            // Q1 = Frobenius(aAffine)
            var q1 = new TwistPoint();
            q1.X.Conjugate(aAffine.X);
            q1.X.Mul(q1.X, BN128Constants.XiToPMinus1Over3);
            q1.Y.Conjugate(aAffine.Y);
            q1.Y.Mul(q1.Y, BN128Constants.XiToPMinus1Over2);
            q1.Z.SetOne();
            q1.T.SetOne();

            // minusQ2: The p² Frobenius gives a factor of -1 for y, but we absorb it
            // by calling this point minusQ2 (see Geth comments)
            var minusQ2 = new TwistPoint();
            minusQ2.X.MulScalar(aAffine.X, BN128Constants.XiToPSquaredMinus1Over3);
            minusQ2.Y.Set(aAffine.Y);  // NOT negated - the -1 is absorbed in the name
            minusQ2.Z.SetOne();
            minusQ2.T.SetOne();

            r2.Square(q1.Y);
            var lfr2 = LineFunctionAdd(r, q1, qX, qY, r2);
            MulLine(ret, lfr2.A, lfr2.B, lfr2.C);
            r = lfr2.ROut;

            r2.Square(minusQ2.Y);
            var lfr3 = LineFunctionAdd(r, minusQ2, qX, qY, r2);
            MulLine(ret, lfr3.A, lfr3.B, lfr3.C);

            return ret;
        }

        private static LineFunctionResult LineFunctionDouble(TwistPoint r, Fp2 qX, Fp2 qY)
        {
            // Following go-ethereum/crypto/bn256/google/optate.go exactly
            // A = r.x²
            var A = new Fp2().Square(r.X);
            // B = r.y²
            var B = new Fp2().Square(r.Y);
            // C = B²
            var C = new Fp2().Square(B);

            // D = 2 * ((r.x + B)² - A - C)
            var D = new Fp2().Add(r.X, B);
            D.Square(D);
            D.Sub(D, A);
            D.Sub(D, C);
            D.Add(D, D);

            // E = 3 * A
            var E = new Fp2().Add(A, A);
            E.Add(E, A);

            // G = E²
            var G = new Fp2().Square(E);

            // rOut.x = G - 2*D
            var rOutX = new Fp2().Sub(G, D);
            rOutX.Sub(rOutX, D);

            // rOut.z = (r.y + r.z)² - B - r.t
            var rOutZ = new Fp2().Add(r.Y, r.Z);
            rOutZ.Square(rOutZ);
            rOutZ.Sub(rOutZ, B);
            rOutZ.Sub(rOutZ, r.T);

            // rOut.y = (D - rOut.x) * E - 8*C
            var rOutY = new Fp2().Sub(D, rOutX);
            rOutY.Mul(rOutY, E);
            var t = new Fp2().Add(C, C);
            t.Add(t, t);
            t.Add(t, t);
            rOutY.Sub(rOutY, t);

            // rOut.t = rOut.z²
            var rOutT = new Fp2().Square(rOutZ);

            var rOut = new TwistPoint(rOutX, rOutY, rOutZ, rOutT);

            // Line coefficients (following Geth exactly)
            // t = 2 * E * r.t
            t.Mul(E, r.T);
            t.Add(t, t);
            // b = -t * q.x = -2 * E * r.t * q.x
            var b = new Fp2().Neg(t);
            b.MulScalar(b, qX.B);  // qX.B is the real part (the scalar value)

            // a = (r.x + E)² - A - G - 4*B
            var a = new Fp2().Add(r.X, E);
            a.Square(a);
            a.Sub(a, A);
            a.Sub(a, G);
            t.Add(B, B);
            t.Add(t, t);
            a.Sub(a, t);

            // c = 2 * rOut.z * r.t * q.y
            var c = new Fp2().Mul(rOutZ, r.T);
            c.Add(c, c);
            c.MulScalar(c, qY.B);  // qY.B is the real part (the scalar value)

            return new LineFunctionResult { A = a, B = b, C = c, ROut = rOut };
        }

        private static LineFunctionResult LineFunctionAdd(TwistPoint r, TwistPoint p, Fp2 qX, Fp2 qY, Fp2 r2)
        {
            // Following go-ethereum/crypto/bn256/google/optate.go exactly
            // r is current point, p is twist point to add, q is G1 point, r2 = p.y²

            // B = p.x * r.t
            var B = new Fp2().Mul(p.X, r.T);

            // D = ((p.y + r.z)² - r2 - r.t) * r.t
            var D = new Fp2().Add(p.Y, r.Z);
            D.Square(D);
            D.Sub(D, r2);
            D.Sub(D, r.T);
            D.Mul(D, r.T);

            // H = B - r.x
            var H = new Fp2().Sub(B, r.X);
            // I = H²
            var I = new Fp2().Square(H);

            // E = 4 * I
            var E = new Fp2().Add(I, I);
            E.Add(E, E);

            // J = H * E
            var J = new Fp2().Mul(H, E);

            // L1 = D - 2*r.y
            var L1 = new Fp2().Sub(D, r.Y);
            L1.Sub(L1, r.Y);

            // V = r.x * E
            var V = new Fp2().Mul(r.X, E);

            // rOut.x = L1² - J - 2*V
            var rOutX = new Fp2().Square(L1);
            rOutX.Sub(rOutX, J);
            rOutX.Sub(rOutX, V);
            rOutX.Sub(rOutX, V);

            // rOut.z = (r.z + H)² - r.t - I
            var rOutZ = new Fp2().Add(r.Z, H);
            rOutZ.Square(rOutZ);
            rOutZ.Sub(rOutZ, r.T);
            rOutZ.Sub(rOutZ, I);

            // t = (V - rOut.x) * L1
            var t = new Fp2().Sub(V, rOutX);
            t.Mul(t, L1);
            // t2 = 2 * r.y * J
            var t2 = new Fp2().Mul(r.Y, J);
            t2.Add(t2, t2);
            // rOut.y = t - t2
            var rOutY = new Fp2().Sub(t, t2);

            // rOut.t = rOut.z²
            var rOutT = new Fp2().Square(rOutZ);

            var rOut = new TwistPoint(rOutX, rOutY, rOutZ, rOutT);

            // Line coefficients (following Geth exactly)
            // t = (p.y + rOut.z)² - r2 - rOut.t
            t.Add(p.Y, rOutZ);
            t.Square(t);
            t.Sub(t, r2);
            t.Sub(t, rOutT);

            // t2 = 2 * L1 * p.x
            t2.Mul(L1, p.X);
            t2.Add(t2, t2);
            // a = t2 - t
            var a = new Fp2().Sub(t2, t);

            // c = 2 * rOut.z * q.y
            var c = new Fp2().MulScalar(rOutZ, qY.B);
            c.Add(c, c);

            // b = -2 * L1 * q.x
            var b = new Fp2().Neg(L1);
            b.MulScalar(b, qX.B);
            b.Add(b, b);

            return new LineFunctionResult { A = a, B = b, C = c, ROut = rOut };
        }

        /// <summary>
        /// Multiplies an Fp12 by a sparse line function element.
        /// </summary>
        private static void MulLine(Fp12 ret, Fp2 a, Fp2 b, Fp2 c)
        {
            var a2 = new Fp6(new Fp2(), a, b);
            a2.Mul(a2, ret.X);
            var t3 = new Fp6().MulScalar(ret.Y, c);

            var t = new Fp2().Add(b, c);
            var t2 = new Fp6(new Fp2(), a, t);
            ret.X.Add(ret.X, ret.Y);
            ret.Y.Set(t3);
            ret.X.Mul(ret.X, t2);
            ret.X.Sub(ret.X, a2);
            ret.X.Sub(ret.X, ret.Y);
            a2.MulTau(a2);
            ret.Y.Add(ret.Y, a2);
        }

        /// <summary>
        /// Final exponentiation: raises to power (p^12 - 1) / r.
        /// </summary>
        private static Fp12 FinalExponentiation(Fp12 input)
        {
            var t1 = new Fp12();

            // Easy part: (p^6 - 1)(p^2 + 1)
            // t1 = input^(-1)
            t1.Invert(input);
            // t2 = input^(p^6) = conj(input)
            var t2 = new Fp12().Conjugate(input);
            // t1 = t2 * t1 = input^(p^6 - 1)
            t1.Mul(t2, t1);

            // t2 = t1^(p^2)
            t2.FrobeniusP2(t1);
            // t1 = t1 * t2 = input^((p^6 - 1)(p^2 + 1))
            t1.Mul(t1, t2);

            // Hard part
            var fp = new Fp12().Frobenius(t1);
            var fp2 = new Fp12().FrobeniusP2(t1);
            var fp3 = new Fp12().Frobenius(fp2);

            var fu = new Fp12().Exp(t1, BN128Constants.U);
            var fu2 = new Fp12().Exp(fu, BN128Constants.U);
            var fu3 = new Fp12().Exp(fu2, BN128Constants.U);

            var y3 = new Fp12().Frobenius(fu);
            var fu2p = new Fp12().Frobenius(fu2);
            var fu3p = new Fp12().Frobenius(fu3);
            var y2 = new Fp12().FrobeniusP2(fu2);

            var y0 = new Fp12().Mul(fp, fp2);
            y0.Mul(y0, fp3);

            var y1 = new Fp12().Conjugate(t1);
            var y5 = new Fp12().Conjugate(fu2);
            y3.Conjugate(y3);
            var y4 = new Fp12().Mul(fu, fu2p);
            y4.Conjugate(y4);

            var y6 = new Fp12().Mul(fu3, fu3p);
            y6.Conjugate(y6);

            var t0 = new Fp12().Square(y6);
            t0.Mul(t0, y4);
            t0.Mul(t0, y5);
            t1.Mul(y3, y5);
            t1.Mul(t1, t0);
            t0.Mul(t0, y2);
            t1.Square(t1);
            t1.Mul(t1, t0);
            t1.Square(t1);
            t0.Mul(t1, y1);
            t1.Mul(t1, y0);
            t0.Square(t0);
            t0.Mul(t0, t1);

            return t0;
        }

        private static TwistPoint MakeAffine(TwistPoint p)
        {
            var result = p.Copy();
            result.MakeAffine();
            return result;
        }
    }
}
