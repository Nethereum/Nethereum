using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbManagerLeaseTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"rocksdb_lease_{Guid.NewGuid():N}");

        public RocksDbManagerLeaseTests() => Directory.CreateDirectory(_root);

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        private string SubDir(string name)
        {
            var path = Path.Combine(_root, name);
            Directory.CreateDirectory(path);
            return path;
        }

        private static BlockHeader MakeHeader(ulong n) => new BlockHeader
        {
            BlockNumber = n,
            StateRoot = Fill(0xBB, n),
            ParentHash = n > 0 ? Fill(0xAA, n - 1) : new byte[32],
            LogsBloom = new byte[256],
            UnclesHash = new byte[32],
            Coinbase = "0x" + new string('0', 40),
            TransactionsHash = new byte[32],
            ReceiptHash = new byte[32],
            Difficulty = 0,
            GasLimit = 30_000_000,
            GasUsed = 0,
            Timestamp = 1_700_000_000 + (long)n,
            ExtraData = Array.Empty<byte>(),
            MixHash = new byte[32],
            Nonce = new byte[8],
        };

        private static byte[] Fill(byte tag, ulong n)
        {
            var b = new byte[32];
            b[0] = tag;
            b[24] = (byte)(n >> 32); b[25] = (byte)(n >> 24); b[26] = (byte)(n >> 16);
            b[27] = (byte)(n >> 8); b[28] = (byte)n;
            return b;
        }

        [Fact]
        public void Given_ADisposedRocksDbManager_When_ALeaseIsRequested_Then_ItThrowsObjectDisposedRatherThanReturningAFreedHandle()
        {
            var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = SubDir("disposed") });
            manager.Dispose();

            Assert.Throws<ObjectDisposedException>(() => manager.Lease());
        }

        [Fact]
        public void Given_AnOpenRocksDbManager_When_ALeaseIsRequested_Then_ItStillReturnsTheLiveDatabaseForReading()
        {
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = SubDir("open") });
            var key = Encoding.UTF8.GetBytes("probe-key");
            var value = Encoding.UTF8.GetBytes("probe-value");
            manager.Put(RocksDbManager.CF_METADATA, key, value);

            using var lease = manager.Lease();
            var read = lease.Database.Get(key, manager.GetColumnFamily(RocksDbManager.CF_METADATA));

            Assert.Equal(value, read);
        }

        [Fact]
        public void Given_ALeaseHeldOpen_When_DisposeIsCalledConcurrently_Then_DisposeBlocksUntilTheLeaseIsReleased()
        {
            var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = SubDir("drain") });
            var disposeStarted = new ManualResetEventSlim(false);
            var disposeCompleted = 0;

            var lease = manager.Lease();

            var disposeTask = Task.Run(() =>
            {
                disposeStarted.Set();
                manager.Dispose();
                Interlocked.Exchange(ref disposeCompleted, 1);
            });

            disposeStarted.Wait();
            Thread.Sleep(250);
            Assert.Equal(0, Interlocked.CompareExchange(ref disposeCompleted, 0, 0));

            lease.Dispose();

            Assert.True(disposeTask.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal(1, Interlocked.CompareExchange(ref disposeCompleted, 0, 0));
        }

        [Fact]
        public async Task Given_AnOpenManager_When_ABlockStoreReadsAHeader_Then_ItStillReturnsThePersistedBlock()
        {
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = SubDir("blockstore-open") });
            var store = new RocksDbBlockStore(manager);
            var header = MakeHeader(7);

            await store.SaveAsync(header, Fill(0xCC, 7));
            var read = await store.GetByNumberAsync(7);

            Assert.NotNull(read);
            Assert.Equal(header.StateRoot, read.StateRoot);
        }

        [Fact]
        public async Task Given_ADisposedManager_When_ABlockStoreReadsAHeader_Then_ItThrowsObjectDisposedRatherThanCrashingTheProcess()
        {
            var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = SubDir("blockstore-disposed") });
            var store = new RocksDbBlockStore(manager);
            await store.SaveAsync(MakeHeader(7), Fill(0xCC, 7));

            manager.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => store.GetByNumberAsync(7));
        }
    }
}
