using System;
using System.Collections.Generic;

namespace Nethereum.ABI.FunctionEncoding
{
    public class ByteCodeSwarmExtractor
    {
        public const string bzzr0_Hex = "627a7a7230";
        public const string Prefix_bzzr0_Hex = "a165";
        public const string Suffix_bzzr0_Hex = "5820";
        public const string Prefix_Address_Hex = Prefix_bzzr0_Hex + bzzr0_Hex + Suffix_bzzr0_Hex;
        public const string Suffix_Address_Hex = "0029";

        public string GetSwarmAddress(string byteCode)
        {
            var firstPrefix = byteCode.IndexOf(Prefix_Address_Hex, StringComparison.Ordinal);
            if (firstPrefix < 0)
                return null;

            var endings = new List<int>();
            var hexRun = 0;
            for (var i = 0; i < byteCode.Length; i++)
            {
                if (hexRun >= 64 && string.CompareOrdinal(byteCode, i, Suffix_Address_Hex, 0, 4) == 0)
                    endings.Add(i);
                hexRun = IsHex(byteCode[i]) ? hexRun + 1 : 0;
            }

            var lineEnd = -1;
            for (var prefix = firstPrefix; prefix >= 0;
                 prefix = byteCode.IndexOf(Prefix_Address_Hex, prefix + 1, StringComparison.Ordinal))
            {
                var start = prefix + Prefix_Address_Hex.Length;
                if (lineEnd < start)
                {
                    lineEnd = byteCode.IndexOf((char)10, start);
                    if (lineEnd < 0) lineEnd = byteCode.Length;
                }

                var index = endings.BinarySearch(lineEnd);
                if (index < 0) index = ~index - 1;
                if (index >= 0 && endings[index] >= start + 64)
                    return byteCode.Substring(start, endings[index] - start);
            }

            return null;
        }

        private static bool IsHex(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        private string GetSwarmPaddedString(string byteCode)
        {
            return Prefix_Address_Hex + GetSwarmAddress(byteCode) + Suffix_Address_Hex;
        }

        public string GetByteCodeIncludingSwarmAddressPart(string byteCode)
        {
            var paddedSwarm = GetSwarmPaddedString(byteCode);
            return byteCode.Substring(0, byteCode.IndexOf(paddedSwarm) + paddedSwarm.Length);
        }

        public string GetByteCodeExludingSwarmAddressPart(string byteCode)
        {
            var paddedSwarm = GetSwarmPaddedString(byteCode);
            return byteCode.Substring(0, byteCode.IndexOf(paddedSwarm));
        }

        public bool HasSwarmAddress(string byteCode)
        {
            return GetSwarmAddress(byteCode) != null;
        }
    }
}