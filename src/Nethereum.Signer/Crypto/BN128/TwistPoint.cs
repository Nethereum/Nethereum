using Org.BouncyCastle.Math;

namespace Nethereum.Signer.Crypto.BN128
{
    /// <summary>
    /// TwistPoint represents a point on the twisted curve y² = x³ + B/ξ over GF(p²).
    /// Points are maintained in Jacobian coordinates (X:Y:Z) where x = X/Z² and y = Y/Z³.
    /// Ported from go-ethereum/crypto/bn256/google/twist.go
    /// </summary>
    public class TwistPoint
    {
        public Fp2 X { get; private set; }
        public Fp2 Y { get; private set; }
        public Fp2 Z { get; private set; }
        public Fp2 T { get; private set; }

        public TwistPoint()
        {
            X = new Fp2();
            Y = new Fp2();
            Z = new Fp2();
            T = new Fp2();
        }

        public TwistPoint(Fp2 x, Fp2 y, Fp2 z, Fp2 t)
        {
            X = x.Copy();
            Y = y.Copy();
            Z = z.Copy();
            T = t?.Copy() ?? new Fp2();
        }

        public TwistPoint Set(TwistPoint other)
        {
            X.Set(other.X);
            Y.Set(other.Y);
            Z.Set(other.Z);
            T.Set(other.T);
            return this;
        }

        public bool IsInfinity()
        {
            return Z.IsZero();
        }

        public TwistPoint SetInfinity()
        {
            X.SetZero();
            Y.SetOne();
            Z.SetZero();
            T.SetZero();
            return this;
        }

        /// <summary>
        /// Adds two twist points using the 2007 Bernstein-Lange addition formula.
        /// </summary>
        public TwistPoint Add(TwistPoint a, TwistPoint b)
        {
            if (a.IsInfinity())
            {
                return this.Set(b);
            }
            if (b.IsInfinity())
            {
                return this.Set(a);
            }

            // z1z1 = a.z²
            var z1z1 = new Fp2().Square(a.Z);
            // z2z2 = b.z²
            var z2z2 = new Fp2().Square(b.Z);
            // u1 = a.x * z2z2
            var u1 = new Fp2().Mul(a.X, z2z2);
            // u2 = b.x * z1z1
            var u2 = new Fp2().Mul(b.X, z1z1);

            // t = b.z * z2z2
            var t = new Fp2().Mul(b.Z, z2z2);
            // s1 = a.y * t
            var s1 = new Fp2().Mul(a.Y, t);

            // t = a.z * z1z1
            t.Mul(a.Z, z1z1);
            // s2 = b.y * t
            var s2 = new Fp2().Mul(b.Y, t);

            // h = u2 - u1
            var h = new Fp2().Sub(u2, u1);
            // xEqual = h.IsZero()
            var xEqual = h.IsZero();

            // t = h + h
            t.Add(h, h);
            // i = t²
            var i = new Fp2().Square(t);
            // j = h * i
            var j = new Fp2().Mul(h, i);

            // t = s2 - s1
            t.Sub(s2, s1);
            var yEqual = t.IsZero();

            if (xEqual && yEqual)
            {
                return this.Double(a);
            }

            // r = t + t
            var r = new Fp2().Add(t, t);
            // v = u1 * i
            var v = new Fp2().Mul(u1, i);

            // t4 = r²
            var t4 = new Fp2().Square(r);
            // t = v + v
            t.Add(v, v);
            // t6 = t4 - j
            var t6 = new Fp2().Sub(t4, j);
            // x = t6 - t
            X.Sub(t6, t);

            // t = v - x (note: using newly computed x)
            t.Sub(v, X);
            // t4 = s1 * j
            t4.Mul(s1, j);
            // t6 = t4 + t4
            t6.Add(t4, t4);
            // t4 = r * t
            t4.Mul(r, t);
            // y = t4 - t6
            Y.Sub(t4, t6);

            // t = a.z + b.z
            t.Add(a.Z, b.Z);
            // t4 = t²
            t4.Square(t);
            // t = t4 - z1z1
            t.Sub(t4, z1z1);
            // t4 = t - z2z2
            t4.Sub(t, z2z2);
            // z = t4 * h
            Z.Mul(t4, h);

            return this;
        }

        /// <summary>
        /// Doubles a twist point using the 2009 Bernstein-Lange doubling formula.
        /// </summary>
        public TwistPoint Double(TwistPoint a)
        {
            // Save a.Y * a.Z early to avoid aliasing issues when a == this
            var aYaZ = new Fp2().Mul(a.Y, a.Z);

            // A = a.x²
            var A = new Fp2().Square(a.X);
            // B = a.y²
            var B = new Fp2().Square(a.Y);
            // C = B²
            var C = new Fp2().Square(B);

            // t = a.x + B
            var t = new Fp2().Add(a.X, B);
            // t2 = t²
            var t2 = new Fp2().Square(t);
            // t = t2 - A
            t.Sub(t2, A);
            // t2 = t - C
            t2.Sub(t, C);
            // d = t2 + t2
            var d = new Fp2().Add(t2, t2);

            // t = A + A
            t.Add(A, A);
            // e = t + A
            var e = new Fp2().Add(t, A);

            // f = e²
            var f = new Fp2().Square(e);

            // t = d + d
            t.Add(d, d);
            // x = f - t
            X.Sub(f, t);

            // t = C + C
            t.Add(C, C);
            // t2 = t + t
            t2.Add(t, t);
            // t = t2 + t2
            t.Add(t2, t2);
            // t2 = d - x (using new x)
            t2.Sub(d, X);
            // t3 = e * t2
            var t3 = new Fp2().Mul(e, t2);
            // y = t3 - t
            Y.Sub(t3, t);

            // z = aYaZ + aYaZ (using saved value)
            Z.Add(aYaZ, aYaZ);

            return this;
        }

        /// <summary>
        /// Converts to affine coordinates.
        /// </summary>
        public TwistPoint MakeAffine()
        {
            if (Z.IsOne())
            {
                return this;
            }
            if (Z.IsZero())
            {
                X.SetZero();
                Y.SetOne();
                T.SetZero();
                return this;
            }

            var zInv = new Fp2().Invert(Z);
            var zInv2 = new Fp2().Square(zInv);
            var zInv3 = new Fp2().Mul(zInv2, zInv);

            X.Mul(X, zInv2);
            Y.Mul(Y, zInv3);
            Z.SetOne();
            T.SetOne();
            return this;
        }

        /// <summary>
        /// Negates a twist point.
        /// </summary>
        public TwistPoint Neg(TwistPoint a)
        {
            X.Set(a.X);
            Y.Neg(a.Y);
            Z.Set(a.Z);
            T.SetZero();
            return this;
        }

        public TwistPoint Copy()
        {
            return new TwistPoint(X, Y, Z, T);
        }

        public TwistPoint ScalarMul(TwistPoint point, BigInteger scalar)
        {
            var result = new TwistPoint();
            result.SetInfinity();

            if (scalar.SignValue == 0 || point.IsInfinity())
            {
                return this.Set(result);
            }

            var temp = point.Copy();
            var scalarCopy = scalar;

            while (scalarCopy.SignValue > 0)
            {
                if (scalarCopy.TestBit(0))
                {
                    result.Add(result, temp);
                }
                temp.Double(temp);
                scalarCopy = scalarCopy.ShiftRight(1);
            }

            return this.Set(result);
        }

        public bool IsInCorrectSubgroup()
        {
            if (IsInfinity())
                return true;

            var result = new TwistPoint();
            result.ScalarMul(this, BN128Constants.Order);
            return result.IsInfinity();
        }

        /// <summary>
        /// Creates a twist point from affine coordinates.
        /// </summary>
        public static TwistPoint FromAffine(Fp2 x, Fp2 y)
        {
            if (x.IsZero() && y.IsZero())
            {
                var inf = new TwistPoint();
                inf.SetInfinity();
                return inf;
            }

            return new TwistPoint(x, y, new Fp2().SetOne(), new Fp2().SetOne());
        }
    }
}
