using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.EVM
{
    public static class LogBloomCalculator
    {
        private static readonly Sha3Keccack Keccak = new Sha3Keccack();

        public static byte[] CalculateBloom(List<Log> logs)
        {
            var bloom = new byte[256];
            if (logs == null) return bloom;

            foreach (var log in logs)
            {
                AddToBloom(bloom, log.Address.HexToByteArray());
                if (log.Topics != null)
                {
                    foreach (var topic in log.Topics)
                        AddToBloom(bloom, topic);
                }
            }

            return bloom;
        }

        public static void CombineBloom(byte[] target, byte[] source)
        {
            if (source == null) return;
            for (int i = 0; i < 256 && i < target.Length && i < source.Length; i++)
                target[i] |= source[i];
        }

        private static void AddToBloom(byte[] bloom, byte[] data)
        {
            var hash = Keccak.CalculateHash(data);
            for (int i = 0; i < 6; i += 2)
            {
                var bit = ((hash[i] & 0x07) << 8) + hash[i + 1];
                bit = bit & 0x7FF;
                var byteIndex = 255 - (bit / 8);
                var bitIndex = bit % 8;
                bloom[byteIndex] |= (byte)(1 << bitIndex);
            }
        }
    }
}
