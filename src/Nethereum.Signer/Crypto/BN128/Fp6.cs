using System;
using Org.BouncyCastle.Math;

namespace Nethereum.Signer.Crypto.BN128
{
    /// <summary>
    /// Fp6 represents an element of the sextic extension field GF(p^6) = GF(p²)[τ] / (τ³ - ξ).
    /// Elements are represented as x*τ² + y*τ + z where τ³ = ξ and ξ = i + 9.
    /// Ported from go-ethereum/crypto/bn256/google/gfp6.go
    /// </summary>
    public class Fp6
    {
        public Fp2 X { get; private set; }
        public Fp2 Y { get; private set; }
        public Fp2 Z { get; private set; }

        public Fp6()
        {
            X = new Fp2();
            Y = new Fp2();
            Z = new Fp2();
        }

        public Fp6(Fp2 x, Fp2 y, Fp2 z)
        {
            X = x.Copy();
            Y = y.Copy();
            Z = z.Copy();
        }

        public Fp6 Set(Fp6 other)
        {
            X.Set(other.X);
            Y.Set(other.Y);
            Z.Set(other.Z);
            return this;
        }

        public Fp6 SetZero()
        {
            X.SetZero();
            Y.SetZero();
            Z.SetZero();
            return this;
        }

        public Fp6 SetOne()
        {
            X.SetZero();
            Y.SetZero();
            Z.SetOne();
            return this;
        }

        public bool IsZero()
        {
            return X.IsZero() && Y.IsZero() && Z.IsZero();
        }

        public bool IsOne()
        {
            return X.IsZero() && Y.IsZero() && Z.IsOne();
        }

        public Fp6 Add(Fp6 a, Fp6 b)
        {
            X.Add(a.X, b.X);
            Y.Add(a.Y, b.Y);
            Z.Add(a.Z, b.Z);
            return this;
        }

        public Fp6 Sub(Fp6 a, Fp6 b)
        {
            X.Sub(a.X, b.X);
            Y.Sub(a.Y, b.Y);
            Z.Sub(a.Z, b.Z);
            return this;
        }

        public Fp6 Neg(Fp6 a)
        {
            X.Neg(a.X);
            Y.Neg(a.Y);
            Z.Neg(a.Z);
            return this;
        }

        public Fp6 Double(Fp6 a)
        {
            X.Add(a.X, a.X);
            Y.Add(a.Y, a.Y);
            Z.Add(a.Z, a.Z);
            return this;
        }

        /// <summary>
        /// Multiplies two Fp6 elements using Karatsuba-like method.
        /// </summary>
        public Fp6 Mul(Fp6 a, Fp6 b)
        {
            // Karatsuba multiplication for (ax*τ² + ay*τ + az) * (bx*τ² + by*τ + bz)
            // where τ³ = ξ

            var v0 = new Fp2().Mul(a.Z, b.Z);
            var v1 = new Fp2().Mul(a.Y, b.Y);
            var v2 = new Fp2().Mul(a.X, b.X);

            var t0 = new Fp2().Add(a.X, a.Y);
            var t1 = new Fp2().Add(b.X, b.Y);
            var tz = new Fp2().Mul(t0, t1);
            tz.Sub(tz, v1);
            tz.Sub(tz, v2);
            tz.MulXi(tz);
            tz.Add(tz, v0);

            t0.Add(a.Y, a.Z);
            t1.Add(b.Y, b.Z);
            var ty = new Fp2().Mul(t0, t1);
            ty.Sub(ty, v0);
            ty.Sub(ty, v1);
            var t2 = new Fp2().MulXi(v2);
            ty.Add(ty, t2);

            t0.Add(a.X, a.Z);
            t1.Add(b.X, b.Z);
            var tx = new Fp2().Mul(t0, t1);
            tx.Sub(tx, v0);
            tx.Add(tx, v1);
            tx.Sub(tx, v2);

            X.Set(tx);
            Y.Set(ty);
            Z.Set(tz);
            return this;
        }

        /// <summary>
        /// Multiplies by a scalar Fp2 element.
        /// </summary>
        public Fp6 MulScalar(Fp6 a, Fp2 b)
        {
            X.Mul(a.X, b);
            Y.Mul(a.Y, b);
            Z.Mul(a.Z, b);
            return this;
        }

        /// <summary>
        /// Multiplies by a scalar from Fp (BigInteger).
        /// Each Fp2 component has both real and imaginary parts multiplied by the scalar.
        /// </summary>
        public Fp6 MulGFP(Fp6 a, BigInteger k)
        {
            X.MulScalar(a.X, k);
            Y.MulScalar(a.Y, k);
            Z.MulScalar(a.Z, k);
            return this;
        }

        /// <summary>
        /// Multiplies by τ: (x*τ² + y*τ + z) * τ = x*τ³ + y*τ² + z*τ = ξ*x + y*τ² + z*τ
        /// </summary>
        public Fp6 MulTau(Fp6 a)
        {
            var tz = new Fp2().MulXi(a.X);
            var ty = new Fp2().Set(a.Z);
            var tx = new Fp2().Set(a.Y);

            X.Set(tx);
            Y.Set(ty);
            Z.Set(tz);
            return this;
        }

        /// <summary>
        /// Squares an Fp6 element.
        /// </summary>
        public Fp6 Square(Fp6 a)
        {
            var v0 = new Fp2().Square(a.Z);
            var v1 = new Fp2().Square(a.Y);
            var v2 = new Fp2().Square(a.X);

            var c0 = new Fp2().Add(a.X, a.Y);
            c0.Square(c0);
            c0.Sub(c0, v1);
            c0.Sub(c0, v2);
            c0.MulXi(c0);
            c0.Add(c0, v0);

            var c1 = new Fp2().Add(a.Y, a.Z);
            c1.Square(c1);
            c1.Sub(c1, v0);
            c1.Sub(c1, v1);
            var xiV2 = new Fp2().MulXi(v2);
            c1.Add(c1, xiV2);

            var c2 = new Fp2().Add(a.X, a.Z);
            c2.Square(c2);
            c2.Sub(c2, v0);
            c2.Add(c2, v1);
            c2.Sub(c2, v2);

            X.Set(c2);
            Y.Set(c1);
            Z.Set(c0);
            return this;
        }

        /// <summary>
        /// Inverts an Fp6 element.
        /// go-ethereum formula: A = z² - ξxy, B = ξx² - yz, C = y² - xz
        /// F = ξCy + Az + ξBx, result = (C/F, B/F, A/F)
        /// </summary>
        public Fp6 Invert(Fp6 a)
        {
            // A = z² - ξxy
            var A = new Fp2().Square(a.Z);
            var t1 = new Fp2().Mul(a.X, a.Y);
            t1.MulXi(t1);
            A.Sub(A, t1);

            // B = ξx² - yz
            var B = new Fp2().Square(a.X);
            B.MulXi(B);
            t1.Mul(a.Y, a.Z);
            B.Sub(B, t1);

            // C = y² - xz
            var C = new Fp2().Square(a.Y);
            t1.Mul(a.X, a.Z);
            C.Sub(C, t1);

            // F = ξCy + Az + ξBx
            var F = new Fp2().Mul(C, a.Y);
            F.MulXi(F);
            t1.Mul(A, a.Z);
            F.Add(F, t1);
            t1.Mul(B, a.X);
            t1.MulXi(t1);
            F.Add(F, t1);

            // Invert F
            F.Invert(F);

            // Result = (C*1/F, B*1/F, A*1/F)
            X.Mul(C, F);
            Y.Mul(B, F);
            Z.Mul(A, F);
            return this;
        }

        /// <summary>
        /// Frobenius automorphism: raises to the power p.
        /// go-ethereum: x *= xiTo2PMinus2Over3, y *= xiToPMinus1Over3
        /// </summary>
        public Fp6 Frobenius(Fp6 a)
        {
            X.Conjugate(a.X);
            Y.Conjugate(a.Y);
            Z.Conjugate(a.Z);

            X.Mul(X, BN128Constants.XiTo2PMinus2Over3);
            Y.Mul(Y, BN128Constants.XiToPMinus1Over3);
            return this;
        }

        /// <summary>
        /// Frobenius P² automorphism.
        /// go-ethereum: x *= xiTo2PSquaredMinus2Over3, y *= xiToPSquaredMinus1Over3
        /// </summary>
        public Fp6 FrobeniusP2(Fp6 a)
        {
            X.MulScalar(a.X, BN128Constants.XiTo2PSquaredMinus2Over3);
            Y.MulScalar(a.Y, BN128Constants.XiToPSquaredMinus1Over3);
            Z.Set(a.Z);
            return this;
        }

        public Fp6 Copy()
        {
            return new Fp6(X, Y, Z);
        }

        public override bool Equals(object obj)
        {
            if (obj is Fp6 other)
            {
                return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode();
        }
    }
}
