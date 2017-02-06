using System;

using NBitcoin.BouncyCastle.Math;
using NBitcoin.BouncyCastle.Math.EC;
using NBitcoin.BouncyCastle.Utilities;

namespace NBitcoin.BouncyCastle.Crypto.Parameters
{
	public class ECDomainParameters
	{
		public ECCurve curve;
		public byte[] seed;
		public ECPoint g;
		public BigInteger n;
		public BigInteger h;

		public ECDomainParameters(
			ECCurve curve,
			ECPoint g,
			BigInteger n)
			: this(curve, g, n, BigInteger.One)
		{
		}

		public ECDomainParameters(
			ECCurve curve,
			ECPoint g,
			BigInteger n,
			BigInteger h)
			: this(curve, g, n, h, null)
		{
		}

		public ECDomainParameters(
			ECCurve curve,
			ECPoint g,
			BigInteger n,
			BigInteger h,
			byte[] seed)
		{
			if(curve == null)
				throw new ArgumentNullException("curve");
			if(g == null)
				throw new ArgumentNullException("g");
			if(n == null)
				throw new ArgumentNullException("n");
			if(h == null)
				throw new ArgumentNullException("h");

			this.curve = curve;
			this.g = g.Normalize();
			this.n = n;
			this.h = h;
			this.seed = Arrays.Clone(seed);
		}

		public ECCurve Curve
		{
			get
			{
				return curve;
			}
		}

		public ECPoint G
		{
			get
			{
				return g;
			}
		}

		public BigInteger N
		{
			get
			{
				return n;
			}
		}

		public BigInteger H
		{
			get
			{
				return h;
			}
		}

		public byte[] GetSeed()
		{
			return Arrays.Clone(seed);
		}

		public override bool Equals(
			object obj)
		{
			if(obj == this)
				return true;

			ECDomainParameters other = obj as ECDomainParameters;

			if(other == null)
				return false;

			return Equals(other);
		}

		protected bool Equals(
			ECDomainParameters other)
		{
			return curve.Equals(other.curve)
				&& g.Equals(other.g)
				&& n.Equals(other.n)
				&& h.Equals(other.h)
				&& Arrays.AreEqual(seed, other.seed);
		}

		public override int GetHashCode()
		{
			return curve.GetHashCode()
				^ g.GetHashCode()
				^ n.GetHashCode()
				^ h.GetHashCode()
				^ Arrays.GetHashCode(seed);
		}
	}

}
