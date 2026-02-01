using Nethereum.AccountAbstraction.Bundler;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Bundler
{
    [Collection(BundlerTestFixture.BUNDLER_COLLECTION)]
    public class ReputationSystemTests
    {
        private readonly BundlerTestFixture _fixture;

        public ReputationSystemTests(BundlerTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GetReputation_ForNewAddress_ReturnsOkStatus()
        {
            var address = "0x" + new string('1', 40);

            var reputation = await _fixture.BundlerService.GetReputationAsync(address);

            Assert.NotNull(reputation);
            Assert.Equal(ReputationStatus.Ok, reputation.Status);
            Assert.Equal(0, reputation.OpsIncluded);
            Assert.Equal(0, reputation.OpsFailed);
        }

        [Fact]
        public async Task SetReputation_AndGetReputation_ReturnsSetValues()
        {
            var address = "0x" + new string('2', 40);

            var newReputation = new ReputationEntry
            {
                Address = address,
                OpsIncluded = 10,
                OpsFailed = 2,
                Status = ReputationStatus.Ok
            };

            await _fixture.BundlerService.SetReputationAsync(address, newReputation);

            var retrieved = await _fixture.BundlerService.GetReputationAsync(address);

            Assert.Equal(10, retrieved.OpsIncluded);
            Assert.Equal(2, retrieved.OpsFailed);
            Assert.Equal(ReputationStatus.Ok, retrieved.Status);
        }

        [Fact]
        public async Task SetReputation_ToThrottled_ReturnsThrottledStatus()
        {
            var address = "0x" + new string('3', 40);

            var throttledReputation = new ReputationEntry
            {
                Address = address,
                OpsIncluded = 5,
                OpsFailed = 10,
                Status = ReputationStatus.Throttled
            };

            await _fixture.BundlerService.SetReputationAsync(address, throttledReputation);

            var retrieved = await _fixture.BundlerService.GetReputationAsync(address);

            Assert.Equal(ReputationStatus.Throttled, retrieved.Status);
        }

        [Fact]
        public async Task SetReputation_ToBanned_ReturnsBannedStatus()
        {
            var address = "0x" + new string('4', 40);

            var bannedReputation = new ReputationEntry
            {
                Address = address,
                OpsIncluded = 2,
                OpsFailed = 50,
                Status = ReputationStatus.Banned
            };

            await _fixture.BundlerService.SetReputationAsync(address, bannedReputation);

            var retrieved = await _fixture.BundlerService.GetReputationAsync(address);

            Assert.Equal(ReputationStatus.Banned, retrieved.Status);
        }

        [Fact]
        public async Task GetReputation_IsCaseInsensitive()
        {
            var addressLower = "0x" + new string('a', 40);
            var addressUpper = "0x" + new string('A', 40);

            var reputation = new ReputationEntry
            {
                Address = addressLower,
                OpsIncluded = 15,
                OpsFailed = 0,
                Status = ReputationStatus.Ok
            };

            await _fixture.BundlerService.SetReputationAsync(addressLower, reputation);

            var retrieved = await _fixture.BundlerService.GetReputationAsync(addressUpper);

            Assert.Equal(15, retrieved.OpsIncluded);
        }

        [Fact]
        public async Task SetReputation_UpdatesExistingEntry()
        {
            var address = "0x" + new string('5', 40);

            var initial = new ReputationEntry
            {
                Address = address,
                OpsIncluded = 5,
                OpsFailed = 1,
                Status = ReputationStatus.Ok
            };

            await _fixture.BundlerService.SetReputationAsync(address, initial);

            var updated = new ReputationEntry
            {
                Address = address,
                OpsIncluded = 10,
                OpsFailed = 3,
                Status = ReputationStatus.Throttled
            };

            await _fixture.BundlerService.SetReputationAsync(address, updated);

            var retrieved = await _fixture.BundlerService.GetReputationAsync(address);

            Assert.Equal(10, retrieved.OpsIncluded);
            Assert.Equal(3, retrieved.OpsFailed);
            Assert.Equal(ReputationStatus.Throttled, retrieved.Status);
        }

        [Fact]
        public async Task ReputationEntry_PreservesAddress()
        {
            var address = "0xabcdef1234567890abcdef1234567890abcdef12";

            var reputation = new ReputationEntry
            {
                Address = address,
                OpsIncluded = 1,
                OpsFailed = 0,
                Status = ReputationStatus.Ok
            };

            await _fixture.BundlerService.SetReputationAsync(address, reputation);

            var retrieved = await _fixture.BundlerService.GetReputationAsync(address);

            Assert.Equal(address, retrieved.Address);
        }

        [Fact]
        public async Task MultipleAddresses_MaintainIndependentReputations()
        {
            var address1 = "0x" + new string('6', 40);
            var address2 = "0x" + new string('7', 40);
            var address3 = "0x" + new string('8', 40);

            await _fixture.BundlerService.SetReputationAsync(address1, new ReputationEntry
            {
                Address = address1,
                OpsIncluded = 100,
                OpsFailed = 0,
                Status = ReputationStatus.Ok
            });

            await _fixture.BundlerService.SetReputationAsync(address2, new ReputationEntry
            {
                Address = address2,
                OpsIncluded = 10,
                OpsFailed = 5,
                Status = ReputationStatus.Throttled
            });

            await _fixture.BundlerService.SetReputationAsync(address3, new ReputationEntry
            {
                Address = address3,
                OpsIncluded = 1,
                OpsFailed = 20,
                Status = ReputationStatus.Banned
            });

            var rep1 = await _fixture.BundlerService.GetReputationAsync(address1);
            var rep2 = await _fixture.BundlerService.GetReputationAsync(address2);
            var rep3 = await _fixture.BundlerService.GetReputationAsync(address3);

            Assert.Equal(ReputationStatus.Ok, rep1.Status);
            Assert.Equal(100, rep1.OpsIncluded);

            Assert.Equal(ReputationStatus.Throttled, rep2.Status);
            Assert.Equal(10, rep2.OpsIncluded);

            Assert.Equal(ReputationStatus.Banned, rep3.Status);
            Assert.Equal(1, rep3.OpsIncluded);
        }

        [Fact]
        public void ReputationStatus_HasExpectedValues()
        {
            Assert.Equal(0, (int)ReputationStatus.Ok);
            Assert.Equal(1, (int)ReputationStatus.Throttled);
            Assert.Equal(2, (int)ReputationStatus.Banned);
        }
    }
}
