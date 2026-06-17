using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    /// <summary>
    /// End-to-end tests for journal-based rewind. Each test sets up a fresh
    /// RocksDB instance, writes a few "blocks worth" of state changes plus
    /// matching reverse diffs (the same pattern <c>HistoricalStateStore</c>
    /// produces inside <c>BlockProcessor.ProcessBlockAsync</c>), then drives
    /// <see cref="StateRewindService.RewindWithJournalAsync"/> and verifies
    /// the on-disk state actually rolled back to the pre-block values.
    /// </summary>
    public class StateRewindServiceTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly RocksDbManager _manager;
        private readonly RocksDbStateStore _state;
        private readonly RocksDbStateDiffStore _diffs;
        private readonly RocksDbBlockStore _blocks;
        private readonly RocksDbChainMetadataStore _meta;
        private readonly StateRewindService _rewind;

        private const string AddrA = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string AddrB = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        private static readonly byte[] Hash0 = NewHashOf(0x00);
        private static readonly byte[] Hash1 = NewHashOf(0x11);
        private static readonly byte[] Hash2 = NewHashOf(0x22);
        private static readonly byte[] Hash3 = NewHashOf(0x33);

        public StateRewindServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_rewind_test_{Guid.NewGuid():N}");
            _manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dbPath });
            _state = new RocksDbStateStore(_manager);
            _diffs = new RocksDbStateDiffStore(_manager);
            _blocks = new RocksDbBlockStore(_manager);
            _meta = new RocksDbChainMetadataStore(_manager);
            _rewind = new StateRewindService(_state, _diffs, _blocks, _meta);
        }

        public void Dispose()
        {
            _manager?.Dispose();
            if (Directory.Exists(_dbPath))
            {
                try { Directory.Delete(_dbPath, true); }
                catch { }
            }
        }

        [Fact]
        public async Task Given_NoChangesAfterTarget_When_Rewind_Then_ReturnsZero()
        {
            _meta.Commit(5, Hash1);
            var undone = await _rewind.RewindWithJournalAsync(targetBlock: 5);
            Assert.Equal(0UL, undone);
            Assert.Equal(5UL, _meta.GetLastBlock());
        }

        [Fact]
        public async Task Given_TargetAboveCurrent_When_Rewind_Then_NoOp()
        {
            _meta.Commit(3, Hash1);
            var undone = await _rewind.RewindWithJournalAsync(targetBlock: 10);
            Assert.Equal(0UL, undone);
            Assert.Equal(3UL, _meta.GetLastBlock());
        }

        [Fact]
        public async Task Given_OneBlockOfChanges_When_RewindToZero_Then_AccountRestoredAndCursorAtZero()
        {
            await SaveHeaderAsync(0, Hash0);
            await SaveHeaderAsync(1, Hash1);

            // Pre-block-1 state: account A doesn't exist.
            // Block 1: write A=1000.
            await _state.SaveAccountAsync(AddrA, new Account { Balance = 1000, Nonce = 1 });
            await _diffs.SaveBlockDiffAsync(new BlockStateDiff
            {
                BlockNumber = 1,
                AccountDiffs = { new AccountDiffEntry { Address = AddrA, PreValue = null } }
            });
            _meta.Commit(1, Hash1);

            Assert.True(await _state.AccountExistsAsync(AddrA));

            var undone = await _rewind.RewindWithJournalAsync(targetBlock: 0);

            Assert.Equal(1UL, undone);
            Assert.Equal(0UL, _meta.GetLastBlock());
            Assert.Equal(Hash0, _meta.GetLastBlockHash());
            Assert.False(await _state.AccountExistsAsync(AddrA));
        }

        [Fact]
        public async Task Given_ThreeBlocksOfChanges_When_RewindToOne_Then_BlocksTwoAndThreeUndone()
        {
            await SaveHeaderAsync(0, Hash0);
            await SaveHeaderAsync(1, Hash1);
            await SaveHeaderAsync(2, Hash2);
            await SaveHeaderAsync(3, Hash3);

            // Block 1: A doesn't exist → A=1000. B doesn't exist → B=2000.
            await _state.SaveAccountAsync(AddrA, new Account { Balance = 1000, Nonce = 1 });
            await _state.SaveAccountAsync(AddrB, new Account { Balance = 2000, Nonce = 1 });
            await _diffs.SaveBlockDiffAsync(new BlockStateDiff
            {
                BlockNumber = 1,
                AccountDiffs =
                {
                    new AccountDiffEntry { Address = AddrA, PreValue = null },
                    new AccountDiffEntry { Address = AddrB, PreValue = null }
                }
            });
            _meta.Commit(1, Hash1);

            // Block 2: A: 1000 → 1500. B unchanged.
            await _state.SaveAccountAsync(AddrA, new Account { Balance = 1500, Nonce = 2 });
            await _diffs.SaveBlockDiffAsync(new BlockStateDiff
            {
                BlockNumber = 2,
                AccountDiffs =
                {
                    new AccountDiffEntry { Address = AddrA, PreValue = new Account { Balance = 1000, Nonce = 1 } }
                }
            });
            _meta.Commit(2, Hash2);

            // Block 3: A: 1500 → 3000. B: 2000 → 2500.
            await _state.SaveAccountAsync(AddrA, new Account { Balance = 3000, Nonce = 3 });
            await _state.SaveAccountAsync(AddrB, new Account { Balance = 2500, Nonce = 2 });
            await _diffs.SaveBlockDiffAsync(new BlockStateDiff
            {
                BlockNumber = 3,
                AccountDiffs =
                {
                    new AccountDiffEntry { Address = AddrA, PreValue = new Account { Balance = 1500, Nonce = 2 } },
                    new AccountDiffEntry { Address = AddrB, PreValue = new Account { Balance = 2000, Nonce = 1 } }
                }
            });
            _meta.Commit(3, Hash3);

            var undone = await _rewind.RewindWithJournalAsync(targetBlock: 1);

            Assert.Equal(2UL, undone);
            Assert.Equal(1UL, _meta.GetLastBlock());
            Assert.Equal(Hash1, _meta.GetLastBlockHash());

            var a = await _state.GetAccountAsync(AddrA);
            Assert.NotNull(a);
            Assert.Equal((BigInteger)1000, BalanceOf(a));
            var b = await _state.GetAccountAsync(AddrB);
            Assert.NotNull(b);
            Assert.Equal((BigInteger)2000, BalanceOf(b));

            Assert.Null(await _diffs.GetBlockDiffAsync(2));
            Assert.Null(await _diffs.GetBlockDiffAsync(3));
            Assert.NotNull(await _diffs.GetBlockDiffAsync(1));
        }

        [Fact]
        public async Task Given_StorageWrites_When_Rewind_Then_StorageRestoredToPreValues()
        {
            await SaveHeaderAsync(0, Hash0);
            await SaveHeaderAsync(1, Hash1);

            // Block 1: write storage slot 0 of A from empty → 0x42.
            await _state.SaveStorageAsync(AddrA, BigInteger.Zero, new byte[] { 0x42 });
            await _diffs.SaveBlockDiffAsync(new BlockStateDiff
            {
                BlockNumber = 1,
                StorageDiffs =
                {
                    new StorageDiffEntry
                    {
                        Address = AddrA,
                        Slot = BigInteger.Zero,
                        PreValue = Array.Empty<byte>()
                    }
                }
            });
            _meta.Commit(1, Hash1);

            Assert.Equal(new byte[] { 0x42 }, await _state.GetStorageAsync(AddrA, BigInteger.Zero));

            var undone = await _rewind.RewindWithJournalAsync(targetBlock: 0);

            Assert.Equal(1UL, undone);
            var slot = await _state.GetStorageAsync(AddrA, BigInteger.Zero);
            Assert.True(slot == null || slot.Length == 0);
        }

        [Fact]
        public async Task Given_FetchCursorsAheadOfHead_When_Rewind_Then_CursorsClampedDown()
        {
            await SaveHeaderAsync(0, Hash0);
            await SaveHeaderAsync(1, Hash1);

            // Block 1 fully executed; pipeline cursors ahead at 10 / 8.
            await _state.SaveAccountAsync(AddrA, new Account { Balance = 100, Nonce = 1 });
            await _diffs.SaveBlockDiffAsync(new BlockStateDiff
            {
                BlockNumber = 1,
                AccountDiffs = { new AccountDiffEntry { Address = AddrA, PreValue = null } }
            });
            _meta.Commit(1, Hash1);
            _meta.SetLastFetchedHeader(10);
            _meta.SetLastFetchedBody(8);

            await _rewind.RewindWithJournalAsync(targetBlock: 0);

            Assert.Equal(0UL, _meta.GetLastBlock());
            Assert.Equal(0UL, _meta.GetLastFetchedHeader());
            Assert.Equal(0UL, _meta.GetLastFetchedBody());
        }

        [Fact]
        public async Task Given_MissingDiff_When_Rewind_Then_Throws()
        {
            await SaveHeaderAsync(0, Hash0);
            await SaveHeaderAsync(1, Hash1);
            _meta.Commit(1, Hash1);
            await Assert.ThrowsAsync<InvalidOperationException>(() => _rewind.RewindWithJournalAsync(0));
        }

        private async Task SaveHeaderAsync(ulong blockNumber, byte[] blockHash)
        {
            var header = new BlockHeader
            {
                BlockNumber = blockNumber,
                ParentHash = blockNumber == 0 ? new byte[32] : NewHashOf((byte)((int)(blockNumber - 1) ^ 0x10)),
                StateRoot = new byte[32],
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                UnclesHash = new byte[32],
                ExtraData = Array.Empty<byte>(),
                LogsBloom = new byte[256],
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = 0,
                GasLimit = 0,
                GasUsed = 0,
                Timestamp = 0,
                MixHash = new byte[32],
                Nonce = new byte[8]
            };
            await _blocks.SaveAsync(header, blockHash);
        }

        private static byte[] NewHashOf(byte fill)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = fill;
            return h;
        }

        private static BigInteger BalanceOf(Account a)
            => new BigInteger(a.Balance.ToBigEndian(), isUnsigned: true, isBigEndian: true);
    }
}
