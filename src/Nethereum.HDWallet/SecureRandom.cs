using NBitcoin;

namespace Nethereum.HdWallet
{
    public class SecureRandom : IRandom
    {
        private static readonly Org.BouncyCastle.Security.SecureRandom SecureRandomInstance =
            new Org.BouncyCastle.Security.SecureRandom();

        public void GetBytes(byte[] output)
        {
            SecureRandomInstance.NextBytes(output);
        }

#if NETCOREAPP2_1 || NETCOREAPP3_1
        public void GetBytes(System.Span<byte> output)
        {
            SecureRandomInstance.NextBytes(output);
        }
#endif

    }
}
