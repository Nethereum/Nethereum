using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.History;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbSplitHistoryStoreTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"splitstore_{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        private static RocksDbStorageOptions SplitOptions() => new RocksDbStorageOptions { SplitHistoryStore = true };

        [Fact]
        public void Open_WithSplitHistoryStore_CreatesTwoPhysicalDatabases()
        {
            var dir = Path.Combine(_root, "shape");
            using var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: SplitOptions());

            var coreDir = Path.Combine(dir, RocksDbChainStoreBundle.CoreSubDir);
            var historyDir = Path.Combine(dir, RocksDbChainStoreBundle.HistorySubDir);
            Assert.True(Directory.Exists(coreDir));
            Assert.True(Directory.Exists(historyDir));
            Assert.True(File.Exists(Path.Combine(coreDir, "CURRENT")));
            Assert.True(File.Exists(Path.Combine(historyDir, "CURRENT")));
        }

        [Fact]
        public async Task SplitBundle_WritesHistoryRowsToHistoryDb_AndStateToCoreDb()
        {
            var dir = Path.Combine(_root, "retarget");
            var block = MakeBlock(500);

            using (var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: SplitOptions()))
            {
                await PersistPerBlockAsync(bundle, block);
                const string address = "0x0000000000000000000000000000000000000001";
                var flat = new FlatStateBatch(
                    deletedAccountAddresses: Array.Empty<string>(),
                    clearedStorageAddresses: Array.Empty<string>(),
                    nonZeroStorage: Array.Empty<(string, BigInteger, byte[])>(),
                    deletedSlots: Array.Empty<(string, BigInteger)>(),
                    accounts: new[] { (address, new Account { Balance = 42, Nonce = 1 }) },
                    code: Array.Empty<(byte[], byte[])>());
                var flush = (IAtomicBlockFlush)bundle;
                await flush.FlushBlockAsync(flat, block: 500, hash: block.Hash);
                await flush.DrainAsync();
            }

            var coreDir = Path.Combine(dir, RocksDbChainStoreBundle.CoreSubDir);
            var historyDir = Path.Combine(dir, RocksDbChainStoreBundle.HistorySubDir);

            using (var historyOnly = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = historyDir }, CatalogueScope.History))
            {
                var headerBytes = historyOnly.Get(HistoryColumnFamilies.BlockHeader,
                    HistoryKeys.BlockKey(500));
                Assert.NotNull(headerBytes);
                var metaBytes = historyOnly.Get(HistoryColumnFamilies.BlockMeta,
                    HistoryKeys.BlockKey(500));
                Assert.NotNull(metaBytes);
            }

            using (var coreOnly = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = coreDir }, CatalogueScope.Core))
            {
                Assert.True(coreOnly.HasColumnFamily(RocksDbManager.CF_METADATA));
                Assert.False(coreOnly.HasColumnFamily(HistoryColumnFamilies.BlockHeader));
                var accountBytes = coreOnly.Get(RocksDbManager.CF_STATE_ACCOUNTS,
                    StateKeys.AccountKey("0x0000000000000000000000000000000000000001"));
                Assert.NotNull(accountBytes);
            }
        }

        [Fact]
        public void HistoryScopedManager_NeverOpensCoreOnlyCf_CoreScopedManager_NeverOpensHistoryOnlyCf()
        {
            var historyDir = Path.Combine(_root, "scopecheck-history");
            var coreDir = Path.Combine(_root, "scopecheck-core");
            Directory.CreateDirectory(historyDir);
            Directory.CreateDirectory(coreDir);

            using var historyMgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = historyDir }, CatalogueScope.History);
            using var coreMgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = coreDir }, CatalogueScope.Core);

            Assert.Throws<ArgumentException>(() => historyMgr.GetColumnFamily(RocksDbManager.CF_METADATA));
            Assert.Throws<ArgumentException>(() => coreMgr.GetColumnFamily(HistoryColumnFamilies.BlockHeader));
        }

        [Fact]
        public async Task SplitBundle_AllFourRpcReads_Resolve()
        {
            var dir = Path.Combine(_root, "rpcs");
            var block = MakeBlock(600);

            using var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: SplitOptions());
            await PersistPerBlockAsync(bundle, block);

            var n = block.Header.BlockNumber.ToBigInteger();

            var header = await bundle.Blocks.GetByNumberAsync(n);
            Assert.NotNull(header);
            Assert.Equal(n, header.BlockNumber.ToBigInteger());

            var tx = await bundle.Transactions.GetByHashAsync(block.Transactions[0].Hash);
            Assert.NotNull(tx);

            var receipts = await bundle.Receipts.GetByBlockHashAsync(block.Hash);
            Assert.Equal(block.Receipts.Count, receipts.Count);

            var logs = await bundle.Logs.GetLogsByBlockNumberAsync(n);
            Assert.Equal(block.Logs.Sum(l => l.Logs.Count), logs.Count);
        }

        [Fact]
        public async Task SplitBundle_PersistBlocksAsync_BulkBatch_RoundTrips()
        {
            var dir = Path.Combine(_root, "bulkbatch");
            var blocks = new List<PersistableBlock> { MakeBlock(700), MakeBlock(701), MakeBlock(702) };

            using var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: SplitOptions());
            await ((IBatchedBlockPersister)bundle).PersistBlocksAsync(blocks);

            foreach (var b in blocks)
            {
                var n = b.Header.BlockNumber.ToBigInteger();
                var hashByNum = await bundle.Blocks.GetHashByNumberAsync(n);
                Assert.Equal(b.Hash.ToHex(), hashByNum.ToHex());
                var txs = await bundle.Transactions.GetByBlockHashAsync(b.Hash);
                Assert.Equal(b.Transactions.Count, txs.Count);
                var receipts = await bundle.Receipts.GetByBlockHashAsync(b.Hash);
                Assert.Equal(b.Receipts.Count, receipts.Count);
                var logs = await bundle.Logs.GetLogsByBlockNumberAsync(n);
                Assert.Equal(b.Logs.Sum(l => l.Logs.Count), logs.Count);
            }
        }

        [Fact]
        public void SplitHistoryStore_Plus_BulkSync_Plus_EnableLogIndex_ThrowsClearError()
        {
            var dir = Path.Combine(_root, "bulksync-guard");
            var opts = SplitOptions();
            opts.EnableLogIndex = true;
            var ex = Assert.Throws<NotSupportedException>(() =>
                RocksDbChainStoreBundle.Open(dir, bulkSync: true, storageOptions: opts));
            Assert.Contains("SplitHistoryStore", ex.Message);
            Assert.Contains("bulkSync", ex.Message);
            Assert.Contains("EnableLogIndex", ex.Message);
        }

        [Fact]
        public void SplitHistoryStore_Plus_BulkSync_EnableLogIndexOff_DoesNotThrow()
        {
            var dir = Path.Combine(_root, "bulksync-supported");
            var opts = SplitOptions();
            using var bundle = RocksDbChainStoreBundle.Open(dir, bulkSync: true, storageOptions: opts);
            Assert.NotNull(bundle);
        }

        [Fact]
        public async Task SplitBundle_BulkSyncFirehose_HistoryCfsPopulated_CoreDbUntouched()
        {
            var dir = Path.Combine(_root, "firehose-cfs");
            var opts = SplitOptions();
            var blocks = new List<PersistableBlock> { MakeBlock(2000), MakeBlock(2001) };

            using (var bundle = RocksDbChainStoreBundle.Open(dir, bulkSync: true, storageOptions: opts))
            {
                await ((IBatchedBlockPersister)bundle).PersistBlocksAsync(blocks);
                ((IBulkDurabilityBoundary)bundle).CheckpointBulk();
                bundle.FinishBulkSync();
            }

            var coreDir = Path.Combine(dir, RocksDbChainStoreBundle.CoreSubDir);
            var historyDir = Path.Combine(dir, RocksDbChainStoreBundle.HistorySubDir);

            using (var historyOnly = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = historyDir }, CatalogueScope.History))
            {
                foreach (var b in blocks)
                {
                    var key = HistoryKeys.BlockKey((ulong)b.Header.BlockNumber.ToBigInteger());
                    Assert.NotNull(historyOnly.Get(HistoryColumnFamilies.BlockHeader, key));
                    Assert.NotNull(historyOnly.Get(HistoryColumnFamilies.BlockMeta, key));
                }
                var txKey = HistoryKeys.TxKey((ulong)blocks[0].Header.BlockNumber.ToBigInteger(), 0);
                Assert.NotNull(historyOnly.Get(HistoryColumnFamilies.TxBody, txKey));
                Assert.NotNull(historyOnly.Get(HistoryColumnFamilies.ReceiptBody, txKey));
                Assert.NotNull(historyOnly.Get(HistoryColumnFamilies.TxHashIndex, blocks[0].Transactions[0].Hash));
            }

            using (var coreOnly = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = coreDir }, CatalogueScope.Core))
            {
                Assert.False(coreOnly.HasColumnFamily(HistoryColumnFamilies.BlockHeader));
                Assert.False(coreOnly.HasColumnFamily(HistoryColumnFamilies.TxBody));
                Assert.False(coreOnly.HasColumnFamily(HistoryColumnFamilies.ReceiptBody));
                Assert.False(coreOnly.HasColumnFamily(HistoryColumnFamilies.Control));
                Assert.True(coreOnly.HasColumnFamily(RocksDbManager.CF_METADATA));
            }
        }

        [Fact]
        public async Task SplitBundle_BulkSyncFirehose_AllFourRpcReads_Resolve()
        {
            var dir = Path.Combine(_root, "firehose-rpcs");
            var opts = SplitOptions();
            var block = MakeBlock(2100);

            using var bundle = RocksDbChainStoreBundle.Open(dir, bulkSync: true, storageOptions: opts);
            await ((IBatchedBlockPersister)bundle).PersistBlocksAsync(new List<PersistableBlock> { block });
            ((IBulkDurabilityBoundary)bundle).CheckpointBulk();

            var n = block.Header.BlockNumber.ToBigInteger();

            var header = await bundle.Blocks.GetByNumberAsync(n);
            Assert.NotNull(header);
            Assert.Equal(n, header.BlockNumber.ToBigInteger());

            var tx = await bundle.Transactions.GetByHashAsync(block.Transactions[0].Hash);
            Assert.NotNull(tx);

            var receipts = await bundle.Receipts.GetByBlockHashAsync(block.Hash);
            Assert.Equal(block.Receipts.Count, receipts.Count);

            var logs = await bundle.Logs.GetLogsByBlockNumberAsync(n);
            Assert.Equal(block.Logs.Sum(l => l.Logs.Count), logs.Count);
        }

        [Fact]
        public async Task SplitBundle_BulkSyncFirehose_Reopen_ResumesFromHistorySideCursor()
        {
            var dir = Path.Combine(_root, "firehose-resume");
            var opts = SplitOptions();
            var firstChunk = new List<PersistableBlock> { MakeBlock(2200), MakeBlock(2201) };
            var secondChunk = new List<PersistableBlock> { MakeBlock(2202) };
            var historyDir = Path.Combine(dir, RocksDbChainStoreBundle.HistorySubDir);
            var scratch = Path.Combine(_root, "resume-probe-scratch");

            using (var bundle = RocksDbChainStoreBundle.Open(dir, bulkSync: true, storageOptions: opts))
            {
                await ((IBatchedBlockPersister)bundle).PersistBlocksAsync(firstChunk);
                bundle.FinishBulkSync();
            }

            using (var historyOnly = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = historyDir }, CatalogueScope.History))
            {
                var probe = new SyncBulkSaveService(historyOnly.Database, scratch);
                Assert.Equal((ulong)2201, probe.LastCompletedBlock());
            }
            var coreDir = Path.Combine(dir, RocksDbChainStoreBundle.CoreSubDir);
            using (var coreOnly = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = coreDir }, CatalogueScope.Core))
                Assert.False(coreOnly.HasColumnFamily(HistoryColumnFamilies.Control));

            using (var reopened = RocksDbChainStoreBundle.Open(dir, bulkSync: true, storageOptions: opts))
            {
                var firstBlockHeader = await reopened.Blocks.GetByNumberAsync(2200);
                Assert.NotNull(firstBlockHeader);

                await ((IBatchedBlockPersister)reopened).PersistBlocksAsync(secondChunk);
                reopened.FinishBulkSync();
            }

            using (var historyOnly = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = historyDir }, CatalogueScope.History))
            {
                var probe = new SyncBulkSaveService(historyOnly.Database, scratch);
                Assert.Equal((ulong)2202, probe.LastCompletedBlock());
            }
        }

        [Fact]
        public async Task SplitBundle_AtomicFlush_WithArmedWithdrawals_FoldsIntoHistoryDb_NoCrossDbCorruption()
        {
            var dir = Path.Combine(_root, "withdrawals");
            var block = MakeBlock(800);
            var hash = block.Hash;

            using var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: SplitOptions());
            await bundle.Blocks.SaveAsync(block.Header, hash);

            var withdrawals = new List<Withdrawal>
            {
                new Withdrawal { Index = 1, ValidatorIndex = 2, Address = Fill(0xDE, 20), AmountInGwei = 100 }
            };

            var flush = (IAtomicBlockFlush)bundle;
            flush.ArmWithdrawals(800, withdrawals);
            await flush.FlushBlockAsync(flat: null, block: 800, hash: hash);
            await flush.DrainAsync();

            var readBack = await bundle.Withdrawals.GetByBlockHashAsync(hash);
            Assert.NotNull(readBack);
            Assert.Single(readBack);
            Assert.Equal(withdrawals[0].Index, readBack[0].Index);
        }

        [Fact]
        public void PairingGuard_FreshBothAbsent_StampsBothWithSameId()
        {
            var coreDir = Path.Combine(_root, "pg-fresh-core");
            var historyDir = Path.Combine(_root, "pg-fresh-history");
            var guard = new StorePairingGuard(coreDir, historyDir);

            guard.EnsurePaired();

            var coreMarker = File.ReadAllText(Path.Combine(coreDir, ".pairing-id"));
            var historyMarker = File.ReadAllText(Path.Combine(historyDir, ".pairing-id"));
            Assert.Equal(coreMarker, historyMarker);

            guard.EnsurePaired();
            Assert.Equal(coreMarker, File.ReadAllText(Path.Combine(coreDir, ".pairing-id")));
        }

        [Fact]
        public void PairingGuard_OneSidedMarker_Throws()
        {
            var coreDir = Path.Combine(_root, "pg-onesided-core");
            var historyDir = Path.Combine(_root, "pg-onesided-history");
            Directory.CreateDirectory(coreDir);
            Directory.CreateDirectory(historyDir);
            File.WriteAllText(Path.Combine(coreDir, ".pairing-id"), "abc123");

            var guard = new StorePairingGuard(coreDir, historyDir);
            var ex = Assert.Throws<InvalidOperationException>(() => guard.EnsurePaired());
            Assert.Contains("partial-directory hazard", ex.Message);
        }

        [Fact]
        public void PairingGuard_MismatchedIds_Throws()
        {
            var coreDir = Path.Combine(_root, "pg-mismatch-core");
            var historyDir = Path.Combine(_root, "pg-mismatch-history");
            Directory.CreateDirectory(coreDir);
            Directory.CreateDirectory(historyDir);
            File.WriteAllText(Path.Combine(coreDir, ".pairing-id"), "id-one");
            File.WriteAllText(Path.Combine(historyDir, ".pairing-id"), "id-two");

            var guard = new StorePairingGuard(coreDir, historyDir);
            var ex = Assert.Throws<InvalidOperationException>(() => guard.EnsurePaired());
            Assert.Contains("do not share a pairing id", ex.Message);
        }

        [Fact]
        public async Task SplitBundle_Boot_StampsPairing_ThenReopenSucceeds()
        {
            var dir = Path.Combine(_root, "pg-boot");
            using (var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: SplitOptions()))
            {
                await bundle.EnsureConsistentHeadAsync(_ => { });
            }

            var coreDir = Path.Combine(dir, RocksDbChainStoreBundle.CoreSubDir);
            var historyDir = Path.Combine(dir, RocksDbChainStoreBundle.HistorySubDir);
            Assert.True(File.Exists(Path.Combine(coreDir, ".pairing-id")));
            Assert.True(File.Exists(Path.Combine(historyDir, ".pairing-id")));

            using var reopened = RocksDbChainStoreBundle.Open(dir, storageOptions: SplitOptions());
            await reopened.EnsureConsistentHeadAsync(_ => { });
        }

        [Fact]
        public async Task SplitBundle_Boot_OneSidedDirectory_RefusesToBoot()
        {
            var dir = Path.Combine(_root, "pg-cw10");
            using (var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: SplitOptions()))
            {
                await bundle.EnsureConsistentHeadAsync(_ => { });
            }

            var historyDir = Path.Combine(dir, RocksDbChainStoreBundle.HistorySubDir);
            File.Delete(Path.Combine(historyDir, ".pairing-id"));

            using var reopened = RocksDbChainStoreBundle.Open(dir, storageOptions: SplitOptions());
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => reopened.EnsureConsistentHeadAsync(_ => { }));
            Assert.Contains("partial-directory hazard", ex.Message);
        }

        [Fact]
        public async Task CW5_HistoryDurableAheadOfCoreCursor_ReExecOverwritesDeterministically()
        {
            var dir = Path.Combine(_root, "cw5");
            using var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: SplitOptions());

            var block = MakeBlock(900);

            await PersistPerBlockAsync(bundle, block);
            Assert.Equal((ulong)0, bundle.Metadata.GetLastBlock());
            var firstReadHeader = await bundle.Blocks.GetByNumberAsync(900);
            Assert.NotNull(firstReadHeader);

            var reExecBlock = MakeBlock(900);
            await PersistPerBlockAsync(bundle, reExecBlock);

            var afterReExecHeader = await bundle.Blocks.GetByNumberAsync(900);
            Assert.NotNull(afterReExecHeader);
            Assert.Equal(firstReadHeader.BlockNumber.ToBigInteger(), afterReExecHeader.BlockNumber.ToBigInteger());
            var hashByNum = await bundle.Blocks.GetHashByNumberAsync(900);
            Assert.Equal(block.Hash.ToHex(), hashByNum.ToHex());
            var txs = await bundle.Transactions.GetByBlockHashAsync(block.Hash);
            Assert.Equal(block.Transactions.Count, txs.Count);

            await bundle.EnsureConsistentHeadAsync(_ => { });
        }

        [Fact]
        public async Task SplitBundle_Checkpoint_SaveAndRestore_RoundTrips_HistoryFirst()
        {
            var dir = Path.Combine(_root, "checkpoint");
            var stateRoot = Fill(0xAB, 32);
            var blockHash1 = Fill(0x01, 32);

            RocksDbChainStoreBundle bundleForRestore;
            {
                using var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: SplitOptions());
                var block1 = MakeBlock(1000);
                await PersistPerBlockAsync(bundle, block1);

                var checkpoint = await bundle.SaveCheckpointAsync(1000, stateRoot, block1.Hash);
                Assert.Equal((ulong)1000, checkpoint.BlockNumber);

                var coreDir = Path.Combine(dir, RocksDbChainStoreBundle.CoreSubDir);
                var historyDir = Path.Combine(dir, RocksDbChainStoreBundle.HistorySubDir);
                Assert.True(Directory.Exists(Path.Combine(coreDir, ".cp", "000000001000")));
                Assert.True(Directory.Exists(Path.Combine(historyDir, ".cp", "000000001000")));

                var block2 = MakeBlock(1001);
                await PersistPerBlockAsync(bundle, block2);
                var afterAdvance = await bundle.Blocks.GetByNumberAsync(1001);
                Assert.NotNull(afterAdvance);
                bundleForRestore = bundle;
            }

            await bundleForRestore.RestoreCheckpointAsync(1000);

            using var reopened = RocksDbChainStoreBundle.Open(dir, storageOptions: SplitOptions());
            var restoredHeader = await reopened.Blocks.GetByNumberAsync(1000);
            Assert.NotNull(restoredHeader);
            await reopened.EnsureConsistentHeadAsync(_ => { });
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
                    new Log { Address = "0x" + new string('a', 40), Topics = new List<byte[]> { Fill(0x11, 32) }, Data = new byte[] { (byte)j } }
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
                bloom: Fill(0x00, 256));
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
