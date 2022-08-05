using System;
using System.Globalization;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.Contracts.Standards.ENS
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
#if !DOTNET35 && !NETSTANDARD1_1           
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
#else
            throw new Exception("GetNameHash unsupported for the current .net version");
#endif
        }

        public string Normalise(string name)
        {
#if !DOTNET35 && !NETSTANDARD1_1
            try
            {
                var idn = new IdnMapping
                {
                    UseStd3AsciiRules = true,
                    AllowUnassigned = true
                };
                var puny = idn.GetAscii(name).ToLower();
                return idn.GetUnicode(puny);

            }
            catch (Exception ex)
            {
                throw new ArgumentOutOfRangeException("Invalid ENS name", ex);
            }
#else
            throw new Exception("Normalise unsupported for the current .net version");
#endif
        }
    }
}