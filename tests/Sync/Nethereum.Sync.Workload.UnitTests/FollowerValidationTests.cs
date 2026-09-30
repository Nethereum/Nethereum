using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Chain.TestData.Vectors;
using Xunit;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class FollowerValidationTests
    {
        [Fact]
        public async Task Follower_ReExecutesWorkloadV1_AndMatchesSequencer()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync();
            await new WorkloadV1().BuildAsync(sequencer);

            var follower = await FollowerNode.CreateAsync(sequencer);
            await follower.ConsumeAsync(sequencer);

            await Nethereum.Chain.TestData.ChainEquivalence.AssertEquivalentAsync(sequencer.Stores, follower.Stores);
            Assert.True((int)(await sequencer.Blocks.GetHeightAsync()) >= 4);
        }
    }
}
