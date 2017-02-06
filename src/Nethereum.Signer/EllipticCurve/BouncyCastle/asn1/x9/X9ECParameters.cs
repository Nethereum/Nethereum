using System;

using NBitcoin.BouncyCastle.Math;
using NBitcoin.BouncyCastle.Math.EC;
using NBitcoin.BouncyCastle.Math.Field;

namespace NBitcoin.BouncyCastle.Asn1.X9
{
	/**
     * ASN.1 def for Elliptic-Curve ECParameters structure. See
     * X9.62, for further details.
     */
	public class X9ECParameters
		: Asn1Encodable
	{
		private X9FieldID fieldID;
		private ECCurve curve;
		private X9ECPoint g;
		private BigInteger n;
		private BigInteger h;
		private byte[] seed;

		public X9ECParameters(
			ECCurve curve,
			ECPoint g,
			BigInteger n)
			: this(curve, g, n, null, null)
		{
		}

		public X9ECParameters(
			ECCurve curve,
			X9ECPoint g,
			BigInteger n,
			BigInteger h)
			: this(curve, g, n, h, null)
		{
		}

		public X9ECParameters(
			ECCurve curve,
			ECPoint g,
			BigInteger n,
			BigInteger h)
			: this(curve, g, n, h, null)
		{
		}

		public X9ECParameters(
			ECCurve curve,
			ECPoint g,
			BigInteger n,
			BigInteger h,
			byte[] seed)
			: this(curve, new X9ECPoint(g), n, h, seed)
		{
		}

		public X9ECParameters(
			ECCurve curve,
			X9ECPoint g,
			BigInteger n,
			BigInteger h,
			byte[] seed)
		{
			this.curve = curve;
			this.g = g;
			this.n = n;
			this.h = h;
			this.seed = seed;

			if(ECAlgorithms.IsFpCurve(curve))
			{
				this.fieldID = new X9FieldID(curve.Field.Characteristic);
			}
			else if(ECAlgorithms.IsF2mCurve(curve))
			{
				IPolynomialExtensionField field = (IPolynomialExtensionField)curve.Field;
				int[] exponents = field.MinimalPolynomial.GetExponentsPresent();
				if(exponents.Length == 3)
				{
					this.fieldID = new X9FieldID(exponents[2], exponents[1]);
				}
				else if(exponents.Length == 5)
				{
					this.fieldID = new X9FieldID(exponents[4], exponents[1], exponents[2], exponents[3]);
				}
				else
				{
					throw new ArgumentException("Only trinomial and pentomial curves are supported");
				}
			}
			else
			{
				throw new ArgumentException("'curve' is of an unsupported type");
			}
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
				return g.Point;
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
			return seed;
		}

		/**
         * Return the ASN.1 entry representing the Curve.
         *
         * @return the X9Curve for the curve in these parameters.
         */
		public X9Curve CurveEntry
		{
			get
			{
				return new X9Curve(curve, seed);
			}
		}

		/**
         * Return the ASN.1 entry representing the FieldID.
         *
         * @return the X9FieldID for the FieldID in these parameters.
         */
		public X9FieldID FieldIDEntry
		{
			get
			{
				return fieldID;
			}
		}

		/**
         * Return the ASN.1 entry representing the base point G.
         *
         * @return the X9ECPoint for the base point in these parameters.
         */
		public X9ECPoint BaseEntry
		{
			get
			{
				return g;
			}
		}

		/**
         * Produce an object suitable for an Asn1OutputStream.
         * <pre>
         *  ECParameters ::= Sequence {
         *      version         Integer { ecpVer1(1) } (ecpVer1),
         *      fieldID         FieldID {{FieldTypes}},
         *      curve           X9Curve,
         *      base            X9ECPoint,
         *      order           Integer,
         *      cofactor        Integer OPTIONAL
         *  }
         * </pre>
         */
		public override Asn1Object ToAsn1Object()
		{
			Asn1EncodableVector v = new Asn1EncodableVector(
				new DerInteger(BigInteger.One),
				fieldID,
				new X9Curve(curve, seed),
				g,
				new DerInteger(n));

			if(h != null)
			{
				v.Add(new DerInteger(h));
			}

			return new DerSequence(v);
		}
	}
}
