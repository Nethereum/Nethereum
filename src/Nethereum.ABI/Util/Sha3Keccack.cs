using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Org.BouncyCastle.Crypto.Digests;

namespace Nethereum.ABI.Util
{
    public class Sha3Keccack
    {
        public string CalculateHash(string value)
        {
            var digest = new KeccakDigest(256);
            var output = new byte[digest.GetDigestSize()];
            var input = Encoding.UTF8.GetBytes(value);
            digest.BlockUpdate(input, 0, input.Length);
            digest.DoFinal(output, 0);
            return output.ToHex();
        }
    }
}
