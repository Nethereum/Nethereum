using System;
using System.IO;

namespace NBitcoin.BouncyCastle.Asn1
{
	internal abstract class Asn1Object
		: Asn1Encodable
	{
		/// <summary>Create a base ASN.1 object from a byte array.</summary>
		/// <param name="data">The byte array to parse.</param>
		/// <returns>The base ASN.1 object represented by the byte array.</returns>
		/// <exception cref="IOException">If there is a problem parsing the data.</exception>
		public static Asn1Object FromByteArray(
			byte[] data)
		{
			try
			{
				MemoryStream input = new MemoryStream(data, false);
				Asn1InputStream asn1 = new Asn1InputStream(input, data.Length);
				Asn1Object result = asn1.ReadObject();
				if(input.Position != input.Length)
					throw new IOException("extra data found after object");
				return result;
			}
			catch(InvalidCastException)
			{
				throw new IOException("cannot recognise object in byte array");
			}
		}

		/// <summary>Read a base ASN.1 object from a stream.</summary>
		/// <param name="inStr">The stream to parse.</param>
		/// <returns>The base ASN.1 object represented by the byte array.</returns>
		/// <exception cref="IOException">If there is a problem parsing the data.</exception>
		public static Asn1Object FromStream(
			Stream inStr)
		{
			try
			{
				return new Asn1InputStream(inStr).ReadObject();
			}
			catch(InvalidCastException)
			{
				throw new IOException("cannot recognise object in stream");
			}
		}

		public sealed override Asn1Object ToAsn1Object()
		{
			return this;
		}

		public abstract void Encode(DerOutputStream derOut);

		protected abstract bool Asn1Equals(Asn1Object asn1Object);
		protected abstract int Asn1GetHashCode();

		public bool CallAsn1Equals(Asn1Object obj)
		{
			return Asn1Equals(obj);
		}

		public int CallAsn1GetHashCode()
		{
			return Asn1GetHashCode();
		}
	}
}
