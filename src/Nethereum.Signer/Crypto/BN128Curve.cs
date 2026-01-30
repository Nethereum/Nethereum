using System;
using System.Linq;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Nethereum.Signer.Crypto.BN128;

namespace Nethereum.Signer.Crypto
{
    public static class BN128Curve
    {
        private static readonly BigInteger P =
            new BigInteger("21888242871839275222246405745257275088696311157297823662689037894645226208583");
        private static readonly BigInteger ORDER =
            new BigInteger("21888242871839275222246405745257275088548364400416034343698204186575808495617");
        private static readonly BigInteger A = BigInteger.Zero;
        private static readonly BigInteger B = BigInteger.Three;

        private static FpCurve _curve;
        public static FpCurve Curve
        {
            get
            {
                if (_curve == null)
                {
                    _curve = new FpCurve(P, A, B, ORDER, BigInteger.One);
                }
                return _curve;
            }
        }

        public static BigInteger FieldModulus => P;

        public static byte[] Add(byte[] input)
        {
            input = PadToLength(input, 128);

            var x1 = ReadCoordinate(input, 0);
            var y1 = ReadCoordinate(input, 32);
            var x2 = ReadCoordinate(input, 64);
            var y2 = ReadCoordinate(input, 96);

            if (!IsValidCoordinate(x1) || !IsValidCoordinate(y1) ||
                !IsValidCoordinate(x2) || !IsValidCoordinate(y2))
            {
                throw new ArgumentException("Invalid BN128 coordinates");
            }

            ECPoint p1, p2;

            if (x1.Equals(BigInteger.Zero) && y1.Equals(BigInteger.Zero))
            {
                p1 = Curve.Infinity;
            }
            else
            {
                p1 = Curve.CreatePoint(x1, y1);
                if (!p1.IsValid())
                    throw new ArgumentException("Point 1 is not on the BN128 curve");
            }

            if (x2.Equals(BigInteger.Zero) && y2.Equals(BigInteger.Zero))
            {
                p2 = Curve.Infinity;
            }
            else
            {
                p2 = Curve.CreatePoint(x2, y2);
                if (!p2.IsValid())
                    throw new ArgumentException("Point 2 is not on the BN128 curve");
            }

            var result = p1.Add(p2).Normalize();
            return EncodePoint(result);
        }

        public static byte[] Mul(byte[] input)
        {
            input = PadToLength(input, 96);

            var x = ReadCoordinate(input, 0);
            var y = ReadCoordinate(input, 32);
            var scalar = ReadCoordinate(input, 64);

            if (!IsValidCoordinate(x) || !IsValidCoordinate(y))
            {
                throw new ArgumentException("Invalid BN128 coordinates");
            }

            ECPoint p;
            if (x.Equals(BigInteger.Zero) && y.Equals(BigInteger.Zero))
            {
                p = Curve.Infinity;
            }
            else
            {
                p = Curve.CreatePoint(x, y);
                if (!p.IsValid())
                    throw new ArgumentException("Point is not on the BN128 curve");
            }

            var result = p.Multiply(scalar).Normalize();
            return EncodePoint(result);
        }

        public static byte[] Pairing(byte[] input)
        {
            if (input == null) input = new byte[0];

            if (input.Length % 192 != 0)
                throw new ArgumentException("Invalid BN128 pairing input length");

            if (input.Length == 0)
            {
                return PadTo32BytesLeft(new byte[] { 1 });
            }

            int pairCount = input.Length / 192;
            var g1Points = new ECPoint[pairCount];
            var g2Points = new TwistPoint[pairCount];

            for (int i = 0; i < pairCount; i++)
            {
                int offset = i * 192;

                // Read G1 point (first 64 bytes)
                var ax = ReadCoordinate(input, offset);
                var ay = ReadCoordinate(input, offset + 32);

                if (!IsValidCoordinate(ax) || !IsValidCoordinate(ay))
                    throw new ArgumentException("Invalid G1 coordinates in pairing input");

                if (ax.Equals(BigInteger.Zero) && ay.Equals(BigInteger.Zero))
                {
                    g1Points[i] = Curve.Infinity;
                }
                else
                {
                    var g1Point = Curve.CreatePoint(ax, ay);
                    if (!g1Point.IsValid())
                        throw new ArgumentException("G1 point not on curve in pairing input");
                    g1Points[i] = g1Point;
                }

                // Read G2 point (next 128 bytes)
                // G2 coordinates are in Fp2, stored as (imaginary, real) pairs
                var bx_im = ReadCoordinate(input, offset + 64);
                var bx_re = ReadCoordinate(input, offset + 96);
                var by_im = ReadCoordinate(input, offset + 128);
                var by_re = ReadCoordinate(input, offset + 160);

                if (!IsValidCoordinate(bx_im) || !IsValidCoordinate(bx_re) ||
                    !IsValidCoordinate(by_im) || !IsValidCoordinate(by_re))
                    throw new ArgumentException("Invalid G2 coordinates in pairing input");

                // Create G2 twist point
                var g2X = new Fp2(bx_im, bx_re);
                var g2Y = new Fp2(by_im, by_re);

                if (g2X.IsZero() && g2Y.IsZero())
                {
                    g2Points[i] = new TwistPoint();
                    g2Points[i].SetInfinity();
                }
                else
                {
                    g2Points[i] = TwistPoint.FromAffine(g2X, g2Y);
                    // Validate G2 point is on the twist curve
                    if (!IsOnTwistCurve(g2X, g2Y))
                        throw new ArgumentException("G2 point not on twist curve in pairing input");
                }
            }

            // Compute pairing check: ∏ e(g1[i], g2[i]) == 1
            bool result = BN128Pairing.PairingCheck(g1Points, g2Points);

            return PadTo32BytesLeft(new byte[] { result ? (byte)1 : (byte)0 });
        }

        /// <summary>
        /// Validates that a G2 point is on the twist curve y² = x³ + B'
        /// where B' = 3/(9+i) in Fp2
        /// </summary>
        private static bool IsOnTwistCurve(Fp2 x, Fp2 y)
        {
            if (x.IsZero() && y.IsZero())
                return true;

            // y² = x³ + B'
            var y2 = new Fp2().Square(y);
            var x3 = new Fp2().Square(x);
            x3.Mul(x3, x);
            x3.Add(x3, BN128Constants.TwistB);

            return y2.Equals(x3);
        }

        private static BigInteger ReadCoordinate(byte[] data, int offset)
        {
            var bytes = new byte[32];
            if (offset + 32 <= data.Length)
            {
                Array.Copy(data, offset, bytes, 0, 32);
            }
            else if (offset < data.Length)
            {
                Array.Copy(data, offset, bytes, 0, data.Length - offset);
            }
            return new BigInteger(1, bytes);
        }

        private static bool IsValidCoordinate(BigInteger coord)
        {
            return coord.CompareTo(BigInteger.Zero) >= 0 && coord.CompareTo(P) < 0;
        }

        private static byte[] EncodePoint(ECPoint point)
        {
            if (point.IsInfinity)
            {
                return new byte[64];
            }

            var x = point.AffineXCoord.ToBigInteger();
            var y = point.AffineYCoord.ToBigInteger();

            var result = new byte[64];
            var xBytes = x.ToByteArrayUnsigned();
            var yBytes = y.ToByteArrayUnsigned();

            Array.Copy(xBytes, 0, result, 32 - xBytes.Length, xBytes.Length);
            Array.Copy(yBytes, 0, result, 64 - yBytes.Length, yBytes.Length);

            return result;
        }

        private static byte[] PadToLength(byte[] data, int length)
        {
            if (data == null) return new byte[length];
            if (data.Length >= length) return data;
            var padded = new byte[length];
            Array.Copy(data, padded, data.Length);
            return padded;
        }

        private static byte[] PadTo32BytesLeft(byte[] data)
        {
            if (data.Length >= 32) return data;
            var result = new byte[32];
            Array.Copy(data, 0, result, 32 - data.Length, data.Length);
            return result;
        }
    }
}
