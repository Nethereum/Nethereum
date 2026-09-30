using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Reputation;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Reputation
{
    public class ReputationServiceOpsSeenTests
    {
        private const string Address = "0x1111111111111111111111111111111111111111";

        [Fact]
        public async Task RecordSeenAsync_OnAcceptedOp_IncrementsOpsSeenByOne()
        {
            var service = new InMemoryReputationService();

            await service.RecordSeenAsync(Address, 1);

            var entry = await service.GetAsync(Address);
            Assert.NotNull(entry);
            Assert.Equal(1, entry!.OpsSeen);
        }

        [Fact]
        public async Task RecordSeenAsync_AccumulatesAcrossMultipleAcceptedOps()
        {
            var service = new InMemoryReputationService();

            await service.RecordSeenAsync(Address, 1);
            await service.RecordSeenAsync(Address, 1);
            await service.RecordSeenAsync(Address, 1);

            var entry = await service.GetAsync(Address);
            Assert.Equal(3, entry!.OpsSeen);
        }

        [Fact]
        public async Task RecordSeenAsync_NegativeDelta_RevertsAPreviousAccept()
        {
            var service = new InMemoryReputationService();
            await service.RecordSeenAsync(Address, 1);

            await service.RecordSeenAsync(Address, -1);

            var entry = await service.GetAsync(Address);
            Assert.Equal(0, entry!.OpsSeen);
        }

        [Fact]
        public async Task RecordSeenAsync_NegativeDelta_FloorsAtZero()
        {
            var service = new InMemoryReputationService();

            await service.RecordSeenAsync(Address, -1);

            var entry = await service.GetAsync(Address);
            Assert.Equal(0, entry!.OpsSeen);
        }

        [Fact]
        public async Task ApplyStakedAccountabilityPenalty_AddsBanPenaltyAndResetsOpsIncluded()
        {
            var service = new InMemoryReputationService();
            await service.RecordSeenAsync(Address, 1);
            await service.RecordIncludedAsync(Address);

            await service.ApplyStakedAccountabilityPenaltyAsync(Address);

            var entry = await service.GetAsync(Address);
            Assert.True(entry!.OpsSeen >= 10000, $"expected opsSeen >= 10000, got {entry.OpsSeen}");
            Assert.Equal(0, entry.OpsIncluded);
            Assert.Equal(ReputationStatus.Banned, entry.Status);
        }

        [Theory]
        [InlineData(0, 0, ReputationStatus.Ok)]
        [InlineData(109, 0, ReputationStatus.Ok)]
        [InlineData(110, 0, ReputationStatus.Throttled)]
        [InlineData(509, 0, ReputationStatus.Throttled)]
        [InlineData(510, 0, ReputationStatus.Banned)]
        public void ComputeStatus_MatchesReferenceThresholds(int opsSeen, int opsIncluded, ReputationStatus expected)
        {
            var config = new ReputationConfig();

            var status = ReputationStatusCalculator.Compute(opsSeen, opsIncluded, config);

            Assert.Equal(expected, status);
        }

        [Fact]
        public async Task RecordIncludedAsync_DoesNotChangeOpsSeen()
        {
            var service = new InMemoryReputationService();
            await service.RecordSeenAsync(Address, 1);

            await service.RecordIncludedAsync(Address);

            var entry = await service.GetAsync(Address);
            Assert.Equal(1, entry!.OpsSeen);
            Assert.Equal(1, entry.OpsIncluded);
        }
    }
}
