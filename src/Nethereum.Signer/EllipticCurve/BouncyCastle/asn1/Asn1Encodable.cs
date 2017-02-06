namespace NBitcoin.BouncyCastle.Asn1
{
	public abstract class Asn1Encodable
		: IAsn1Convertible
	{
		public const string Der = "DER";
		public const string Ber = "BER";

		public sealed override int GetHashCode()
		{
			return ToAsn1Object().CallAsn1GetHashCode();
		}

		public sealed override bool Equals(
			object obj)
		{
			if(obj == this)
				return true;

			IAsn1Convertible other = obj as IAsn1Convertible;

			if(other == null)
				return false;

			Asn1Object o1 = ToAsn1Object();
			Asn1Object o2 = other.ToAsn1Object();

			return o1 == o2 || o1.CallAsn1Equals(o2);
		}

		public abstract Asn1Object ToAsn1Object();
	}
}
