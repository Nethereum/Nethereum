using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Reputation;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Reputation
{
    public class ReputationDecayTests
    {
        private const string Address = "0x1111111111111111111111111111111111111111";

        [Fact]
        public async Task DecayAsync_DecaysOpsSeenAndOpsIncludedBy23Over24()
        {
            var service = new InMemoryReputationService();
            await service.RecordSeenAsync(Address, 240);
            for (int i = 0; i < 48; i++)
            {
                await service.RecordIncludedAsync(Address);
            }

            await service.DecayAsync();

            var entry = await service.GetAsync(Address);
            Assert.NotNull(entry);
            Assert.Equal(230, entry!.OpsSeen);
            Assert.Equal(46, entry.OpsIncluded);
        }

        [Fact]
        public async Task DecayAsync_RepeatedTicks_ConvergeTowardZeroAndDropTheEntry()
        {
            var service = new InMemoryReputationService();
            await service.RecordSeenAsync(Address, 5);

            for (int i = 0; i < 200; i++)
            {
                await service.DecayAsync();
            }

            Assert.Null(await service.GetAsync(Address));
            Assert.Empty(await service.GetAllAsync());
        }

        [Fact]
        public async Task DecayAsync_DoesNotDropAnEntryThatStillHasOpsIncluded()
        {
            var service = new InMemoryReputationService();
            await service.RecordSeenAsync(Address, 1);
            for (int i = 0; i < 100; i++)
            {
                await service.RecordIncludedAsync(Address);
            }

            await service.DecayAsync();

            var entry = await service.GetAsync(Address);
            Assert.NotNull(entry);
            Assert.Equal(0, entry!.OpsSeen);
            Assert.Equal(95, entry.OpsIncluded);
        }

        [Fact]
        public async Task DecayAsync_ReducesABanBackTowardOk()
        {
            var service = new InMemoryReputationService();
            await service.RecordSeenAsync(Address, 510);
            var before = await service.GetAsync(Address);
            Assert.Equal(ReputationStatus.Banned, before!.Status);

            await service.DecayAsync();

            var after = await service.GetAsync(Address);
            Assert.NotNull(after);
            Assert.Equal(488, after!.OpsSeen);
            Assert.NotEqual(ReputationStatus.Banned, after.Status);
            Assert.False(await service.IsBannedAsync(Address));
        }

        [Fact]
        public void ReputationDecayCalculator_FloorsAndReportsDropAtAllZero()
        {
            var config = new ReputationConfig();

            var decaying = new ReputationEntry { Address = Address, OpsSeen = 47, OpsIncluded = 24 };
            var dropDecaying = ReputationDecayCalculator.Decay(decaying, config);
            Assert.Equal(45, decaying.OpsSeen);
            Assert.Equal(23, decaying.OpsIncluded);
            Assert.False(dropDecaying);

            var zeroing = new ReputationEntry { Address = Address, OpsSeen = 0, OpsIncluded = 0 };
            Assert.True(ReputationDecayCalculator.Decay(zeroing, config));
        }
    }
}
