using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class EnableLogIndexTests : IDisposable
    {
        private const string LogAddress = "0x00000000000000000000000000000000c0ffee";
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"enablelogindex_{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        [Fact]
        public async Task Default_ServesLogsViaL1_AndWritesNoLogIndexCfs()
        {
            var dir = Path.Combine(_root, "off");
            var block = MakeBlock(42);

            using (var bundle = RocksDbChainStoreBundle.Open(dir))
            {
                Assert.IsType<CompositeLogStore>(bundle.Logs);

                await PersistPerBlockAsync(bundle, block);

                var n = block.Header.BlockNumber.ToBigInteger();
                var expectedCount = block.Logs.Sum(l => l.Logs.Count);

                var byNumber = await bundle.Logs.GetLogsByBlockNumberAsync(n);
                Assert.Equal(expectedCount, byNumber.Count);

                var byHash = await bundle.Logs.GetLogsByBlockHashAsync(block.Hash);
                Assert.Equal(expectedCount, byHash.Count);

                var byTx = await bundle.Logs.GetLogsByTxHashAsync(block.Transactions[0].Hash);
                Assert.NotEmpty(byTx);

                var byFilter = await bundle.Logs.GetLogsAsync(new LogFilter
                {
                    Addresses = new List<string> { LogAddress },
                    FromBlock = n,
                    ToBlock = n,
                });
                Assert.Equal(expectedCount, byFilter.Count);
            }

            using var raw = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir });
            Assert.Equal(0, CountEntries(raw, RocksDbManager.CF_LOGS));
            Assert.Equal(0, CountEntries(raw, RocksDbManager.CF_LOG_BY_BLOCK));
            Assert.Equal(0, CountEntries(raw, RocksDbManager.CF_LOG_BY_ADDRESS));
            Assert.Equal(0, CountEntries(raw, RocksDbManager.CF_LOG_BY_TX));
            Assert.Equal(0, CountEntries(raw, RocksDbManager.CF_BLOCK_BLOOMS));

            Assert.True(CountEntries(raw, HistoryColumnFamilies.BlockMeta) > 0);
        }

        [Fact]
        public async Task Default_BulkSyncPath_WritesNoLogIndexCfs_ButStillServesViaL1()
        {
            var dir = Path.Combine(_root, "off-bulk");
            var blocks = new List<PersistableBlock> { MakeBlock(100), MakeBlock(101) };

            using (var bundle = RocksDbChainStoreBundle.Open(dir, bulkSync: true))
            {
                await ((IBatchedBlockPersister)bundle).PersistBlocksAsync(blocks);
                bundle.CheckpointBulk();

                foreach (var b in blocks)
                {
                    var n = b.Header.BlockNumber.ToBigInteger();
                    var logs = await bundle.Logs.GetLogsByBlockNumberAsync(n);
                    Assert.Equal(b.Logs.Sum(l => l.Logs.Count), logs.Count);
                }
            }

            using var raw = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir });
            Assert.Equal(0, CountEntries(raw, RocksDbManager.CF_LOGS));
            Assert.Equal(0, CountEntries(raw, RocksDbManager.CF_LOG_BY_BLOCK));
            Assert.Equal(0, CountEntries(raw, RocksDbManager.CF_LOG_BY_ADDRESS));
            Assert.Equal(0, CountEntries(raw, RocksDbManager.CF_LOG_BY_TX));
            Assert.Equal(0, CountEntries(raw, RocksDbManager.CF_BLOCK_BLOOMS));
        }

        [Fact]
        public async Task Default_BatchDrainPath_WritesNoLogIndexCfs_ButStillServesViaL1()
        {
            var dir = Path.Combine(_root, "off-batch");
            var blocks = new List<PersistableBlock> { MakeBlock(200), MakeBlock(201) };

            using (var bundle = RocksDbChainStoreBundle.Open(dir))
            {
                await ((IBatchedBlockPersister)bundle).PersistBlocksAsync(blocks);

                foreach (var b in blocks)
                {
                    var n = b.Header.BlockNumber.ToBigInteger();
                    var logs = await bundle.Logs.GetLogsByBlockNumberAsync(n);
                    Assert.Equal(b.Logs.Sum(l => l.Logs.Count), logs.Count);
                }
            }

            using var raw = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir });
            Assert.Equal(0, CountEntries(raw, RocksDbManager.CF_LOGS));
            Assert.Equal(0, CountEntries(raw, RocksDbManager.CF_LOG_BY_BLOCK));
            Assert.Equal(0, CountEntries(raw, RocksDbManager.CF_LOG_BY_ADDRESS));
            Assert.Equal(0, CountEntries(raw, RocksDbManager.CF_LOG_BY_TX));
            Assert.Equal(0, CountEntries(raw, RocksDbManager.CF_BLOCK_BLOOMS));
        }

        [Fact]
        public async Task On_BuildsAndServesTheLegacyIndex()
        {
            var dir = Path.Combine(_root, "on");
            var options = new RocksDbStorageOptions { EnableLogIndex = true };
            var block = MakeBlock(42);

            using (var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: options))
            {
                Assert.IsType<CompositeLogStore>(bundle.Logs);

                await PersistPerBlockAsync(bundle, block);

                var n = block.Header.BlockNumber.ToBigInteger();
                var expectedCount = block.Logs.Sum(l => l.Logs.Count);
                var byNumber = await bundle.Logs.GetLogsByBlockNumberAsync(n);
                Assert.Equal(expectedCount, byNumber.Count);
            }

            using var raw = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir });
            Assert.True(CountEntries(raw, RocksDbManager.CF_LOGS) > 0);
            Assert.True(CountEntries(raw, RocksDbManager.CF_LOG_BY_BLOCK) > 0);
            Assert.True(CountEntries(raw, RocksDbManager.CF_LOG_BY_ADDRESS) > 0);
            Assert.True(CountEntries(raw, RocksDbManager.CF_LOG_BY_TX) > 0);
            Assert.True(CountEntries(raw, RocksDbManager.CF_BLOCK_BLOOMS) > 0);
        }

        [Fact]
        public async Task On_BulkSyncPath_BuildsTheLegacyIndex()
        {
            var dir = Path.Combine(_root, "on-bulk");
            var options = new RocksDbStorageOptions { EnableLogIndex = true };
            var blocks = new List<PersistableBlock> { MakeBlock(100), MakeBlock(101) };

            using (var bundle = RocksDbChainStoreBundle.Open(dir, bulkSync: true, storageOptions: options))
            {
                await ((IBatchedBlockPersister)bundle).PersistBlocksAsync(blocks);
                bundle.CheckpointBulk();
            }

            using var raw = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir });
            Assert.True(CountEntries(raw, RocksDbManager.CF_LOGS) > 0);
            Assert.True(CountEntries(raw, RocksDbManager.CF_BLOCK_BLOOMS) > 0);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task On_WorksWithSplitHistoryStore_OnAndOff(bool splitHistoryStore)
        {
            var dir = Path.Combine(_root, "on-split-" + splitHistoryStore);
            var options = new RocksDbStorageOptions { EnableLogIndex = true, SplitHistoryStore = splitHistoryStore };
            var block = MakeBlock(300);

            using var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: options);
            await PersistPerBlockAsync(bundle, block);

            var n = block.Header.BlockNumber.ToBigInteger();
            var logs = await bundle.Logs.GetLogsByBlockNumberAsync(n);
            Assert.Equal(block.Logs.Sum(l => l.Logs.Count), logs.Count);
        }

        private static int CountEntries(RocksDbManager manager, string columnFamily)
        {
            using var it = manager.CreateIterator(columnFamily);
            var count = 0;
            for (it.SeekToFirst(); it.Valid(); it.Next()) count++;
            return count;
        }

        private static async Task PersistPerBlockAsync(RocksDbChainStoreBundle bundle, PersistableBlock b)
        {
            var blockNumber = b.Header.BlockNumber.ToBigInteger();
            await bundle.Blocks.SaveAsync(b.Header, b.Hash);
            await bundle.Uncles.SaveAsync(b.Hash, b.Uncles);
            if (b.Transactions != null)
                await bundle.Transactions.SaveManyAsync(b.Hash, blockNumber, (IReadOnlyList<ISignedTransaction>)b.Transactions);
            if (b.Receipts != null && b.Receipts.Count > 0)
                await bundle.Receipts.SaveManyAsync(b.Hash, blockNumber, b.Receipts);
            if (b.Logs != null && b.Logs.Count > 0)
                await bundle.Logs.SaveManyLogsAsync(b.Logs, b.Hash, blockNumber);
            if (b.Bloom != null)
                await bundle.Logs.SaveBlockBloomAsync(blockNumber, b.Bloom);
        }

        private static PersistableBlock MakeBlock(long number)
        {
            var hash = Fill((byte)(number & 0xFF), 32);
            var header = MakeHeader(number, hash);
            var bloom = BloomFor(LogAddress);
            header.LogsBloom = bloom;

            var txs = new List<ISignedTransaction> { MakeTx(number, 0), MakeTx(number, 1) };
            BigInteger blockNum = number;

            var receipts = new List<ReceiptSaveItem>();
            var logs = new List<(List<Log> Logs, byte[] TxHash, int TxIndex)>();
            BigInteger cumulative = 0;
            for (int j = 0; j < txs.Count; j++)
            {
                cumulative += 21000;
                var rcptLogs = new List<Log>
                {
                    new Log { Address = LogAddress, Topics = new List<byte[]> { Fill(0x11, 32) }, Data = new byte[] { (byte)j } }
                };
                var rcpt = new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = cumulative, Logs = rcptLogs };
                receipts.Add(new ReceiptSaveItem(rcpt, txs[j].Hash, j, 21000, null, 1_000_000_000));
                logs.Add((rcptLogs, txs[j].Hash, j));
            }

            return new PersistableBlock(
                header, hash,
                uncles: new List<BlockHeader>(),
                withdrawals: null,
                transactions: txs,
                receipts: receipts,
                logs: logs,
                bloom: bloom);
        }

        private static byte[] BloomFor(string address)
        {
            var filter = new LogBloomFilter();
            filter.AddAddress(address);
            return filter.Data;
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

        private static byte[] Fill(byte v, int len)
        {
            var b = new byte[len];
            for (int i = 0; i < len; i++) b[i] = v;
            return b;
        }
    }
}
