namespace NBitcoin.BouncyCastle.Asn1
{
	public class DerOctetString
		: Asn1OctetString
	{
		/// <param name="str">The octets making up the octet string.</param>
		public DerOctetString(
			byte[] str)
			: base(str)
		{
		}

		public override void Encode(
			DerOutputStream derOut)
		{
			derOut.WriteEncoded(Asn1Tags.OctetString, str);
		}

		public static void Encode(
			DerOutputStream derOut,
			byte[] bytes,
			int offset,
			int length)
		{
			derOut.WriteEncoded(Asn1Tags.OctetString, bytes, offset, length);
		}
	}
}
