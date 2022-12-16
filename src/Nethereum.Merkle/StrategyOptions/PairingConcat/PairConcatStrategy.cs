using System.Linq;

namespace Nethereum.Merkle.StrategyOptions.PairingConcat
{
    public class PairConcatStrategy : IPairConcatStrategy
    {
        public PairConcatStrategy()
        {

        }

        public byte[] Concat(byte[] left, byte[] right)
        {
            return left.Concat(right).ToArray();
        }
    }

}
