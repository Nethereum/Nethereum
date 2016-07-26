namespace NBitcoin.BouncyCastle.Math.Field
{
	public interface IPolynomialExtensionField
		: IExtensionField
	{
		IPolynomial MinimalPolynomial
		{
			get;
		}
	}
}
