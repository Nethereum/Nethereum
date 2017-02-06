using NBitcoin.BouncyCastle.Math;
using NBitcoin.BouncyCastle.Math.EC;
using NBitcoin.BouncyCastle.Math.EC.Custom.Sec;
using NBitcoin.BouncyCastle.Math.EC.Endo;
using NBitcoin.BouncyCastle.Utilities.Encoders;
using NBitcoin.BouncyCastle.Asn1.X9;

namespace NBitcoin.BouncyCastle.Crypto.EC
{
	public sealed class CustomNamedCurves
	{
		private CustomNamedCurves()
		{
		}

		private static ECCurve ConfigureCurveGlv(ECCurve c, GlvTypeBParameters p)
		{
			return c.Configure().SetEndomorphism(new GlvTypeBEndomorphism(c, p)).Create();
		}

		public static X9ECParameters Secp256k1
		{
			get
			{
				return SecP256K1Holder.Instance.Parameters;
			}
		}

		/*
         * secp256k1
         */
		public class SecP256K1Holder
			: X9ECParametersHolder
		{
			private SecP256K1Holder()
			{
			}

			public static readonly X9ECParametersHolder Instance = new SecP256K1Holder();

			protected override X9ECParameters CreateParameters()
			{
				byte[] S = null;
				GlvTypeBParameters glv = new GlvTypeBParameters(
					new BigInteger("7ae96a2b657c07106e64479eac3434e99cf0497512f58995c1396c28719501ee", 16),
					new BigInteger("5363ad4cc05c30e0a5261c028812645a122e22ea20816678df02967c1b23bd72", 16),
					new BigInteger[]{
						new BigInteger("3086d221a7d46bcde86c90e49284eb15", 16),
						new BigInteger("-e4437ed6010e88286f547fa90abfe4c3", 16) },
					new BigInteger[]{
						new BigInteger("114ca50f7a8e2f3f657c1108d9d44cfd8", 16),
						new BigInteger("3086d221a7d46bcde86c90e49284eb15", 16) },
					new BigInteger("3086d221a7d46bcde86c90e49284eb153dab", 16),
					new BigInteger("e4437ed6010e88286f547fa90abfe4c42212", 16),
					272);
				ECCurve curve = ConfigureCurveGlv(new SecP256K1Curve(), glv);
				X9ECPoint G = new X9ECPoint(curve, Hex.Decode("04"
					+ "79BE667EF9DCBBAC55A06295CE870B07029BFCDB2DCE28D959F2815B16F81798"
					+ "483ADA7726A3C4655DA4FBFC0E1108A8FD17B448A68554199C47D08FFB10D4B8"));
				return new X9ECParameters(curve, G, curve.Order, curve.Cofactor, S);
			}
		}


	}
}
