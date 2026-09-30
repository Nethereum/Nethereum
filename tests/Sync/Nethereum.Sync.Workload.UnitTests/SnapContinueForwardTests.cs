using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Chain.TestData.Vectors;
using Xunit;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class SnapContinueForwardTests
    {
        private static BigInteger Eth(long n) => new BigInteger(n) * BigInteger.Pow(10, 18);

        [Fact]
        public async Task Follower_SnapsThenContinuesForward_OnRecoveredState()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 100);
            await new WorkloadV1().BuildAsync(sequencer);

            var pivot = (int)(await sequencer.Blocks.GetHeightAsync());
            var pivotHeader = await sequencer.Blocks.GetByNumberAsync(pivot);
            var pivotHash = await sequencer.Blocks.GetHashByNumberAsync(pivot);

            using var follower = await SnapFollowerNode.SnapAsync(sequencer, pivotHeader, pivotHash);

            sequencer.QueueTransfer(sequencer.Accounts.Alice, sequencer.Accounts.Bob.Address, Eth(1));
            await sequencer.ProduceBlockAsync();
            sequencer.QueueDeploy(sequencer.Accounts.Carol, WorkloadContracts.StorageLoggerBytecode);
            await sequencer.ProduceBlockAsync();
            sequencer.QueueTransfer(sequencer.Accounts.Dave, sequencer.Accounts.Erin.Address, Eth(2));
            await sequencer.ProduceBlockAsync();

            await follower.ForwardExecuteAsync(sequencer.ProducedBlockData.Skip(pivot));

            var tip = await sequencer.Blocks.GetHeightAsync();
            Assert.Equal(
                await sequencer.Blocks.GetHashByNumberAsync(tip),
                await follower.Blocks.GetHashByNumberAsync(tip));

            Assert.Equal(tip, (await follower.Diffs.GetNewestDiffBlockAsync()).Value);
            Assert.Equal((BigInteger)(pivot + 1), (await follower.Diffs.GetOldestDiffBlockAsync()).Value);
        }
    }
}
