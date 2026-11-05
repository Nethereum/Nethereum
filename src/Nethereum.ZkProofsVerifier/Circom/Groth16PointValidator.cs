using System;
using Nethereum.Signer.Crypto;
using Nethereum.Signer.Crypto.BN128;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Nethereum.ZkProofsVerifier.Groth16;

namespace Nethereum.ZkProofsVerifier.Circom
{
    internal static class Groth16PointValidator
    {
        public static ECPoint CreateG1(string xText, string yText)
        {
            var x = ParseCoordinate(xText);
            var y = ParseCoordinate(yText);
            if (x.SignValue == 0 && y.SignValue == 0)
                return BN128Curve.Curve.Infinity;
            var point = BN128Curve.Curve.CreatePoint(x, y);
            if (!point.IsValid())
                throw new ArgumentException("G1 point is not on the curve");
            return point;
        }

        public static TwistPoint CreateG2(string xC0Text, string xC1Text, string yC0Text, string yC1Text)
        {
            var xC0 = ParseCoordinate(xC0Text);
            var xC1 = ParseCoordinate(xC1Text);
            var yC0 = ParseCoordinate(yC0Text);
            var yC1 = ParseCoordinate(yC1Text);

            var x = new Fp2(xC1, xC0);
            var y = new Fp2(yC1, yC0);
            if (x.IsZero() && y.IsZero())
                return TwistPoint.FromAffine(x, y);

            if (!IsOnTwist(x, y))
                throw new ArgumentException("G2 point is not on the twist curve");

            var point = TwistPoint.FromAffine(x, y);
            if (!point.IsInCorrectSubgroup())
                throw new ArgumentException("G2 point is not in the r-order subgroup");
            return point;
        }

        public static string ValidateProof(Groth16Proof proof)
        {
            if (proof.A == null || proof.B == null || proof.C == null)
                return "Proof point is null";
            if (!IsValidG1(proof.A) || !IsValidG1(proof.C))
                return "G1 point is not on the curve";
            if (!IsValidG2(proof.B))
                return "G2 point is not on the twist curve or not in the r-order subgroup";
            return null;
        }

        private static bool IsValidG1(ECPoint point)
        {
            return point.IsInfinity || point.IsValid();
        }

        private static bool IsValidG2(TwistPoint point)
        {
            if (point.IsInfinity())
                return true;
            var affine = point.Copy();
            affine.MakeAffine();
            if (!IsOnTwist(affine.X, affine.Y))
                return false;
            return affine.IsInCorrectSubgroup();
        }

        private static bool IsOnTwist(Fp2 x, Fp2 y)
        {
            var y2 = new Fp2().Square(y);
            var x3 = new Fp2().Square(x);
            x3.Mul(x3, x);
            x3.Add(x3, BN128Constants.TwistB);
            return y2.Equals(x3);
        }

        private static BigInteger ParseCoordinate(string text)
        {
            var value = new BigInteger(FieldElementText.RequireBoundedDecimal(text));
            if (value.SignValue < 0 || value.CompareTo(BN128Constants.P) >= 0)
                throw new ArgumentException("Coordinate is not a canonical field element");
            return value;
        }
    }
}
