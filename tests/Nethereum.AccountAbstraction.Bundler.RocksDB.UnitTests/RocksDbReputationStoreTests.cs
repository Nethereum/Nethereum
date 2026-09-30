using Nethereum.AccountAbstraction.Bundler.Reputation;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Stores;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RocksDB.UnitTests
{
    public class RocksDbReputationStoreTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly BundlerRocksDbManager _manager;
        private readonly RocksDbReputationStore _store;
        private readonly ReputationConfig _config;

        public RocksDbReputationStoreTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"bundler_rep_test_{Guid.NewGuid():N}");
            var options = new BundlerRocksDbOptions { DatabasePath = _testDbPath };
            _config = new ReputationConfig();
            _manager = new BundlerRocksDbManager(options);
            _store = new RocksDbReputationStore(_manager, _config);
        }

        public void Dispose()
        {
            _manager.Dispose();
            if (Directory.Exists(_testDbPath))
            {
                Directory.Delete(_testDbPath, true);
            }
        }

        [Fact]
        public async Task RecordIncludedAsync_CreatesAndUpdatesEntry()
        {
            var address = "0x1111111111111111111111111111111111111111";

            await _store.RecordIncludedAsync(address);
            await _store.RecordIncludedAsync(address);

            var entry = await _store.GetAsync(address);
            Assert.NotNull(entry);
            Assert.Equal(2, entry.OpsIncluded);
            Assert.Equal(0, entry.OpsFailed);
            Assert.Equal(ReputationStatus.Ok, entry.Status);
        }

        [Fact]
        public async Task RecordFailedAsync_AloneDoesNotChangeStatus()
        {
            var address = "0x2222222222222222222222222222222222222222";

            for (int i = 0; i < 20; i++)
            {
                await _store.RecordFailedAsync(address);
            }

            var entry = await _store.GetAsync(address);
            Assert.NotNull(entry);
            Assert.Equal(20, entry.OpsFailed);
            Assert.Equal(ReputationStatus.Ok, entry.Status);
        }

        [Fact]
        public async Task RecordSeenAsync_ThrottlesWhenExpectedInclusionExceedsThrottlingSlack()
        {
            var address = "0x2222222222222222222222222222222222222222";

            await _store.RecordSeenAsync(address, 110);

            var entry = await _store.GetAsync(address);
            Assert.NotNull(entry);
            Assert.Equal(110, entry.OpsSeen);
            Assert.Equal(ReputationStatus.Throttled, entry.Status);
            Assert.NotNull(entry.ThrottledUntil);
        }

        [Fact]
        public async Task RecordSeenAsync_BansWhenExpectedInclusionExceedsBanSlack()
        {
            var address = "0x3333333333333333333333333333333333333333";

            await _store.RecordSeenAsync(address, 510);

            var entry = await _store.GetAsync(address);
            Assert.NotNull(entry);
            Assert.Equal(510, entry.OpsSeen);
            Assert.Equal(ReputationStatus.Banned, entry.Status);
            Assert.NotNull(entry.BannedUntil);
        }

        [Fact]
        public async Task ApplyStakedAccountabilityPenaltyAsync_BansEntityAndResetsOpsIncluded()
        {
            var address = "0x2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f";
            await _store.RecordSeenAsync(address, 1);
            await _store.RecordIncludedAsync(address);

            await _store.ApplyStakedAccountabilityPenaltyAsync(address);

            var entry = await _store.GetAsync(address);
            Assert.NotNull(entry);
            Assert.True(entry.OpsSeen >= 10000, $"expected opsSeen >= 10000, got {entry.OpsSeen}");
            Assert.Equal(0, entry.OpsIncluded);
            Assert.Equal(ReputationStatus.Banned, entry.Status);
            Assert.NotNull(entry.BannedUntil);
        }

        [Fact]
        public async Task IsThrottledAsync_ReturnsTrueWhenThrottled()
        {
            var address = "0x4444444444444444444444444444444444444444";
            await _store.SetThrottledAsync(address, TimeSpan.FromHours(1));

            var isThrottled = await _store.IsThrottledAsync(address);

            Assert.True(isThrottled);
        }

        [Fact]
        public async Task IsBannedAsync_ReturnsTrueWhenBanned()
        {
            var address = "0x5555555555555555555555555555555555555555";
            await _store.SetBannedAsync(address, TimeSpan.FromHours(1));

            var isBanned = await _store.IsBannedAsync(address);

            Assert.True(isBanned);
        }

        [Fact]
        public async Task ClearAsync_RemovesEntry()
        {
            var address = "0x6666666666666666666666666666666666666666";
            await _store.RecordIncludedAsync(address);

            await _store.ClearAsync(address);

            var entry = await _store.GetAsync(address);
            Assert.Null(entry);
        }

        [Fact]
        public async Task DecayAsync_DecaysOpsSeenAndOpsIncludedBy23Over24()
        {
            // ERC-4337 hourly decay: opsSeen and opsIncluded are each multiplied by
            // 23/24 (integer division) via the shared ReputationDecayCalculator, the same
            // formula the in-memory backend runs. 240 -> 230, 48 -> 46.
            var address = "0x7777777777777777777777777777777777777777";
            await _store.RecordSeenAsync(address, 240);
            for (int i = 0; i < 48; i++)
            {
                await _store.RecordIncludedAsync(address);
            }

            await _store.DecayAsync();

            var entry = await _store.GetAsync(address);
            Assert.NotNull(entry);
            Assert.Equal(230, entry.OpsSeen);
            Assert.Equal(46, entry.OpsIncluded);
        }

        [Fact]
        public async Task DecayAsync_RepeatedTicks_ConvergeTowardZeroAndDropTheEntry()
        {
            var address = "0x7c7c7c7c7c7c7c7c7c7c7c7c7c7c7c7c7c7c7c7c";
            await _store.RecordSeenAsync(address, 5);

            for (int i = 0; i < 200; i++)
            {
                await _store.DecayAsync();
            }

            var entry = await _store.GetAsync(address);
            Assert.Null(entry);
            Assert.Empty(await _store.GetAllAsync());
        }

        [Fact]
        public async Task DecayAsync_ClearsAThrottleOnceOpsSeenFallsBelowThreshold()
        {
            var address = "0x7d7d7d7d7d7d7d7d7d7d7d7d7d7d7d7d7d7d7d7d";
            await _store.RecordSeenAsync(address, 110);
            var before = await _store.GetAsync(address);
            Assert.Equal(ReputationStatus.Throttled, before!.Status);

            await _store.DecayAsync();

            var after = await _store.GetAsync(address);
            Assert.NotNull(after);
            Assert.Equal(105, after!.OpsSeen);
            Assert.Equal(ReputationStatus.Ok, after.Status);
            Assert.False(await _store.IsThrottledAsync(address));
        }

        [Fact]
        public async Task GetAllAsync_ReturnsAllEntries()
        {
            await _store.RecordIncludedAsync("0x8888888888888888888888888888888888888888");
            await _store.RecordIncludedAsync("0x9999999999999999999999999999999999999999");

            var entries = await _store.GetAllAsync();

            Assert.Equal(2, entries.Length);
        }

        [Fact]
        public async Task DataPersistsAcrossRecreation()
        {
            var address = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            await _store.RecordIncludedAsync(address);
            await _store.RecordIncludedAsync(address);
            await _store.RecordIncludedAsync(address);

            _manager.Dispose();

            var options = new BundlerRocksDbOptions { DatabasePath = _testDbPath };
            using var newManager = new BundlerRocksDbManager(options);
            var newStore = new RocksDbReputationStore(newManager, _config);

            var entry = await newStore.GetAsync(address);

            Assert.NotNull(entry);
            Assert.Equal(3, entry.OpsIncluded);
        }
    }
}
