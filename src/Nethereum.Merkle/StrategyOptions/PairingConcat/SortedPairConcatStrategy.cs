using Nethereum.Util;
using System;
using System.Linq;

namespace Nethereum.Merkle.StrategyOptions.PairingConcat
{
    public class SortedPairConcatStrategy : IPairConcatStrategy
    {
        private readonly ByteListComparer byteListComparer;
        public SortedPairConcatStrategy()
        {
            byteListComparer = new ByteListComparer();
        }

        public byte[] Concat(byte[] left, byte[] right)
        {
            var list = new[] { left.ToList(), right.ToList() };
            Array.Sort(list, byteListComparer);

            return list.First().Concat(list.Last()).ToArray();
        }
    }

}
