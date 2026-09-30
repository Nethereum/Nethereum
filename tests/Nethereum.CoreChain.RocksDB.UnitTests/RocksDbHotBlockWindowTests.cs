using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.History;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbHotBlockWindowTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"hotwindow_{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        private static RocksDbStorageOptions SplitOptions(int hotWindowBlocks = 128) =>
            new RocksDbStorageOptions { SplitHistoryStore = true, HotWindowBlocks = hotWindowBlocks };

        [Fact]
        public void LegacySingleDb_NeverOpensHotCfs()
        {
            var dir = Path.Combine(_root, "legacy");
            using (var bundle = RocksDbChainStoreBundle.Open(dir))
            {
                Assert.IsType<RocksDbBlockStore>(bundle.Blocks);
                Assert.IsType<RocksDbTransactionStore>(bundle.Transactions);
            }

            using var rawManager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir });
            Assert.False(rawManager.HasColumnFamily(RocksDbManager.CF_HOT_BLOCK_HEADER));
            Assert.False(rawManager.HasColumnFamily(RocksDbManager.CF_HOT_BLOCK_META));
            Assert.False(rawManager.HasColumnFamily(RocksDbManager.CF_HOT_BLOCK_HASH_INDEX));
            Assert.False(rawManager.HasColumnFamily(RocksDbManager.CF_HOT_TX_BODY));
            Assert.False(rawManager.HasColumnFamily(RocksDbManager.CF_HOT_TX_HASH_INDEX));
        }

        [Fact]
        public void SplitCoreScopedManager_OpensHotCfs()
        {
            var dir = Path.Combine(_root, "split-open");
            using (var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: SplitOptions()))
            {
                Assert.IsType<CompositeBlockStore>(bundle.Blocks);
                Assert.IsType<CompositeTransactionStore>(bundle.Transactions);
            }

            var coreDir = Path.Combine(dir, RocksDbChainStoreBundle.CoreSubDir);
            using var coreOnly = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = coreDir }, CatalogueScope.Core);
            Assert.True(coreOnly.HasColumnFamily(RocksDbManager.CF_HOT_BLOCK_HEADER));
            Assert.True(coreOnly.HasColumnFamily(RocksDbManager.CF_HOT_BLOCK_META));
            Assert.True(coreOnly.HasColumnFamily(RocksDbManager.CF_HOT_BLOCK_HASH_INDEX));
            Assert.True(coreOnly.HasColumnFamily(RocksDbManager.CF_HOT_TX_BODY));
            Assert.True(coreOnly.HasColumnFamily(RocksDbManager.CF_HOT_TX_HASH_INDEX));
        }

        [Fact]
        public async Task HotWindow_EvictsBelowFloor_KeepsExactlyLatestWindowSize()
        {
            const int windowSize = 5;
            const long first = 100, last = 112;
            var dir = Path.Combine(_root, "evict");

            using (var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: SplitOptions(windowSize)))
            {
                for (var n = first; n <= last; n++)
                    await PersistPerBlockAsync(bundle, MakeBlock(n));
            }

            var coreDir = Path.Combine(dir, RocksDbChainStoreBundle.CoreSubDir);
            using var coreOnly = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = coreDir }, CatalogueScope.Core);

            var floor = last - windowSize + 1;
            for (var n = first; n < floor; n++)
            {
                Assert.Null(coreOnly.Get(RocksDbManager.CF_HOT_BLOCK_HEADER, HistoryKeys.BlockKey((ulong)n)));
                Assert.Null(coreOnly.Get(RocksDbManager.CF_HOT_BLOCK_META, HistoryKeys.BlockKey((ulong)n)));
                Assert.Null(coreOnly.Get(RocksDbManager.CF_HOT_TX_BODY, HistoryKeys.TxKey((ulong)n, 0)));
            }
            for (var n = floor; n <= last; n++)
            {
                Assert.NotNull(coreOnly.Get(RocksDbManager.CF_HOT_BLOCK_HEADER, HistoryKeys.BlockKey((ulong)n)));
                Assert.NotNull(coreOnly.Get(RocksDbManager.CF_HOT_BLOCK_META, HistoryKeys.BlockKey((ulong)n)));
                Assert.NotNull(coreOnly.Get(RocksDbManager.CF_HOT_TX_BODY, HistoryKeys.TxKey((ulong)n, 0)));
            }

            for (var n = first; n < floor; n++)
            {
                var block = MakeBlock(n);
                Assert.Null(coreOnly.Get(RocksDbManager.CF_HOT_BLOCK_HASH_INDEX, block.Hash));
                Assert.Null(coreOnly.Get(RocksDbManager.CF_HOT_TX_HASH_INDEX, block.Transactions[0].Hash));
            }
        }

        [Fact]
        public async Task Composite_GetBlockByNumber_And_GetTransactionByHash_ResolveForBothRanges()
        {
            const int windowSize = 5;
            const long first = 200, last = 212;
            var dir = Path.Combine(_root, "bothranges");

            using var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: SplitOptions(windowSize));
            var blocks = new List<PersistableBlock>();
            for (var n = first; n <= last; n++)
            {
                var b = MakeBlock(n);
                blocks.Add(b);
                await PersistPerBlockAsync(bundle, b);
            }

            var floor = last - windowSize + 1;
            var oldBlock = blocks[0];
            var recentBlock = blocks[blocks.Count - 1];

            Assert.True(oldBlock.Header.BlockNumber.ToBigInteger() < floor);
            Assert.True(recentBlock.Header.BlockNumber.ToBigInteger() >= floor);

            var oldHeader = await bundle.Blocks.GetByNumberAsync(oldBlock.Header.BlockNumber.ToBigInteger());
            Assert.NotNull(oldHeader);
            var recentHeader = await bundle.Blocks.GetByNumberAsync(recentBlock.Header.BlockNumber.ToBigInteger());
            Assert.NotNull(recentHeader);

            var oldTx = await bundle.Transactions.GetByHashAsync(oldBlock.Transactions[0].Hash);
            Assert.NotNull(oldTx);
            var recentTx = await bundle.Transactions.GetByHashAsync(recentBlock.Transactions[0].Hash);
            Assert.NotNull(recentTx);

            Assert.NotNull(await bundle.Blocks.GetByHashAsync(oldBlock.Hash));
            Assert.NotNull(await bundle.Blocks.GetByHashAsync(recentBlock.Hash));
            Assert.True(await bundle.Blocks.ExistsAsync(oldBlock.Hash));
            Assert.True(await bundle.Blocks.ExistsAsync(recentBlock.Hash));
            Assert.Equal(oldBlock.Hash.ToHex(), (await bundle.Blocks.GetHashByNumberAsync(oldBlock.Header.BlockNumber.ToBigInteger())).ToHex());
            Assert.Equal(recentBlock.Hash.ToHex(), (await bundle.Blocks.GetHashByNumberAsync(recentBlock.Header.BlockNumber.ToBigInteger())).ToHex());
        }

        [Fact]
        public async Task DeleteByNumber_RemovesHotRowsToo()
        {
            const int windowSize = 5;
            var dir = Path.Combine(_root, "delete");
            var block = MakeBlock(300);

            using (var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: SplitOptions(windowSize)))
            {
                await PersistPerBlockAsync(bundle, block);
                Assert.NotNull(await bundle.Blocks.GetByNumberAsync(300));

                await bundle.Transactions.DeleteByBlockNumberAsync(300);
                await bundle.Blocks.DeleteByNumberAsync(300);
            }

            var coreDir = Path.Combine(dir, RocksDbChainStoreBundle.CoreSubDir);
            using var coreOnly = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = coreDir }, CatalogueScope.Core);
            Assert.Null(coreOnly.Get(RocksDbManager.CF_HOT_BLOCK_HEADER, HistoryKeys.BlockKey(300)));
            Assert.Null(coreOnly.Get(RocksDbManager.CF_HOT_BLOCK_HASH_INDEX, block.Hash));
            Assert.Null(coreOnly.Get(RocksDbManager.CF_HOT_TX_BODY, HistoryKeys.TxKey(300, 0)));
            Assert.Null(coreOnly.Get(RocksDbManager.CF_HOT_TX_HASH_INDEX, block.Transactions[0].Hash));
        }

        [Fact]
        public async Task SelfSufficiency_ReadInsideHotWindow_NeverTouchesHistory_OutsideWindowDoes()
        {
            const int windowSize = 5;
            const long first = 400, last = 412;
            var dir = Path.Combine(_root, "selfsufficiency");

            var blocks = new List<PersistableBlock>();
            using (var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: SplitOptions(windowSize)))
            {
                for (var n = first; n <= last; n++)
                {
                    var b = MakeBlock(n);
                    blocks.Add(b);
                    await PersistPerBlockAsync(bundle, b);
                }
            }

            var floor = last - windowSize + 1;
            var insideWindowBlock = blocks.Find(b => b.Header.BlockNumber.ToBigInteger() == floor + 2);
            var outsideWindowBlock = blocks.Find(b => b.Header.BlockNumber.ToBigInteger() == first + 1);
            Assert.NotNull(insideWindowBlock);
            Assert.NotNull(outsideWindowBlock);

            var coreDir = Path.Combine(dir, RocksDbChainStoreBundle.CoreSubDir);
            using var coreOnly = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = coreDir }, CatalogueScope.Core);
            var hotWindow = new RocksDbHotBlockWindowStore(coreOnly, windowSize);

            var throwingBlocks = new ThrowingBlockStore();
            var throwingTxs = new ThrowingTransactionStore();
            var blockComposite = new CompositeBlockStore(throwingBlocks, hotWindow);
            var txComposite = new CompositeTransactionStore(throwingTxs, hotWindow);

            var header = await blockComposite.GetByNumberAsync(insideWindowBlock.Header.BlockNumber.ToBigInteger());
            Assert.NotNull(header);
            Assert.Equal(insideWindowBlock.Header.BlockNumber.ToBigInteger(), header.BlockNumber.ToBigInteger());

            var byHash = await blockComposite.GetByHashAsync(insideWindowBlock.Hash);
            Assert.NotNull(byHash);
            Assert.True(await blockComposite.ExistsAsync(insideWindowBlock.Hash));
            Assert.Equal(insideWindowBlock.Hash.ToHex(),
                (await blockComposite.GetHashByNumberAsync(insideWindowBlock.Header.BlockNumber.ToBigInteger())).ToHex());

            var tx = await txComposite.GetByHashAsync(insideWindowBlock.Transactions[0].Hash);
            Assert.NotNull(tx);
            var txsForBlock = await txComposite.GetByBlockNumberAsync(insideWindowBlock.Header.BlockNumber.ToBigInteger());
            Assert.Equal(insideWindowBlock.Transactions.Count, txsForBlock.Count);
            var txsByHash = await txComposite.GetByBlockHashAsync(insideWindowBlock.Hash);
            Assert.Equal(insideWindowBlock.Transactions.Count, txsByHash.Count);

            Assert.False(throwingBlocks.WasTouched);
            Assert.False(throwingTxs.WasTouched);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => blockComposite.GetByNumberAsync(outsideWindowBlock.Header.BlockNumber.ToBigInteger()));
            Assert.True(throwingBlocks.WasTouched);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => txComposite.GetByHashAsync(outsideWindowBlock.Transactions[0].Hash));
            Assert.True(throwingTxs.WasTouched);
        }

        private sealed class ThrowingBlockStore : IBlockStore
        {
            public bool WasTouched { get; private set; }
            private Exception Touch([System.Runtime.CompilerServices.CallerMemberName] string member = null)
            {
                WasTouched = true;
                return new InvalidOperationException($"history touched ({member}) for a block the hot window should have served alone");
            }
            public Task<BlockHeader> GetByHashAsync(byte[] hash) => throw Touch();
            public Task<BlockHeader> GetByNumberAsync(BigInteger number) => throw Touch();
            public Task<BlockHeader> GetLatestAsync() => throw Touch();
            public Task<BigInteger> GetHeightAsync() => throw Touch();
            public Task SaveAsync(BlockHeader header, byte[] blockHash) => throw Touch();
            public Task<bool> ExistsAsync(byte[] hash) => throw Touch();
            public Task<byte[]> GetHashByNumberAsync(BigInteger number) => throw Touch();
            public Task UpdateBlockHashAsync(BigInteger blockNumber, byte[] newHash) => throw Touch();
            public Task DeleteByNumberAsync(BigInteger blockNumber) => throw Touch();
        }

        private sealed class ThrowingTransactionStore : ITransactionStore
        {
            public bool WasTouched { get; private set; }
            private Exception Touch([System.Runtime.CompilerServices.CallerMemberName] string member = null)
            {
                WasTouched = true;
                return new InvalidOperationException($"history touched ({member}) for a transaction the hot window should have served alone");
            }
            public Task<ISignedTransaction> GetByHashAsync(byte[] txHash) => throw Touch();
            public Task<List<ISignedTransaction>> GetByBlockHashAsync(byte[] blockHash) => throw Touch();
            public Task<List<byte[]>> GetHashesByBlockHashAsync(byte[] blockHash) => throw Touch();
            public Task<List<ISignedTransaction>> GetByBlockNumberAsync(BigInteger blockNumber) => throw Touch();
            public Task SaveAsync(ISignedTransaction tx, byte[] blockHash, int txIndex, BigInteger blockNumber) => throw Touch();
            public Task SaveManyAsync(byte[] blockHash, BigInteger blockNumber, IReadOnlyList<ISignedTransaction> txs) => throw Touch();
            public Task<TransactionLocation> GetLocationAsync(byte[] txHash) => throw Touch();
            public Task DeleteByBlockNumberAsync(BigInteger blockNumber) => throw Touch();
        }

        private static async Task PersistPerBlockAsync(RocksDbChainStoreBundle bundle, PersistableBlock b)
        {
            var blockNumber = b.Header.BlockNumber.ToBigInteger();
            await bundle.Blocks.SaveAsync(b.Header, b.Hash);
            if (b.Transactions != null)
                await bundle.Transactions.SaveManyAsync(b.Hash, blockNumber, (IReadOnlyList<ISignedTransaction>)b.Transactions);
        }

        private static PersistableBlock MakeBlock(long number)
        {
            var hash = Fill32((int)number);
            var header = MakeHeader(number, hash);
            var txs = new List<ISignedTransaction> { MakeTx(number, 0), MakeTx(number, 1) };

            return new PersistableBlock(
                header, hash,
                uncles: new List<BlockHeader>(),
                withdrawals: null,
                transactions: txs,
                receipts: new List<ReceiptSaveItem>(),
                logs: new List<(List<Log> Logs, byte[] TxHash, int TxIndex)>(),
                bloom: Fill((byte)0x00, 256));
        }

        private static BlockHeader MakeHeader(long number, byte[] hash)
        {
            return new BlockHeader
            {
                BlockNumber = new EvmUInt256((ulong)number),
                ParentHash = new byte[32],
                TransactionsHash = new byte[32],
                UnclesHash = new byte[32],
                ReceiptHash = new byte[32],
                StateRoot = new byte[32],
                Difficulty = new EvmUInt256(1UL),
                GasLimit = 1,
                Timestamp = 1,
                ExtraData = Array.Empty<byte>(),
                MixHash = new byte[32],
                Nonce = new byte[8],
                LogsBloom = new byte[256],
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

        private static byte[] Fill32(int number)
        {
            var b = new byte[32];
            b[28] = (byte)(number >> 24);
            b[29] = (byte)(number >> 16);
            b[30] = (byte)(number >> 8);
            b[31] = (byte)number;
            return b;
        }

        private static byte[] Fill(byte v, int len)
        {
            var b = new byte[len];
            for (int i = 0; i < len; i++) b[i] = v;
            return b;
        }
    }
}
