namespace NBitcoin.BouncyCastle.Math.EC.Endo
{
	public interface GlvEndomorphism
		: ECEndomorphism
	{
		BigInteger[] DecomposeScalar(BigInteger k);
	}
}
