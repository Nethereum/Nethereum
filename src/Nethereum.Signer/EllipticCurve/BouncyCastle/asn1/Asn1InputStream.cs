using System;
using System.IO;

using NBitcoin.BouncyCastle.Utilities.IO;

namespace NBitcoin.BouncyCastle.Asn1
{
	/**
	 * a general purpose ASN.1 decoder - note: this class differs from the
	 * others in that it returns null after it has read the last object in
	 * the stream. If an ASN.1 Null is encountered a Der/BER Null object is
	 * returned.
	 */
	internal class Asn1InputStream
		: FilterStream
	{
		private readonly int limit;

		private readonly byte[][] tmpBuffers;

		public static int FindLimit(Stream input)
		{
			if(input is LimitedInputStream)
			{
				return ((LimitedInputStream)input).GetRemaining();
			}
			else if(input is MemoryStream)
			{
				MemoryStream mem = (MemoryStream)input;
				return (int)(mem.Length - mem.Position);
			}

			return int.MaxValue;
		}

		public Asn1InputStream(
			Stream inputStream)
			: this(inputStream, FindLimit(inputStream))
		{
		}

		/**
		 * Create an ASN1InputStream where no DER object will be longer than limit.
		 *
		 * @param input stream containing ASN.1 encoded data.
		 * @param limit maximum size of a DER encoded object.
		 */
		public Asn1InputStream(
			Stream inputStream,
			int limit)
			: base(inputStream)
		{
			this.limit = limit;
			this.tmpBuffers = new byte[16][];
		}

		/**
		 * Create an ASN1InputStream based on the input byte array. The length of DER objects in
		 * the stream is automatically limited to the length of the input array.
		 *
		 * @param input array containing ASN.1 encoded data.
		 */
		public Asn1InputStream(
			byte[] input)
			: this(new MemoryStream(input, false), input.Length)
		{
		}

		/**
		* build an object given its tag and the number of bytes to construct it from.
		*/
		private Asn1Object BuildObject(
			int tag,
			int tagNo,
			int length)
		{
			bool isConstructed = (tag & Asn1Tags.Constructed) != 0;

			DefiniteLengthInputStream defIn = new DefiniteLengthInputStream(this.s, length);

			if((tag & Asn1Tags.Application) != 0)
			{
				throw new IOException("invalid ECDSA sig");
			}

			if((tag & Asn1Tags.Tagged) != 0)
			{
				throw new IOException("invalid ECDSA sig");
			}

			if(isConstructed)
			{
				switch(tagNo)
				{
					case Asn1Tags.Sequence:
						return CreateDerSequence(defIn);
					default:
						throw new IOException("unknown tag " + tagNo + " encountered");
				}
			}

			return CreatePrimitiveDerObject(tagNo, defIn, tmpBuffers);
		}

		public Asn1EncodableVector BuildEncodableVector()
		{
			Asn1EncodableVector v = new Asn1EncodableVector();

			Asn1Object o;
			while((o = ReadObject()) != null)
			{
				v.Add(o);
			}

			return v;
		}

		public virtual Asn1EncodableVector BuildDerEncodableVector(
			DefiniteLengthInputStream dIn)
		{
			return new Asn1InputStream(dIn).BuildEncodableVector();
		}

		public virtual DerSequence CreateDerSequence(
			DefiniteLengthInputStream dIn)
		{
			return DerSequence.FromVector(BuildDerEncodableVector(dIn));
		}

		public Asn1Object ReadObject()
		{
			int tag = ReadByte();
			if(tag <= 0)
			{
				if(tag == 0)
					throw new IOException("unexpected end-of-contents marker");

				return null;
			}

			//
			// calculate tag number
			//
			int tagNo = ReadTagNumber(this.s, tag);

			bool isConstructed = (tag & Asn1Tags.Constructed) != 0;

			//
			// calculate length
			//
			int length = ReadLength(this.s, limit);

			if(length < 0) // indefinite length method
			{
				throw new IOException("indefinite length primitive encoding encountered");
			}
			else
			{
				try
				{
					return BuildObject(tag, tagNo, length);
				}
				catch(ArgumentException e)
				{
					throw new Asn1Exception("corrupted stream detected", e);
				}
			}
		}

		public static int ReadTagNumber(
			Stream s,
			int tag)
		{
			int tagNo = tag & 0x1f;

			//
			// with tagged object tag number is bottom 5 bits, or stored at the start of the content
			//
			if(tagNo == 0x1f)
			{
				tagNo = 0;

				int b = s.ReadByte();

				// X.690-0207 8.1.2.4.2
				// "c) bits 7 to 1 of the first subsequent octet shall not all be zero."
				if((b & 0x7f) == 0) // Note: -1 will pass
				{
					throw new IOException("Corrupted stream - invalid high tag number found");
				}

				while((b >= 0) && ((b & 0x80) != 0))
				{
					tagNo |= (b & 0x7f);
					tagNo <<= 7;
					b = s.ReadByte();
				}

				if(b < 0)
					throw new EndOfStreamException("EOF found inside tag value.");

				tagNo |= (b & 0x7f);
			}

			return tagNo;
		}

		public static int ReadLength(
			Stream s,
			int limit)
		{
			int length = s.ReadByte();
			if(length < 0)
				throw new EndOfStreamException("EOF found when length expected");

			if(length == 0x80)
				return -1;      // indefinite-length encoding

			if(length > 127)
			{
				int size = length & 0x7f;

				// Note: The invalid long form "0xff" (see X.690 8.1.3.5c) will be caught here
				if(size > 4)
					throw new IOException("DER length more than 4 bytes: " + size);

				length = 0;
				for(int i = 0; i < size; i++)
				{
					int next = s.ReadByte();

					if(next < 0)
						throw new EndOfStreamException("EOF found reading length");

					length = (length << 8) + next;
				}

				if(length < 0)
					throw new IOException("Corrupted stream - negative length found");

				if(length >= limit)   // after all we must have read at least 1 byte
					throw new IOException("Corrupted stream - out of bounds length found");
			}

			return length;
		}

		public static byte[] GetBuffer(DefiniteLengthInputStream defIn, byte[][] tmpBuffers)
		{
			int len = defIn.GetRemaining();
			if(len >= tmpBuffers.Length)
			{
				return defIn.ToArray();
			}

			byte[] buf = tmpBuffers[len];
			if(buf == null)
			{
				buf = tmpBuffers[len] = new byte[len];
			}

			defIn.ReadAllIntoByteArray(buf);

			return buf;
		}

		public static Asn1Object CreatePrimitiveDerObject(
			int tagNo,
			DefiniteLengthInputStream defIn,
			byte[][] tmpBuffers)
		{
			switch(tagNo)
			{
				case Asn1Tags.Boolean:
					throw new IOException("invalid ECDSA sig");
				case Asn1Tags.Enumerated:
					throw new IOException("invalid ECDSA sig");
				case Asn1Tags.ObjectIdentifier:
					throw new IOException("invalid ECDSA sig");
			}

			byte[] bytes = defIn.ToArray();

			switch(tagNo)
			{
				case Asn1Tags.Integer:
					return new DerInteger(bytes);
				default:
					throw new IOException("unknown tag " + tagNo + " encountered");
			}
		}
	}
}
