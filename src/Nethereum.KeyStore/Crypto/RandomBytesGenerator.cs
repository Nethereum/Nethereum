using Org.BouncyCastle.Security;

namespace Nethereum.KeyStore.Crypto
{
    public class RandomBytesGenerator : IRandomBytesGenerator
    {
        private static readonly SecureRandom Random = new SecureRandom();

        public byte[] GenerateRandomInitialisationVector()
        {
            return GenerateRandomBytes(16);
        }

        public byte[] GeneratRandomPrivateKey()
        {
            return GenerateRandomBytes(32);
        }

        public byte[] GenerateRandomSalt()
        {
            return GenerateRandomBytes(32);
        }

        public byte[] GenerateRandomBytes(int size)
        {
            byte[] bytes = new byte[size];
            Random.NextBytes(bytes);
            return bytes;
        }
    }
}