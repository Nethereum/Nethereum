using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ADRaffy.ENSNormalize;
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
            return ENSNormalize.ENSIP15.Normalize(name);
        }

        public NormDetails NormaliseDetails(string name)
        {
            return ENSNormalize.ENSIP15.NormalizeDetails(name);
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