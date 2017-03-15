using Nethereum.Hex.HexConvertors.Extensions;
using Org.BouncyCastle.Crypto.Digests;
using System.Text;

namespace Nethereum.Util
{
    public class Sha3Keccack
    {
        public string CalculateHash(string value)
        {
            var input = Encoding.UTF8.GetBytes(value);
            var output = CalculateHash(input);
            return output.ToHex();
        }

        public byte[] CalculateHash(byte[] value)
        {
            var digest = new KeccakDigest(256);
            var output = new byte[digest.GetDigestSize()];
            digest.BlockUpdate(value, 0, value.Length);
            digest.DoFinal(output, 0);
            return output;
        }
    }
}