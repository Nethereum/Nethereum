using System;
using System.Linq;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Nethereum.Signer.Crypto.BN128;
using Nethereum.Signer.Crypto.BN128.Managed;

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
            return TryAdd(input, out var output)
                ? output
                : throw new ArgumentException("Invalid BN128 ADD input: coordinates not in the field or point not on the curve");
        }

        public static byte[] Mul(byte[] input)
        {
            return TryMul(input, out var output)
                ? output
                : throw new ArgumentException("Invalid BN128 MUL input: coordinates not in the field or point not on the curve");
        }

        public static byte[] Pairing(byte[] input)
        {
            return TryPairing(input, out var output)
                ? output
                : throw new ArgumentException("Invalid BN128 pairing input: length, coordinates, curve membership or subgroup");
        }

        public static bool TryAdd(byte[] input, out byte[] output)
        {
            var data = PadToLength(input, 128);
            output = null;
            if (!G1Jacobian.TryFromAffineBytes(data, 0, out var first)) return false;
            if (!G1Jacobian.TryFromAffineBytes(data, G1Jacobian.AffineByteLength, out var second)) return false;
            output = first.Add(second).ToAffineBytes();
            return true;
        }

        public static bool TryMul(byte[] input, out byte[] output)
        {
            var data = PadToLength(input, 96);
            output = null;
            if (!G1Jacobian.TryFromAffineBytes(data, 0, out var point)) return false;
            var scalar = BigEndianBytes.ReadUnsignedBigInteger(data, G1Jacobian.AffineByteLength, 32);
            output = G1ScalarMultiplication.Multiply(point, scalar).ToAffineBytes();
            return true;
        }

        public static bool TryPairing(byte[] input, out byte[] output)
        {
            output = null;
            if (input == null) input = new byte[0];

            if (input.Length % 192 != 0) return false;

            if (input.Length == 0)
            {
                output = PadTo32BytesLeft(new byte[] { 1 });
                return true;
            }

            int pairCount = input.Length / 192;
            var g1Points = new ECPoint[pairCount];
            var g2Points = new TwistPoint[pairCount];

            for (int i = 0; i < pairCount; i++)
            {
                int offset = i * 192;

                if (!TryReadG1Point(input, offset, out g1Points[i])) return false;
                if (!TryReadG2Point(input, offset + 64, out g2Points[i])) return false;
            }

            bool result = BN128Pairing.PairingCheck(g1Points, g2Points);

            output = PadTo32BytesLeft(new byte[] { result ? (byte)1 : (byte)0 });
            return true;
        }

        private static bool TryReadG1Point(byte[] input, int offset, out ECPoint point)
        {
            point = null;
            var x = ReadCoordinate(input, offset);
            var y = ReadCoordinate(input, offset + 32);

            if (!IsValidCoordinate(x) || !IsValidCoordinate(y)) return false;

            if (x.Equals(BigInteger.Zero) && y.Equals(BigInteger.Zero))
            {
                point = Curve.Infinity;
                return true;
            }

            var candidate = Curve.CreatePoint(x, y);
            if (!candidate.IsValid()) return false;
            point = candidate;
            return true;
        }

        private static bool TryReadG2Point(byte[] input, int offset, out TwistPoint point)
        {
            point = null;
            var xIm = ReadCoordinate(input, offset);
            var xRe = ReadCoordinate(input, offset + 32);
            var yIm = ReadCoordinate(input, offset + 64);
            var yRe = ReadCoordinate(input, offset + 96);

            if (!IsValidCoordinate(xIm) || !IsValidCoordinate(xRe) ||
                !IsValidCoordinate(yIm) || !IsValidCoordinate(yRe))
                return false;

            var g2X = new Fp2(xIm, xRe);
            var g2Y = new Fp2(yIm, yRe);

            if (g2X.IsZero() && g2Y.IsZero())
            {
                point = new TwistPoint();
                point.SetInfinity();
                return true;
            }

            if (!IsOnTwistCurve(g2X, g2Y)) return false;
            var candidate = TwistPoint.FromAffine(g2X, g2Y);
            if (!candidate.IsInCorrectSubgroup()) return false;
            point = candidate;
            return true;
        }

        private static bool IsOnTwistCurve(Fp2 x, Fp2 y)
        {
            if (x.IsZero() && y.IsZero())
                return true;

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
