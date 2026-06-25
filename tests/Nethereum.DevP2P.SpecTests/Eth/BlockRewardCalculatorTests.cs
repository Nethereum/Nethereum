using Nethereum.CoreChain;
using System.Numerics;
using Nethereum.AppChain.Sync;
using Nethereum.EVM;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Eth
{
    /// <summary>
    /// Spec tests for BlockRewardCalculator. Canonical reward schedule from
    /// the Yellow Paper Section 11.3 (with Byzantium/EIP-649 + Constantinople/EIP-1234
    /// updates) and uncle-reward formula:
    ///   uncle_reward = miner_reward * (8 + nU - nB) / 8
    /// where nU is the uncle's block number and nB is the including block's
    /// number. Miner also receives 1/32 of a full reward per included uncle.
    /// </summary>
    public class BlockRewardCalculatorTests
    {
        [Theory]
        [InlineData(HardforkName.Frontier,         "5000000000000000000")]
        [InlineData(HardforkName.Homestead,        "5000000000000000000")]
        [InlineData(HardforkName.DaoFork,          "5000000000000000000")]
        [InlineData(HardforkName.TangerineWhistle, "5000000000000000000")]
        [InlineData(HardforkName.SpuriousDragon,   "5000000000000000000")]
        [InlineData(HardforkName.Byzantium,        "3000000000000000000")]
        [InlineData(HardforkName.Constantinople,   "2000000000000000000")]
        [InlineData(HardforkName.Petersburg,       "2000000000000000000")]
        [InlineData(HardforkName.Istanbul,         "2000000000000000000")]
        [InlineData(HardforkName.MuirGlacier,      "2000000000000000000")]
        [InlineData(HardforkName.Berlin,           "2000000000000000000")]
        [InlineData(HardforkName.London,           "2000000000000000000")]
        [InlineData(HardforkName.ArrowGlacier,     "2000000000000000000")]
        [InlineData(HardforkName.GrayGlacier,      "2000000000000000000")]
        [InlineData(HardforkName.Paris,            "0")]
        [InlineData(HardforkName.Shanghai,         "0")]
        [InlineData(HardforkName.Cancun,           "0")]
        [InlineData(HardforkName.Prague,           "0")]
        public void MinerReward_MatchesCanonicalSchedule(HardforkName fork, string expectedWei)
        {
            Assert.Equal(BigInteger.Parse(expectedWei), BlockRewardCalculator.MinerReward(fork));
        }

        [Theory]
        [InlineData(100, 99,  "4375000000000000000")]  // 5 * 7/8 = 4.375 ETH (Frontier, immediate uncle)
        [InlineData(100, 98,  "3750000000000000000")]  // 5 * 6/8 = 3.75 ETH
        [InlineData(100, 93,  "625000000000000000")]   // 5 * 1/8 = 0.625 ETH (depth 7, deepest valid)
        [InlineData(100, 92,  "0")]                    // depth 8 — too old
        [InlineData(100, 101, "0")]                    // future block — invalid
        public void UncleReward_FrontierMiner_MatchesFormula(ulong blockNumber, ulong uncleBlockNumber, string expectedWei)
        {
            var minerReward = BlockRewardCalculator.MinerReward(HardforkName.Frontier);
            Assert.Equal(BigInteger.Parse(expectedWei),
                         BlockRewardCalculator.UncleReward(minerReward, uncleBlockNumber, blockNumber));
        }

        [Fact]
        public void UncleReward_PostMerge_IsZero()
        {
            Assert.Equal(BigInteger.Zero,
                         BlockRewardCalculator.UncleReward(BlockRewardCalculator.MinerReward(HardforkName.Paris), 99, 100));
        }

        [Theory]
        [InlineData(HardforkName.Frontier,       "156250000000000000")]  // 5 / 32 ETH
        [InlineData(HardforkName.Byzantium,      "93750000000000000")]   // 3 / 32 ETH
        [InlineData(HardforkName.Constantinople, "62500000000000000")]   // 2 / 32 ETH
        [InlineData(HardforkName.Paris,          "0")]
        public void MinerUncleInclusionReward_MatchesFormula(HardforkName fork, string expectedWei)
        {
            var minerReward = BlockRewardCalculator.MinerReward(fork);
            Assert.Equal(BigInteger.Parse(expectedWei),
                         BlockRewardCalculator.MinerUncleInclusionReward(minerReward));
        }
    }
}
