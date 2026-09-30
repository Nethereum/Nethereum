using System.Threading.Tasks;
using Nethereum.Chain.TestData.Vectors;
using Xunit;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class GenesisScaleTests
    {
        [Fact]
        public async Task ManyAccounts_FundedAtGenesis_AndFollowerMatches()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 100);

            Assert.Equal(108, sequencer.Accounts.All.Count);
            foreach (var account in sequencer.Accounts.All)
                Assert.True(await sequencer.State.AccountExistsAsync(account.Address), account.Address);

            var follower = await FollowerNode.CreateAsync(sequencer);
            Assert.Equal(
                await sequencer.Blocks.GetHashByNumberAsync(0),
                await follower.Blocks.GetHashByNumberAsync(0));
        }

        [Fact]
        public async Task ScaledWorkload_FollowerMatchesEveryStore()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 200);
            await new WorkloadV1().BuildAsync(sequencer);

            var follower = await FollowerNode.CreateAsync(sequencer);
            await follower.ConsumeAsync(sequencer);

            await Nethereum.Chain.TestData.ChainEquivalence.AssertEquivalentAsync(sequencer.Stores, follower.Stores);
            Assert.True((await sequencer.State.GetAllAccountsAsync()).Count > 200);
        }
    }
}
