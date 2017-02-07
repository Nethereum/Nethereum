using Nethereum.ABI.Util;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.ENS
{
    public class EnsUtil
    {
        public string GetEnsLabelHash(string label)
        {
            var kecckak = new Sha3Keccack();
            return kecckak.CalculateHash(label);
        }

        public string GetEnsNameHash(string name)
        {
            var node = "0x0000000000000000000000000000000000000000000000000000000000000000";
            var kecckak = new Sha3Keccack();
            if (!string.IsNullOrEmpty(name))
            {
                var labels = name.Split('.');
                for (var i = labels.Length - 1; i >= 0; i--)
                {
                    var byteInput = (node + GetEnsLabelHash(labels[i])).HexToByteArray();
                    node = kecckak.CalculateHash(byteInput).ToHex();
                }
            }
            return node.EnsureHexPrefix();
        }
    }
}