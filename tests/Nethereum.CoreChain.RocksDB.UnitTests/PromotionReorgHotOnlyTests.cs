using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class PromotionReorgHotOnlyTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"promotionreorg_{Guid.NewGuid():N}");

        public PromotionReorgHotOnlyTests() => Directory.CreateDirectory(_root);

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        [Fact]
        public async Task ReorgWithinWindow_PromotionMode_NeverWritesOrDeletesHistory()
        {
            var dir = Path.Combine(_root, "reorg-hot-only");
            Directory.CreateDirectory(dir);
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true });
            var hot = new RocksDbHotBlockWindowStore(manager, windowSize: 64, evictOnWrite: false);
            var history = new ThrowingBlockStore();
            var historyTx = new ThrowingTransactionStore();
            var blocks = new CompositeBlockStore(history, hot, writeThroughHistory: false);
            var transactions = new CompositeTransactionStore(historyTx, hot, writeThroughHistory: false);

            for (long n = 1; n <= 5; n++)
            {
                var block = MakeBlock(n);
                await blocks.SaveAsync(block.Header, block.Hash).ConfigureAwait(false);
                await transactions.SaveManyAsync(block.Hash, block.Number, block.Transactions).ConfigureAwait(false);
            }

            for (long n = 5; n >= 3; n--)
            {
                var number = (BigInteger)n;
                await transactions.DeleteByBlockNumberAsync(number).ConfigureAwait(false);
                await blocks.DeleteByNumberAsync(number).ConfigureAwait(false);
            }

            Assert.True(hot.ContainsBlock(1));
            Assert.True(hot.ContainsBlock(2));
            Assert.False(hot.ContainsBlock(3));
            Assert.False(hot.ContainsBlock(4));
            Assert.False(hot.ContainsBlock(5));
        }

        [Fact]
        public async Task RewindBelowPromotionFloor_PromotionMode_FatalVerdict()
        {
            var dir = Path.Combine(_root, "rewind-below-floor");
            Directory.CreateDirectory(dir);
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true });
            var journalOptions = new HistoricalStateOptions { MaxHistoryBlocks = 3 };
            var bundle = RocksDbChainStoreBundle.FromManager(manager, dir, journalOptions, ownsManager: false);

            bundle.Metadata.Commit(10, Fill32(10));

            var coordinator = new RewindCoordinator(bundle);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => coordinator.RewindToAsync(targetBlock: 5, RewindPolicy.JournalFirstThenSnapshot)).ConfigureAwait(false);
            Assert.Contains("promotion floor", ex.Message);

            Assert.Null(await bundle.Blocks.GetByNumberAsync(1).ConfigureAwait(false));
            Assert.Equal(10UL, bundle.Metadata.GetLastBlock());
        }

        [Fact]
        public async Task RewindAtOrAbovePromotionFloor_PromotionMode_NoOpNotFatal()
        {
            var dir = Path.Combine(_root, "rewind-at-floor");
            Directory.CreateDirectory(dir);
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true });
            var journalOptions = new HistoricalStateOptions { MaxHistoryBlocks = 3 };
            var bundle = RocksDbChainStoreBundle.FromManager(manager, dir, journalOptions, ownsManager: false);

            bundle.Metadata.Commit(10, Fill32(10));

            var coordinator = new RewindCoordinator(bundle);

            var result = await coordinator.RewindToAsync(targetBlock: 7, RewindPolicy.JournalFirstThenSnapshot).ConfigureAwait(false);
            Assert.Equal(RewindOutcome.NoPathAvailable, result.Outcome);
        }

        [Fact]
        public void PromotionWindow_AtOpen_DerivesHotWindowFromMaxHistoryBlocks()
        {
            var dir = Path.Combine(_root, "window-derive");
            Directory.CreateDirectory(dir);
            using var manager = new RocksDbManager(new RocksDbStorageOptions
            {
                DatabasePath = dir, PromotionEnabled = true, HotWindowBlocks = 128,
            });
            var journalOptions = new HistoricalStateOptions { MaxHistoryBlocks = 999 };
            var bundle = RocksDbChainStoreBundle.FromManager(manager, dir, journalOptions, ownsManager: false);

            var hot = (RocksDbHotBlockWindowStore)bundle.GetType()
                .GetField("_hotWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(bundle);
            Assert.Equal(999, hot.WindowSize);
        }

        [Fact]
        public void PromotionWindowOverflow_AtOpen_Fatal()
        {
            var dir = Path.Combine(_root, "window-overflow");
            Directory.CreateDirectory(dir);
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true });
            var journalOptions = new HistoricalStateOptions { MaxHistoryBlocks = (BigInteger)int.MaxValue + 1 };

            Assert.Throws<InvalidOperationException>(
                () => RocksDbChainStoreBundle.FromManager(manager, dir, journalOptions, ownsManager: false));
        }

        private sealed class ThrowingBlockStore : IBlockStore
        {
            public Task<BlockHeader> GetByHashAsync(byte[] hash) => throw Fail();
            public Task<BlockHeader> GetByNumberAsync(BigInteger number) => throw Fail();
            public Task<BlockHeader> GetLatestAsync() => throw Fail();
            public Task<BigInteger> GetHeightAsync() => throw Fail();
            public Task SaveAsync(BlockHeader header, byte[] blockHash) => throw Fail();
            public Task<bool> ExistsAsync(byte[] hash) => throw Fail();
            public Task<byte[]> GetHashByNumberAsync(BigInteger number) => throw Fail();
            public Task UpdateBlockHashAsync(BigInteger blockNumber, byte[] newHash) => throw Fail();
            public Task DeleteByNumberAsync(BigInteger blockNumber) => throw Fail();

            private static InvalidOperationException Fail()
                => new InvalidOperationException("history must never be written to or deleted from in promotion mode");
        }

        private sealed class ThrowingTransactionStore : ITransactionStore
        {
            public Task<ISignedTransaction> GetByHashAsync(byte[] txHash) => throw Fail();
            public Task<List<ISignedTransaction>> GetByBlockHashAsync(byte[] blockHash) => throw Fail();
            public Task<List<byte[]>> GetHashesByBlockHashAsync(byte[] blockHash) => throw Fail();
            public Task<List<ISignedTransaction>> GetByBlockNumberAsync(BigInteger blockNumber) => throw Fail();
            public Task SaveAsync(ISignedTransaction tx, byte[] blockHash, int txIndex, BigInteger blockNumber) => throw Fail();
            public Task SaveManyAsync(byte[] blockHash, BigInteger blockNumber, IReadOnlyList<ISignedTransaction> txs) => throw Fail();
            public Task<TransactionLocation> GetLocationAsync(byte[] txHash) => throw Fail();
            public Task DeleteByBlockNumberAsync(BigInteger blockNumber) => throw Fail();

            private static InvalidOperationException Fail()
                => new InvalidOperationException("history must never be written to or deleted from in promotion mode");
        }

        private sealed class TestBlock
        {
            public BlockHeader Header;
            public byte[] Hash;
            public BigInteger Number;
            public List<ISignedTransaction> Transactions;
        }

        private static TestBlock MakeBlock(long number)
        {
            var hash = Fill32(number);
            var header = MakeHeader(number, hash);
            var txs = new List<ISignedTransaction> { MakeTx(number, 0) };
            return new TestBlock
            {
                Header = header,
                Hash = hash,
                Number = header.BlockNumber.ToBigInteger(),
                Transactions = txs,
            };
        }

        private static BlockHeader MakeHeader(long number, byte[] hash)
        {
            var bloom = new byte[256];
            return new BlockHeader
            {
                BlockNumber = new EvmUInt256((ulong)number),
                ParentHash = Fill32(number - 1),
                TransactionsHash = new byte[32],
                UnclesHash = new byte[32],
                ReceiptHash = new byte[32],
                StateRoot = new byte[32],
                Difficulty = new EvmUInt256(1UL),
                GasLimit = 1,
                Timestamp = 1000 + number,
                ExtraData = Array.Empty<byte>(),
                MixHash = hash,
                Nonce = new byte[8],
                LogsBloom = bloom,
                Coinbase = "0x0000000000000000000000000000000000000000",
            };
        }

        private static ISignedTransaction MakeTx(long number, int index)
        {
            var r = new byte[32]; r[0] = 0x01;
            var s = new byte[32]; s[0] = 0x01;
            return new LegacyTransaction(
                nonce: CanonicalTestScalars.Scalar((number + 1) * 1000 + index),
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: new byte[] { },
                data: Array.Empty<byte>(),
                r: r, s: s, v: 27);
        }

        private static byte[] Fill32(long number)
        {
            var b = new byte[32];
            b[24] = 0x55;
            b[28] = (byte)(number >> 24);
            b[29] = (byte)(number >> 16);
            b[30] = (byte)(number >> 8);
            b[31] = (byte)number;
            return b;
        }
    }
}
