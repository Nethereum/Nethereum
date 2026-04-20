using Nethereum.Util.HashProviders;

namespace Nethereum.Zisk.Core
{
    public class ZiskSha256HashProvider : IHashProvider
    {
        public byte[] ComputeHash(byte[] data)
        {
            if (data == null) data = new byte[0];
            var output = new byte[32];
            ZiskCrypto.sha256_c(data, (nuint)data.Length, output);
            return output;
        }
    }
}
