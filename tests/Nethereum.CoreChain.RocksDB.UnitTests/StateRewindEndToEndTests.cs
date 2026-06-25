using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    /// <summary>
    /// Closes the gap between the journal-rewind unit tests (which seed the
    /// diff store manually) and real block execution (which writes through
    /// <see cref="HistoricalStateStore"/>). Drives writes through the
    /// historical decorator exactly as <c>BlockProcessor.ProcessBlockAsync</c>
    /// does, then verifies that journal capture, persistent diff storage,
    /// rewind, and Patricia state-root recomputation all agree.
    /// </summary>
    public class StateRewindEndToEndTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly RocksDbManager _manager;
        private readonly RocksDbStateStore _inner;
        private readonly RocksDbStateDiffStore _diffs;
        private readonly RocksDbBlockStore _blocks;
        private readonly RocksDbChainMetadataStore _meta;
        private readonly RocksDbTrieNodeStore _trieNodes;
        private readonly HistoricalStateStore _historical;
        private readonly StateRewindService _rewind;

        private const string AddrA = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        private static readonly byte[] Hash0 = NewHashOf(0x00);
        private static readonly byte[] Hash1 = NewHashOf(0x11);
        private static readonly byte[] Hash2 = NewHashOf(0x22);

        public StateRewindEndToEndTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_rewind_e2e_{Guid.NewGuid():N}");
            _manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dbPath });
            _inner = new RocksDbStateStore(_manager);
            _diffs = new RocksDbStateDiffStore(_manager);
            _blocks = new RocksDbBlockStore(_manager);
            _meta = new RocksDbChainMetadataStore(_manager);
            _trieNodes = new RocksDbTrieNodeStore(_manager);
            _historical = new HistoricalStateStore(_inner, _diffs, HistoricalStateOptions.FullArchive);
            _rewind = new StateRewindService(_inner, _diffs, _blocks, _meta);
        }

        public void Dispose()
        {
            _manager?.Dispose();
            if (Directory.Exists(_dbPath))
            {
                try { Directory.Delete(_dbPath, true); } catch { }
            }
        }

        [Fact]
        public async Task Given_TwoBlocksWrittenViaJournal_When_RewindOneBlock_Then_StateRootMatchesPreBlockTwo()
        {
            await SaveHeaderAsync(0, Hash0);
            await SaveHeaderAsync(1, Hash1);
            await SaveHeaderAsync(2, Hash2);

            var r0 = await NewCalculator().ComputeStateRootAsync();

            _historical.SetCurrentBlockNumber(1);
            await _historical.SaveAccountAsync(AddrA, new Account { Balance = 1000, Nonce = 1 });
            await _historical.SaveStorageAsync(AddrA, BigInteger.One, new byte[] { 0x42 });
            await _historical.ClearCurrentBlockNumberAsync();
            _meta.Commit(1, Hash1);
            var r1 = await NewCalculator().ComputeStateRootAsync();

            _historical.SetCurrentBlockNumber(2);
            await _historical.SaveAccountAsync(AddrA, new Account { Balance = 2000, Nonce = 2 });
            await _historical.SaveStorageAsync(AddrA, BigInteger.One, new byte[] { 0x99 });
            await _historical.SaveStorageAsync(AddrA, new BigInteger(2), new byte[] { 0xAA });
            await _historical.ClearCurrentBlockNumberAsync();
            _meta.Commit(2, Hash2);
            var r2 = await NewCalculator().ComputeStateRootAsync();

            Assert.NotEqual(r0, r1);
            Assert.NotEqual(r1, r2);

            var diffBlock2 = await _diffs.GetBlockDiffAsync(2);
            Assert.NotNull(diffBlock2);
            Assert.Single(diffBlock2.AccountDiffs);
            Assert.Equal(2, diffBlock2.StorageDiffs.Count);

            var undone = await _rewind.RewindWithJournalAsync(targetBlock: 1);

            Assert.Equal(1UL, undone);
            Assert.Equal(1UL, _meta.GetLastBlock());
            Assert.Equal(Hash1, _meta.GetLastBlockHash());

            var rewoundAccount = await _inner.GetAccountAsync(AddrA);
            Assert.NotNull(rewoundAccount);
            Assert.Equal((BigInteger)1000, AccountBalance(rewoundAccount));

            Assert.Equal(new byte[] { 0x42 }, await _inner.GetStorageAsync(AddrA, BigInteger.One));
            var slot2 = await _inner.GetStorageAsync(AddrA, new BigInteger(2));
            Assert.True(slot2 == null || slot2.Length == 0);

            var rewoundRoot = await NewCalculator().ComputeStateRootAsync();
            Assert.Equal(r1, rewoundRoot);
        }

        [Fact]
        public async Task Given_TwoBlocksWrittenViaJournal_When_RewindToGenesis_Then_StateRootMatchesEmpty()
        {
            await SaveHeaderAsync(0, Hash0);
            await SaveHeaderAsync(1, Hash1);
            await SaveHeaderAsync(2, Hash2);

            var r0 = await NewCalculator().ComputeStateRootAsync();

            _historical.SetCurrentBlockNumber(1);
            await _historical.SaveAccountAsync(AddrA, new Account { Balance = 1000, Nonce = 1 });
            await _historical.SaveStorageAsync(AddrA, BigInteger.One, new byte[] { 0x42 });
            await _historical.ClearCurrentBlockNumberAsync();
            _meta.Commit(1, Hash1);

            _historical.SetCurrentBlockNumber(2);
            await _historical.SaveAccountAsync(AddrA, new Account { Balance = 2000, Nonce = 2 });
            await _historical.SaveStorageAsync(AddrA, new BigInteger(2), new byte[] { 0xAA });
            await _historical.ClearCurrentBlockNumberAsync();
            _meta.Commit(2, Hash2);

            var undone = await _rewind.RewindWithJournalAsync(targetBlock: 0);

            Assert.Equal(2UL, undone);
            Assert.Equal(0UL, _meta.GetLastBlock());
            Assert.False(await _inner.AccountExistsAsync(AddrA));

            var slot1 = await _inner.GetStorageAsync(AddrA, BigInteger.One);
            Assert.True(slot1 == null || slot1.Length == 0);
            var slot2 = await _inner.GetStorageAsync(AddrA, new BigInteger(2));
            Assert.True(slot2 == null || slot2.Length == 0);

            var rewoundRoot = await NewCalculator().ComputeStateRootAsync();
            Assert.Equal(r0, rewoundRoot);
        }

        private IIncrementalStateRootCalculator NewCalculator()
            => new IncrementalStateRootCalculator(_inner, _trieNodes);

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

        private static BigInteger AccountBalance(Account a)
            => new BigInteger(a.Balance.ToBigEndian(), isUnsigned: true, isBigEndian: true);
    }
}
