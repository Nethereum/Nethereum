using System;
using Org.BouncyCastle.Math;

namespace Nethereum.Signer.Crypto.BN128
{
    /// <summary>
    /// Fp2 represents an element of the quadratic extension field GF(p²) = GF(p)[i] / (i² + 1).
    /// Elements are represented as a + bi where i² = -1.
    /// Ported from go-ethereum/crypto/bn256/google/gfp2.go
    /// </summary>
    public class Fp2
    {
        public static readonly BigInteger P = new BigInteger("21888242871839275222246405745257275088696311157297823662689037894645226208583");

        public BigInteger A { get; private set; }
        public BigInteger B { get; private set; }

        public Fp2()
        {
            A = BigInteger.Zero;
            B = BigInteger.Zero;
        }

        public Fp2(BigInteger a, BigInteger b)
        {
            A = a.Mod(P);
            B = b.Mod(P);
        }

        public Fp2 Set(Fp2 other)
        {
            A = other.A;
            B = other.B;
            return this;
        }

        public Fp2 SetZero()
        {
            A = BigInteger.Zero;
            B = BigInteger.Zero;
            return this;
        }

        public Fp2 SetOne()
        {
            A = BigInteger.Zero;
            B = BigInteger.One;
            return this;
        }

        public bool IsZero()
        {
            return A.Equals(BigInteger.Zero) && B.Equals(BigInteger.Zero);
        }

        public bool IsOne()
        {
            return A.Equals(BigInteger.Zero) && B.Equals(BigInteger.One);
        }

        /// <summary>
        /// Adds two Fp2 elements: (a1 + b1*i) + (a2 + b2*i) = (a1+a2) + (b1+b2)*i
        /// </summary>
        public Fp2 Add(Fp2 x, Fp2 y)
        {
            A = x.A.Add(y.A).Mod(P);
            B = x.B.Add(y.B).Mod(P);
            return this;
        }

        /// <summary>
        /// Subtracts two Fp2 elements: (a1 + b1*i) - (a2 + b2*i) = (a1-a2) + (b1-b2)*i
        /// </summary>
        public Fp2 Sub(Fp2 x, Fp2 y)
        {
            A = x.A.Subtract(y.A).Mod(P);
            B = x.B.Subtract(y.B).Mod(P);
            return this;
        }

        /// <summary>
        /// Negates an Fp2 element: -(a + b*i) = -a + (-b)*i
        /// </summary>
        public Fp2 Neg(Fp2 x)
        {
            A = x.A.Negate().Mod(P);
            B = x.B.Negate().Mod(P);
            return this;
        }

        /// <summary>
        /// Conjugates an Fp2 element: conj(a + b*i) = -a + b (negate imaginary part)
        /// </summary>
        public Fp2 Conjugate(Fp2 x)
        {
            A = x.A.Negate().Mod(P);
            B = x.B;
            return this;
        }

        /// <summary>
        /// Multiplies two Fp2 elements using Karatsuba method.
        /// (a1 + b1*i)(a2 + b2*i) = (b1*b2 - a1*a2) + (a1*b2 + a2*b1)*i
        /// since i² = -1
        /// </summary>
        public Fp2 Mul(Fp2 x, Fp2 y)
        {
            // tx = a1*b2 + a2*b1 (imaginary part coefficient)
            var tx = x.A.Multiply(y.B).Add(y.A.Multiply(x.B)).Mod(P);

            // ty = b1*b2 - a1*a2 (real part)
            var ty = x.B.Multiply(y.B).Subtract(x.A.Multiply(y.A)).Mod(P);

            A = tx;
            B = ty;
            return this;
        }

        /// <summary>
        /// Squares an Fp2 element using optimized formula.
        /// (a + b*i)² = (b-a)(b+a) + 2ab*i
        /// </summary>
        public Fp2 Square(Fp2 x)
        {
            // t1 = b - a
            var t1 = x.B.Subtract(x.A);
            // t2 = b + a
            var t2 = x.B.Add(x.A);
            // ty = (b-a)(b+a) = b² - a²
            var ty = t1.Multiply(t2).Mod(P);

            // tx = 2*a*b
            var tx = x.A.Multiply(x.B).ShiftLeft(1).Mod(P);

            A = tx;
            B = ty;
            return this;
        }

        /// <summary>
        /// Inverts an Fp2 element: 1/(a + b*i) = (-a + b*i) / (a² + b²)
        /// </summary>
        public Fp2 Invert(Fp2 x)
        {
            // t = a² + b²
            var t = x.B.Multiply(x.B).Add(x.A.Multiply(x.A)).Mod(P);

            // inv = 1/t mod P
            var inv = t.ModInverse(P);

            // result = (-a + b*i) * inv
            A = x.A.Negate().Multiply(inv).Mod(P);
            B = x.B.Multiply(inv).Mod(P);
            return this;
        }

        /// <summary>
        /// Multiplies by a scalar from Fp.
        /// </summary>
        public Fp2 MulScalar(Fp2 x, BigInteger k)
        {
            A = x.A.Multiply(k).Mod(P);
            B = x.B.Multiply(k).Mod(P);
            return this;
        }

        /// <summary>
        /// Multiplies by xi = i + 9, which is the non-residue used in the tower.
        /// In our representation: A = imaginary coefficient, B = real coefficient
        /// (A*i + B) * (1*i + 9) = A*i² + 9*A*i + B*i + 9*B = -A + 9*A*i + B*i + 9*B
        ///                      = (9*B - A) + (9*A + B)*i
        /// So new_im = 9*A + B, new_re = 9*B - A
        /// </summary>
        public Fp2 MulXi(Fp2 x)
        {
            var nine = BigInteger.ValueOf(9);
            var newA = x.A.Multiply(nine).Add(x.B).Mod(P);
            var newB = x.B.Multiply(nine).Subtract(x.A).Mod(P);
            A = newA;
            B = newB;
            return this;
        }

        public Fp2 Copy()
        {
            return new Fp2(A, B);
        }

        public override bool Equals(object obj)
        {
            if (obj is Fp2 other)
            {
                return A.Equals(other.A) && B.Equals(other.B);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return A.GetHashCode() ^ B.GetHashCode();
        }

        public override string ToString()
        {
            return $"({A} + {B}*i)";
        }
    }
}
