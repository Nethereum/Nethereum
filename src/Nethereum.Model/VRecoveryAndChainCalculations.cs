using Nethereum.RLP;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Nethereum.Model
{
    public static class VRecoveryAndChainCalculations
    {
        public static int GetRecIdFromV(byte[] v)
        {
            return GetRecIdFromV(v[0]);
        }

        public static bool IsEthereumV(int v)
        {
            return v == 27 || v == 28;
        }

        public static int GetRecIdFromV(byte v)
        {
            var header = v;
            // The header byte: 0x1B = first key with even y, 0x1C = first key with odd y,
            //                  0x1D = second key with even y, 0x1E = second key with odd y
            if (header < 27 || header > 34)
                throw new Exception("Header byte out of range: " + header);
            if (header >= 31)
                header -= 4;
            return header - 27;
        }

        public static int GetRecIdFromVChain(BigInteger vChain, BigInteger chainId)
        {
            return (int)(vChain - chainId * 2 - 35);
        }

        public static BigInteger GetChainFromVChain(BigInteger vChain)
        {
            var start = vChain - 35;
            var even = start % 2 == 0;
            if (even) return start / 2;
            return (start - 1) / 2;
        }

        public static int GetRecIdFromVChain(byte[] vChain, BigInteger chainId)
        {
            return GetRecIdFromVChain(vChain.ToBigIntegerFromRLPDecoded(), chainId);
        }

        public static BigInteger CalculateV(BigInteger chainId, int recId)
        {
            return chainId * 2 + recId + 35;
        }
    }
}
