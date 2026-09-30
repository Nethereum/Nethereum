using System.Numerics;
using Nethereum.EVM;

namespace Nethereum.CoreChain
{
    public static class BlockRewardCalculator
    {
        public static readonly BigInteger FrontierReward = BigInteger.Parse("5000000000000000000");
        public static readonly BigInteger ByzantiumReward = BigInteger.Parse("3000000000000000000");
        public static readonly BigInteger ConstantinopleReward = BigInteger.Parse("2000000000000000000");
        public static readonly BigInteger NoReward = BigInteger.Zero;

        public static BigInteger MinerReward(HardforkName activeFork)
        {
            return activeFork switch
            {
                HardforkName.Frontier or
                HardforkName.FrontierThawing or
                HardforkName.Homestead or
                HardforkName.DaoFork or
                HardforkName.TangerineWhistle or
                HardforkName.SpuriousDragon => FrontierReward,

                HardforkName.Byzantium => ByzantiumReward,

                HardforkName.Constantinople or
                HardforkName.Petersburg or
                HardforkName.Istanbul or
                HardforkName.MuirGlacier or
                HardforkName.Berlin or
                HardforkName.London or
                HardforkName.ArrowGlacier or
                HardforkName.GrayGlacier => ConstantinopleReward,

                _ => NoReward
            };
        }

        public static BigInteger UncleReward(BigInteger minerReward, ulong uncleBlockNumber, ulong currentBlockNumber)
        {
            if (minerReward.IsZero) return BigInteger.Zero;
            var depth = (long)currentBlockNumber - (long)uncleBlockNumber;
            if (depth < 1 || depth > 7) return BigInteger.Zero;
            return minerReward * (8 - depth) / 8;
        }

        public static BigInteger MinerUncleInclusionReward(BigInteger minerReward)
        {
            return minerReward / 32;
        }
    }
}
