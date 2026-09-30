using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class HotWindowTipBandThresholdTests : IDisposable
    {
        private const int WindowSize = 128;
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"hotwindow_tipband_{Guid.NewGuid():N}");

        public HotWindowTipBandThresholdTests() => Directory.CreateDirectory(_root);

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
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

        private static async Task WriteDescendingAsync(IBlockStore blocks, ulong top, int count)
        {
            for (ulong n = top; n > top - (ulong)count; n--)
                await blocks.SaveAsync(MakeHeader(n), Fill(0xAA, n)).ConfigureAwait(false);
        }

        [Fact]
        public async Task Split_DescendingWalk_HotWindowStaysBoundedToTipBand_DeepBlocksReadFromHistory()
        {
            const ulong T = 25_000_000;
            const int count = 500;

            var dir = Path.Combine(_root, "split-band");
            Directory.CreateDirectory(dir);
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true });
            var history = new RocksDbBlockStore(manager);
            var hot = new RocksDbHotBlockWindowStore(manager, WindowSize, evictOnWrite: true);
            var blocks = new CompositeBlockStore(history, hot, writeThroughHistory: true);

            await WriteDescendingAsync(blocks, T, count);

            Assert.True(hot.ContainsBlock(T), "tip must be hot");
            Assert.True(hot.ContainsBlock(T - (ulong)WindowSize + 1), "band floor must be hot");
            Assert.False(hot.ContainsBlock(T - (ulong)WindowSize), "one below the floor must NOT be hot");
            Assert.False(hot.ContainsBlock(T - 499), "deep descent block must NOT be hot");

            Assert.NotNull(await history.GetByNumberAsync(new BigInteger(T - 499)));
            Assert.NotNull(await blocks.GetByNumberAsync(new BigInteger(T - 499)));
            Assert.NotNull(await blocks.GetHashByNumberAsync(new BigInteger(T - 499)));
            Assert.NotNull(await blocks.GetByNumberAsync(new BigInteger(T)));
            Assert.NotNull(await blocks.GetByNumberAsync(new BigInteger(T - 32)));
        }

        private static ISignedTransaction MakeTx(ulong number)
        {
            var r = new byte[32]; r[0] = 0x01;
            var s = new byte[32]; s[0] = 0x01;
            return new LegacyTransaction(
                nonce: CanonicalTestScalars.Scalar((long)number + 1),
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: new byte[] { },
                data: Array.Empty<byte>(),
                r: r, s: s, v: 27);
        }

        [Fact]
        public async Task Split_AscendingBodyFill_HotWindowStaysBoundedToTipBand_DeepBodiesReadFromHistory()
        {
            const ulong T = 25_000_000;
            const int span = 800;

            var dir = Path.Combine(_root, "split-ascending");
            Directory.CreateDirectory(dir);
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true });
            var historyBlocks = new RocksDbBlockStore(manager);
            var historyTx = new RocksDbTransactionStore(manager, historyBlocks);
            var hot = new RocksDbHotBlockWindowStore(manager, WindowSize, evictOnWrite: true);
            var blocks = new CompositeBlockStore(historyBlocks, hot, writeThroughHistory: true);
            var transactions = new CompositeTransactionStore(historyTx, hot, writeThroughHistory: true);

            await WriteDescendingAsync(blocks, T, span);
            Assert.True(hot.ContainsBlock(T));
            Assert.False(hot.ContainsBlock(T - 500));

            for (ulong n = T - span; n <= T; n++)
            {
                await blocks.SaveAsync(MakeHeader(n), Fill(0xAA, n)).ConfigureAwait(false);
                await transactions.SaveManyAsync(Fill(0xAA, n), new BigInteger(n),
                    new List<ISignedTransaction> { MakeTx(n) }).ConfigureAwait(false);
            }

            Assert.True(hot.ContainsBlock(T), "tip stays hot");
            Assert.True(hot.ContainsBlock(T - (ulong)WindowSize + 1), "band floor stays hot");
            Assert.False(hot.ContainsBlock(T - (ulong)WindowSize), "one below the floor is not hot");
            Assert.False(hot.ContainsBlock(T - 500), "deep ascending block is not hot");
            Assert.Empty(hot.GetTransactionsForBlock(T - 500));
            Assert.NotEmpty(await transactions.GetByBlockNumberAsync(new BigInteger(T - 500)));
            Assert.NotNull(await blocks.GetByNumberAsync(new BigInteger(T - 500)));
            Assert.NotEmpty(hot.GetTransactionsForBlock(T));
        }

        [Fact]
        public async Task Promotion_DescendingWrites_AllLandInHot_NoDataLoss()
        {
            const ulong T = 25_000_000;
            const int count = 500;

            var dir = Path.Combine(_root, "promotion-all");
            Directory.CreateDirectory(dir);
            using var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PromotionEnabled = true });
            var history = new RocksDbBlockStore(manager);
            var hot = new RocksDbHotBlockWindowStore(manager, WindowSize, evictOnWrite: false);
            var blocks = new CompositeBlockStore(history, hot, writeThroughHistory: false);

            await WriteDescendingAsync(blocks, T, count);

            Assert.True(hot.ContainsBlock(T), "tip must be hot");
            Assert.True(hot.ContainsBlock(T - 499), "deep block must be hot in promotion mode (no data loss)");
            Assert.Null(await history.GetByNumberAsync(new BigInteger(T - 499)));
            Assert.NotNull(await blocks.GetByNumberAsync(new BigInteger(T - 499)));
        }
    }
}
