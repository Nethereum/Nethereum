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

        private static readonly Fp Nine = Fp.FromBigInteger(BigInteger.ValueOf(9));

        private Fp _a;
        private Fp _b;

        public BigInteger A => _a.ToBigInteger();
        public BigInteger B => _b.ToBigInteger();

        internal Fp AF => _a;
        internal Fp BF => _b;

        public Fp2()
        {
            _a = Fp.Zero;
            _b = Fp.Zero;
        }

        public Fp2(BigInteger a, BigInteger b)
        {
            _a = Fp.FromBigInteger(a);
            _b = Fp.FromBigInteger(b);
        }

        internal Fp2(Fp a, Fp b)
        {
            _a = a;
            _b = b;
        }

        public Fp2 Set(Fp2 other)
        {
            _a = other._a;
            _b = other._b;
            return this;
        }

        public Fp2 SetZero()
        {
            _a = Fp.Zero;
            _b = Fp.Zero;
            return this;
        }

        public Fp2 SetOne()
        {
            _a = Fp.Zero;
            _b = Fp.One;
            return this;
        }

        public bool IsZero()
        {
            return _a.IsZero && _b.IsZero;
        }

        public bool IsOne()
        {
            return _a.IsZero && _b.Equals(Fp.One);
        }

        /// <summary>
        /// Adds two Fp2 elements: (a1 + b1*i) + (a2 + b2*i) = (a1+a2) + (b1+b2)*i
        /// </summary>
        public Fp2 Add(Fp2 x, Fp2 y)
        {
            _a = x._a.Add(y._a);
            _b = x._b.Add(y._b);
            return this;
        }

        /// <summary>
        /// Subtracts two Fp2 elements: (a1 + b1*i) - (a2 + b2*i) = (a1-a2) + (b1-b2)*i
        /// </summary>
        public Fp2 Sub(Fp2 x, Fp2 y)
        {
            _a = x._a.Sub(y._a);
            _b = x._b.Sub(y._b);
            return this;
        }

        /// <summary>
        /// Negates an Fp2 element: -(a + b*i) = -a + (-b)*i
        /// </summary>
        public Fp2 Neg(Fp2 x)
        {
            _a = x._a.Neg();
            _b = x._b.Neg();
            return this;
        }

        /// <summary>
        /// Conjugates an Fp2 element: conj(a + b*i) = -a + b (negate imaginary part)
        /// </summary>
        public Fp2 Conjugate(Fp2 x)
        {
            _a = x._a.Neg();
            _b = x._b;
            return this;
        }

        /// <summary>
        /// Multiplies two Fp2 elements using Karatsuba method.
        /// (a1 + b1*i)(a2 + b2*i) = (b1*b2 - a1*a2) + (a1*b2 + a2*b1)*i
        /// since i² = -1
        /// </summary>
        public Fp2 Mul(Fp2 x, Fp2 y)
        {
            var tx = x._a.Mul(y._b).Add(y._a.Mul(x._b));
            var ty = x._b.Mul(y._b).Sub(x._a.Mul(y._a));

            _a = tx;
            _b = ty;
            return this;
        }

        /// <summary>
        /// Squares an Fp2 element using optimized formula.
        /// (a + b*i)² = (b-a)(b+a) + 2ab*i
        /// </summary>
        public Fp2 Square(Fp2 x)
        {
            var t1 = x._b.Sub(x._a);
            var t2 = x._b.Add(x._a);
            var ty = t1.Mul(t2);

            var ab = x._a.Mul(x._b);
            var tx = ab.Add(ab);

            _a = tx;
            _b = ty;
            return this;
        }

        /// <summary>
        /// Inverts an Fp2 element: 1/(a + b*i) = (-a + b*i) / (a² + b²)
        /// </summary>
        public Fp2 Invert(Fp2 x)
        {
            var t = x._b.Mul(x._b).Add(x._a.Mul(x._a));

            var inv = t.Inv();

            _a = x._a.Neg().Mul(inv);
            _b = x._b.Mul(inv);
            return this;
        }

        /// <summary>
        /// Multiplies by a scalar from Fp.
        /// </summary>
        public Fp2 MulScalar(Fp2 x, Fp k)
        {
            _a = x._a.Mul(k);
            _b = x._b.Mul(k);
            return this;
        }

        public Fp2 MulScalar(Fp2 x, BigInteger k) => MulScalar(x, Fp.FromBigInteger(k));

        /// <summary>
        /// Multiplies by xi = i + 9, which is the non-residue used in the tower.
        /// In our representation: A = imaginary coefficient, B = real coefficient
        /// (A*i + B) * (1*i + 9) = A*i² + 9*A*i + B*i + 9*B = -A + 9*A*i + B*i + 9*B
        ///                      = (9*B - A) + (9*A + B)*i
        /// So new_im = 9*A + B, new_re = 9*B - A
        /// </summary>
        public Fp2 MulXi(Fp2 x)
        {
            var newA = x._a.Mul(Nine).Add(x._b);
            var newB = x._b.Mul(Nine).Sub(x._a);
            _a = newA;
            _b = newB;
            return this;
        }

        public Fp2 Copy()
        {
            return new Fp2(_a, _b);
        }

        public override bool Equals(object obj)
        {
            if (obj is Fp2 other)
            {
                return _a.Equals(other._a) && _b.Equals(other._b);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return _a.GetHashCode() ^ _b.GetHashCode();
        }

        public override string ToString()
        {
            return $"({A} + {B}*i)";
        }
    }
}
