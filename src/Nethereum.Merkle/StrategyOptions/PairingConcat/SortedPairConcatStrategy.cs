using Nethereum.Util;
using System;
using System.Linq;

namespace Nethereum.Merkle.StrategyOptions.PairingConcat
{
    public class SortedPairConcatStrategy : IPairConcatStrategy
    {
        private readonly ByteArrayComparer byteComparer;
        public SortedPairConcatStrategy()
        {
            byteComparer = new ByteArrayComparer();
        }

        public byte[] Concat(byte[] left, byte[] right)
        {
            var list = new[] { left, right };
            Array.Sort(list, byteComparer);

            return list.First().Concat(list.Last()).ToArray();
        }
    }

}
