using System.Security.Cryptography;

namespace Nethereum.Util.HashProviders
{
    public class Sha256HashProvider : IHashProvider
    {
        public byte[] ComputeHash(byte[] data)
        {
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(data);
            }
        }
    }
}
