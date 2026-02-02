using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Stores;
using Nethereum.AccountAbstraction.Structs;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RocksDB.UnitTests
{
    public class RocksDbMempoolTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly BundlerRocksDbManager _manager;
        private readonly RocksDbUserOpMempool _mempool;

        public RocksDbMempoolTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"bundler_test_{Guid.NewGuid():N}");
            var options = new BundlerRocksDbOptions { DatabasePath = _testDbPath };
            _manager = new BundlerRocksDbManager(options);
            _mempool = new RocksDbUserOpMempool(_manager, options);
        }

        public void Dispose()
        {
            _manager.Dispose();
            if (Directory.Exists(_testDbPath))
            {
                Directory.Delete(_testDbPath, true);
            }
        }

        private MempoolEntry CreateTestEntry(string userOpHash, string sender = "0x1111111111111111111111111111111111111111")
        {
            return new MempoolEntry
            {
                UserOpHash = userOpHash,
                EntryPoint = "0x5FF137D4b0FDCD49DcA30c7CF57E578a026d2789",
                UserOperation = new PackedUserOperation
                {
                    Sender = sender,
                    Nonce = BigInteger.Zero,
                    CallData = new byte[] { 0x01, 0x02, 0x03 },
                    Signature = new byte[] { 0xab, 0xcd }
                },
                Priority = BigInteger.One
            };
        }

        [Fact]
        public async Task AddAsync_WithValidEntry_ReturnsTrueAndPersists()
        {
            var entry = CreateTestEntry("0x" + new string('a', 64));

            var result = await _mempool.AddAsync(entry);

            Assert.True(result);
            var retrieved = await _mempool.GetAsync(entry.UserOpHash);
            Assert.NotNull(retrieved);
            Assert.Equal(entry.UserOpHash, retrieved.UserOpHash);
            Assert.Equal(entry.EntryPoint, retrieved.EntryPoint);
            Assert.Equal(MempoolEntryState.Pending, retrieved.State);
        }

        [Fact]
        public async Task AddAsync_WithDuplicateHash_ReturnsFalse()
        {
            var entry = CreateTestEntry("0x" + new string('b', 64));
            await _mempool.AddAsync(entry);

            var duplicate = CreateTestEntry("0x" + new string('b', 64));
            var result = await _mempool.AddAsync(duplicate);

            Assert.False(result);
        }

        [Fact]
        public async Task GetPendingAsync_ReturnsOnlyPendingEntries()
        {
            var entry1 = CreateTestEntry("0x" + new string('c', 64));
            var entry2 = CreateTestEntry("0x" + new string('d', 64));
            await _mempool.AddAsync(entry1);
            await _mempool.AddAsync(entry2);

            var pending = await _mempool.GetPendingAsync(10);

            Assert.Equal(2, pending.Length);
        }

        [Fact]
        public async Task MarkSubmittedAsync_ChangesState()
        {
            var entry = CreateTestEntry("0x" + new string('e', 64));
            await _mempool.AddAsync(entry);

            await _mempool.MarkSubmittedAsync(new[] { entry.UserOpHash }, "0x" + new string('f', 64));

            var retrieved = await _mempool.GetAsync(entry.UserOpHash);
            Assert.NotNull(retrieved);
            Assert.Equal(MempoolEntryState.Submitted, retrieved.State);
        }

        [Fact]
        public async Task MarkIncludedAsync_ChangesStateAndSetsBlockNumber()
        {
            var entry = CreateTestEntry("0x" + new string('1', 64));
            await _mempool.AddAsync(entry);
            var txHash = "0x" + new string('2', 64);
            await _mempool.MarkSubmittedAsync(new[] { entry.UserOpHash }, txHash);

            await _mempool.MarkIncludedAsync(new[] { entry.UserOpHash }, txHash, 12345);

            var retrieved = await _mempool.GetAsync(entry.UserOpHash);
            Assert.NotNull(retrieved);
            Assert.Equal(MempoolEntryState.Included, retrieved.State);
            Assert.Equal(12345, retrieved.BlockNumber);
        }

        [Fact]
        public async Task RevertSubmittedAsync_ReturnsEntryToPending()
        {
            var entry = CreateTestEntry("0x" + new string('3', 64));
            await _mempool.AddAsync(entry);
            var txHash = "0x" + new string('4', 64);
            await _mempool.MarkSubmittedAsync(new[] { entry.UserOpHash }, txHash);

            await _mempool.RevertSubmittedAsync(txHash);

            var retrieved = await _mempool.GetAsync(entry.UserOpHash);
            Assert.NotNull(retrieved);
            Assert.Equal(MempoolEntryState.Pending, retrieved.State);
            Assert.Equal(1, retrieved.RetryCount);
        }

        [Fact]
        public async Task GetBySenderAsync_ReturnsEntriesForSender()
        {
            var sender = "0x2222222222222222222222222222222222222222";
            var entry1 = CreateTestEntry("0x" + new string('5', 64), sender);
            entry1.UserOperation.Nonce = BigInteger.Zero;
            var entry2 = CreateTestEntry("0x" + new string('6', 64), sender);
            entry2.UserOperation.Nonce = BigInteger.One;
            var entry3 = CreateTestEntry("0x" + new string('7', 64), "0x3333333333333333333333333333333333333333");

            await _mempool.AddAsync(entry1);
            await _mempool.AddAsync(entry2);
            await _mempool.AddAsync(entry3);

            var bySender = await _mempool.GetBySenderAsync(sender);

            Assert.Equal(2, bySender.Length);
            Assert.All(bySender, e => Assert.Equal(sender.ToLowerInvariant(), e.UserOperation.Sender?.ToLowerInvariant()));
        }

        [Fact]
        public async Task RemoveAsync_DeletesEntry()
        {
            var entry = CreateTestEntry("0x" + new string('8', 64));
            await _mempool.AddAsync(entry);

            var removed = await _mempool.RemoveAsync(entry.UserOpHash);

            Assert.True(removed);
            var retrieved = await _mempool.GetAsync(entry.UserOpHash);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task CountAsync_ReturnsCorrectCount()
        {
            await _mempool.AddAsync(CreateTestEntry("0x" + new string('9', 64)));
            await _mempool.AddAsync(CreateTestEntry("0x" + new string('0', 64)));

            var count = await _mempool.CountAsync();

            Assert.Equal(2, count);
        }

        [Fact]
        public async Task GetStatsAsync_ReturnsAccurateStats()
        {
            var entry1 = CreateTestEntry("0x" + new string('a', 64), "0x1111111111111111111111111111111111111111");
            var entry2 = CreateTestEntry("0x" + new string('b', 64), "0x2222222222222222222222222222222222222222");
            entry2.Paymaster = "0x4444444444444444444444444444444444444444";
            await _mempool.AddAsync(entry1);
            await _mempool.AddAsync(entry2);

            var stats = await _mempool.GetStatsAsync();

            Assert.Equal(2, stats.TotalCount);
            Assert.Equal(2, stats.PendingCount);
            Assert.Equal(2, stats.UniqueSenders);
            Assert.Equal(1, stats.UniquePaymasters);
        }

        [Fact]
        public async Task DataPersistsAcrossManagerRecreation()
        {
            var entry = CreateTestEntry("0x" + new string('c', 64));
            await _mempool.AddAsync(entry);

            _manager.Dispose();

            var options = new BundlerRocksDbOptions { DatabasePath = _testDbPath };
            using var newManager = new BundlerRocksDbManager(options);
            var newMempool = new RocksDbUserOpMempool(newManager, options);

            var retrieved = await newMempool.GetAsync(entry.UserOpHash);

            Assert.NotNull(retrieved);
            Assert.Equal(entry.UserOpHash, retrieved.UserOpHash);
        }
    }
}
