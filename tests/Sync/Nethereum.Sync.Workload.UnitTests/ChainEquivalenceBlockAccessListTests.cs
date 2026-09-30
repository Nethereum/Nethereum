using System.Linq;
using System.Threading.Tasks;
using Nethereum.Chain.TestData.Vectors;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class ChainEquivalenceBlockAccessListTests
    {
        [Fact]
        public async Task Given_TwoChainsThatDifferOnlyInABlockAccessList_When_TheyAreComparedForEquivalence_Then_TheComparatorReportsTheDifference()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(hardfork: "amsterdam");
            await new WorkloadV1().BuildAsync(sequencer);

            var follower = await FollowerNode.CreateAsync(sequencer);
            await follower.ConsumeAsync(sequencer);

            var baseline = await ChainEquivalence.CompareAllAsync(sequencer.Stores, follower.Stores);
            Assert.Empty(baseline.Differences);

            var height = (long)await sequencer.Blocks.GetHeightAsync();
            var tamperedBlock = await FindBlockWithNonEmptyBlockAccessListAsync(follower, height);
            Assert.True(tamperedBlock > 0, "no block carried a non-empty block access list to tamper with");

            var blockHash = await follower.Blocks.GetHashByNumberAsync(tamperedBlock);
            var rlp = await follower.BlockAccessLists.GetByBlockHashAsync(blockHash);
            var decoded = BlockAccessListRLPEncoder.Current.Decode(rlp);
            decoded[0].BalanceChanges.Add(new BalanceChange(9_999, new EvmUInt256(123_456)));
            await follower.BlockAccessLists.SaveAsync(blockHash, BlockAccessListRLPEncoder.Current.Encode(decoded));

            var result = await ChainEquivalence.CompareAllAsync(sequencer.Stores, follower.Stores);
            Assert.Contains(result.Differences, d => d.Contains($"block {tamperedBlock}") && d.Contains("balance change"));
        }

        private static async Task<long> FindBlockWithNonEmptyBlockAccessListAsync(FollowerNode follower, long height)
        {
            for (var n = 1L; n <= height; n++)
            {
                var hash = await follower.Blocks.GetHashByNumberAsync(n);
                var rlp = await follower.BlockAccessLists.GetByBlockHashAsync(hash);
                if (rlp == null) continue;
                if (BlockAccessListRLPEncoder.Current.Decode(rlp).Any(a => a.BalanceChanges.Count > 0))
                    return n;
            }
            return -1;
        }
    }
}
