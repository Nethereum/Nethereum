using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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

        public string DnsEncode(string name)
        {
            var labels = name.Split('.');
            var totalEncoded = new List<byte>();
            foreach (var label in labels)
            {
                var labelBytes = Encoding.UTF8.GetBytes(Normalise(label));
                if(labelBytes.Length > 63)
                {
                    throw new Exception("Invalid DNS encoded entry; length exceeds 63 bytes");
                }

                var labelBytesEncoded = new byte[labelBytes.Length + 1];
                Array.Copy(labelBytes, 0, labelBytesEncoded, 1, labelBytes.Length);
                labelBytesEncoded[0] = (byte)labelBytes.Length;
                totalEncoded.AddRange(labelBytesEncoded);
            }
            return totalEncoded.ToArray().ToHex(true) + "00";
        }

        public string GetParent(string name)
        {
            if (name == null) return null;
            name = name.Trim();
            if (name == "." || !name.Contains(".")) return null;
            return name.Substring(name.IndexOf(".") + 1);
        }

    }
}