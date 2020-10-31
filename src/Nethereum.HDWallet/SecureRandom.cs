using NBitcoin;

namespace Nethereum.HdWallet
{
#if !(NETCOREAPP2_1 || NETCOREAPP3_1 || NETSTANDARD2_0)
    public class SecureRandom : IRandom
    {
        public Org.BouncyCastle.Security.SecureRandom SecureRandomInstance =
            new Org.BouncyCastle.Security.SecureRandom();

        public SecureRandom(Org.BouncyCastle.Security.SecureRandom secureRandom = null)
        {
            if(secureRandom != null)
            {
                SecureRandomInstance = secureRandom;
            }
        }

        public void GetBytes(byte[] output)
        {
            SecureRandomInstance.NextBytes(output);
        }
    }
#endif

}
