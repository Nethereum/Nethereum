using System.Numerics;
using Nethereum.EVM;

namespace Nethereum.CoreChain
{
    /// <summary>
    /// Pre-Merge consensus block reward schedule for Ethereum:
    ///   - Frontier through Byzantium-exclusive: 5 ETH
    ///   - Byzantium through Constantinople-exclusive: 3 ETH
    ///   - Constantinople through Paris-exclusive: 2 ETH
    ///   - Paris (Merge) onwards: 0 ETH (block rewards moved to the beacon chain)
    /// Uncle reward = miner_reward * (8 + uncle_block_number - block_number) / 8
    /// and the miner also receives 1/32 of a full miner_reward per uncle included.
    /// </summary>
    public static class BlockRewardCalculator
    {
        public static readonly BigInteger FrontierReward = BigInteger.Parse("5000000000000000000"); // 5 ETH
        public static readonly BigInteger ByzantiumReward = BigInteger.Parse("3000000000000000000"); // 3 ETH
        public static readonly BigInteger ConstantinopleReward = BigInteger.Parse("2000000000000000000"); // 2 ETH
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

                // Paris (Merge) onwards: 0. PoS validators are rewarded by the
                // beacon chain, not by execution-layer block rewards.
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
