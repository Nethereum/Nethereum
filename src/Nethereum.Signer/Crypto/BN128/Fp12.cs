using Org.BouncyCastle.Math;

namespace Nethereum.Signer.Crypto.BN128
{
    /// <summary>
    /// Fp12 represents an element of GF(p^12) = GF(p^6)[ω] / (ω² - τ).
    /// Elements are represented as x*ω + y where ω² = τ.
    /// Ported from go-ethereum/crypto/bn256/google/gfp12.go
    /// </summary>
    public class Fp12
    {
        public Fp6 X { get; private set; }
        public Fp6 Y { get; private set; }

        public Fp12()
        {
            X = new Fp6();
            Y = new Fp6();
        }

        public Fp12(Fp6 x, Fp6 y)
        {
            X = x.Copy();
            Y = y.Copy();
        }

        public Fp12 Set(Fp12 other)
        {
            X.Set(other.X);
            Y.Set(other.Y);
            return this;
        }

        public Fp12 SetZero()
        {
            X.SetZero();
            Y.SetZero();
            return this;
        }

        public Fp12 SetOne()
        {
            X.SetZero();
            Y.SetOne();
            return this;
        }

        public bool IsZero()
        {
            return X.IsZero() && Y.IsZero();
        }

        public bool IsOne()
        {
            return X.IsZero() && Y.IsOne();
        }

        public Fp12 Add(Fp12 a, Fp12 b)
        {
            X.Add(a.X, b.X);
            Y.Add(a.Y, b.Y);
            return this;
        }

        public Fp12 Sub(Fp12 a, Fp12 b)
        {
            X.Sub(a.X, b.X);
            Y.Sub(a.Y, b.Y);
            return this;
        }

        public Fp12 Neg(Fp12 a)
        {
            X.Neg(a.X);
            Y.Neg(a.Y);
            return this;
        }

        /// <summary>
        /// Conjugates an Fp12 element: conj(x*ω + y) = -x*ω + y
        /// </summary>
        public Fp12 Conjugate(Fp12 a)
        {
            X.Neg(a.X);
            Y.Set(a.Y);
            return this;
        }

        /// <summary>
        /// Multiplies two Fp12 elements.
        /// (a.x*ω + a.y)(b.x*ω + b.y) = (a.x*b.y + a.y*b.x)*ω + (a.y*b.y + a.x*b.x*τ)
        /// where ω² = τ
        /// </summary>
        public Fp12 Mul(Fp12 a, Fp12 b)
        {
            var tx = new Fp6().Mul(a.X, b.Y);
            var t = new Fp6().Mul(b.X, a.Y);
            tx.Add(tx, t);

            var ty = new Fp6().Mul(a.Y, b.Y);
            t.Mul(a.X, b.X);
            t.MulTau(t);
            ty.Add(ty, t);

            X.Set(tx);
            Y.Set(ty);
            return this;
        }

        /// <summary>
        /// Multiplies by an Fp6 scalar.
        /// </summary>
        public Fp12 MulScalar(Fp12 a, Fp6 b)
        {
            X.Mul(a.X, b);
            Y.Mul(a.Y, b);
            return this;
        }

        /// <summary>
        /// Squares an Fp12 element using the complex squaring algorithm.
        /// </summary>
        public Fp12 Square(Fp12 a)
        {
            // v0 = a.x * a.y
            var v0 = new Fp6().Mul(a.X, a.Y);

            // t = a.x * τ
            var t = new Fp6().MulTau(a.X);
            // t = a.y + t = a.y + a.x*τ
            t.Add(a.Y, t);
            // ty = a.x + a.y
            var ty = new Fp6().Add(a.X, a.Y);
            // ty = ty * t = (a.x + a.y)(a.y + a.x*τ)
            ty.Mul(ty, t);
            // ty = ty - v0
            ty.Sub(ty, v0);
            // t = v0 * τ
            t.MulTau(v0);
            // ty = ty - t
            ty.Sub(ty, t);

            // tx = v0 + v0 = 2*v0
            var tx = new Fp6().Add(v0, v0);

            X.Set(tx);
            Y.Set(ty);
            return this;
        }

        /// <summary>
        /// Inverts an Fp12 element.
        /// 1/(x*ω + y) = (-x*ω + y) / (y² - x²*τ)
        /// </summary>
        public Fp12 Invert(Fp12 a)
        {
            // t1 = a.x²
            var t1 = new Fp6().Square(a.X);
            // t1 = t1 * τ
            t1.MulTau(t1);
            // t2 = a.y²
            var t2 = new Fp6().Square(a.Y);
            // t1 = t2 - t1 = a.y² - a.x²*τ
            t1.Sub(t2, t1);
            // t2 = 1/t1
            t2.Invert(t1);

            // x = -a.x * t2
            X.Neg(a.X);
            X.Mul(X, t2);
            // y = a.y * t2
            Y.Mul(a.Y, t2);
            return this;
        }

        /// <summary>
        /// Raises to power p (Frobenius automorphism).
        /// </summary>
        public Fp12 Frobenius(Fp12 a)
        {
            X.Frobenius(a.X);
            Y.Frobenius(a.Y);
            X.MulScalar(X, BN128Constants.XiToPMinus1Over6);
            return this;
        }

        /// <summary>
        /// Raises to power p² (Frobenius P² automorphism).
        /// go-ethereum: e.x.MulGFP(e.x, xiToPSquaredMinus1Over6)
        /// </summary>
        public Fp12 FrobeniusP2(Fp12 a)
        {
            X.FrobeniusP2(a.X);
            X.MulGFP(X, BN128Constants.XiToPSquaredMinus1Over6);
            Y.FrobeniusP2(a.Y);
            return this;
        }

        /// <summary>
        /// Raises to power p⁴.
        /// </summary>
        public Fp12 FrobeniusP4(Fp12 a)
        {
            X.MulGFP(a.X, BN128Constants.XiTo2PSquaredMinus2Over3);
            Y.Set(a.Y);
            return this;
        }

        /// <summary>
        /// Exponentiates by a BigInteger.
        /// </summary>
        public Fp12 Exp(Fp12 a, BigInteger power)
        {
            var sum = new Fp12();
            sum.SetOne();
            var t = new Fp12();

            for (int i = power.BitLength - 1; i >= 0; i--)
            {
                t.Square(sum);
                if (power.TestBit(i))
                {
                    sum.Mul(t, a);
                }
                else
                {
                    sum.Set(t);
                }
            }

            this.Set(sum);
            return this;
        }

        public Fp12 Copy()
        {
            return new Fp12(X, Y);
        }

        public override bool Equals(object obj)
        {
            if (obj is Fp12 other)
            {
                return X.Equals(other.X) && Y.Equals(other.Y);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return X.GetHashCode() ^ Y.GetHashCode();
        }
    }
}
