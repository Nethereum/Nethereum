using Nethereum.ABI.Util;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System;
using System.Globalization;

namespace Nethereum.ENS
{
    public class EnsUtil
    {
        public string GetLabelHash(string label)
        {
            var kecckak = new Sha3Keccack();
            return kecckak.CalculateHash(label);
        }

        public string GetNameHash(string name)
        {
            
            var node = "0x0000000000000000000000000000000000000000000000000000000000000000";
            var kecckak = new Sha3Keccack();
            if (!string.IsNullOrEmpty(name))
            {
                name = Normalise(name);
                var labels = name.Split('.');
                for (var i = labels.Length - 1; i >= 0; i--)
                {
                    var byteInput = (node + GetLabelHash(labels[i])).HexToByteArray();
                    node = kecckak.CalculateHash(byteInput).ToHex();
                }
            }
            return node.EnsureHexPrefix();
        }

        public string Normalise(string name)
        {
            try
            {
                var idn = new IdnMapping
                {
                    UseStd3AsciiRules = true
                };
                return idn.GetAscii(name).ToLower();
                
            }
            catch (Exception ex)
            {
                throw new ArgumentOutOfRangeException("Invalid ENS name", ex);
            }
        }
    }
}