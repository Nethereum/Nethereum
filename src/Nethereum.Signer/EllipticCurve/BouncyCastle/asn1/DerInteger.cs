using System;

using NBitcoin.BouncyCastle.Math;
using NBitcoin.BouncyCastle.Utilities;

namespace NBitcoin.BouncyCastle.Asn1
{
	public class DerInteger
		: Asn1Object
	{
		private readonly byte[] bytes;

		/**
         * return an integer from the passed in object
         *
         * @exception ArgumentException if the object cannot be converted.
         */
		public static DerInteger GetInstance(
			object obj)
		{
			if(obj == null || obj is DerInteger)
			{
				return (DerInteger)obj;
			}

			throw new ArgumentException("illegal object in GetInstance: " + Platform.GetTypeName(obj));
		}

		public DerInteger(
			int value)
		{
			bytes = BigInteger.ValueOf(value).ToByteArray();
		}

		public DerInteger(
			BigInteger value)
		{
			if(value == null)
				throw new ArgumentNullException("value");

			bytes = value.ToByteArray();
		}

		public DerInteger(
			byte[] bytes)
		{
			this.bytes = bytes;
		}

		public BigInteger Value
		{
			get
			{
				return new BigInteger(bytes);
			}
		}

		/**
         * in some cases positive values Get crammed into a space,
         * that's not quite big enough...
         */
		public BigInteger PositiveValue
		{
			get
			{
				return new BigInteger(1, bytes);
			}
		}

		public override void Encode(
			DerOutputStream derOut)
		{
			derOut.WriteEncoded(Asn1Tags.Integer, bytes);
		}

		protected override int Asn1GetHashCode()
		{
			return Arrays.GetHashCode(bytes);
		}

		protected override bool Asn1Equals(
			Asn1Object asn1Object)
		{
			DerInteger other = asn1Object as DerInteger;

			if(other == null)
				return false;

			return Arrays.AreEqual(this.bytes, other.bytes);
		}

		public override string ToString()
		{
			return Value.ToString();
		}
	}
}
