using Nethereum.Util.HashProviders;

namespace Nethereum.Zisk.Core
{
    public class ZiskKeccakHashProvider : IHashProvider
    {
        public byte[] ComputeHash(byte[] data)
        {
            if (data == null) data = new byte[0];
            var output = new byte[32];
            ZiskCrypto.zkvm_keccak256(data, (nuint)data.Length, output);
            return output;
        }
    }
}
