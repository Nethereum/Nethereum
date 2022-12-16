using System;

namespace Nethereum.Merkle.StrategyOptions.PairingConcat
{
    public static class PairingConcatFactory
    {
        public static IPairConcatStrategy GetPairConcatStrategy(PairingConcatType type)
        {
            switch (type)
            {
                case PairingConcatType.Normal:
                    return new PairConcatStrategy();
                case PairingConcatType.Sorted:
                    return new SortedPairConcatStrategy();
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }

}
