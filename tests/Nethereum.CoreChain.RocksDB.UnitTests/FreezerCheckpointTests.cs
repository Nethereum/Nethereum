using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class FreezerCheckpointTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"freezercp_{Guid.NewGuid():N}");
        private readonly ITransactionVerificationAndRecovery _signer = new TransactionVerificationAndRecoveryImp();
        private readonly EthECKey _senderKey = EthECKey.GenerateKey();
        private readonly LegacyTransactionSigner _txSigner = new LegacyTransactionSigner();

        private const long ImmutabilityThreshold = 90_000;

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        private string DataDir => Path.Combine(_root, "data");
        private string FreezerDir => Path.Combine(_root, "freezer");

        private static readonly Nethereum.Freezer.FilterMaps.FilterMapsParams TinyParams =
            new Nethereum.Freezer.FilterMaps.FilterMapsParams(
                logMapHeight: 4, logMapWidth: 8, logMapsPerEpoch: 1, logValuesPerMap: 3,
                baseRowGroupSize: 2, baseRowLengthRatio: 4, logLayerDiff: 4);

        private RocksDbStorageOptions FollowOptions() => new RocksDbStorageOptions
        {
            UseFreezerHistory = true,
            FreezerHistoryDirectory = FreezerDir,
            PromotionEnabled = true,
            FilterMapsIndexParams = TinyParams,
        };

        private RocksDbChainStoreBundle OpenBundle()
            => RocksDbChainStoreBundle.Open(DataDir, null, false, FollowOptions(), _signer);

        private RocksDbStorageOptions BulkOptions() => new RocksDbStorageOptions
        {
            UseFreezerHistory = true,
            FreezerHistoryDirectory = FreezerDir,
            PromotionEnabled = false,
            FilterMapsIndexParams = TinyParams,
        };

        private RocksDbChainStoreBundle OpenBulkBundle()
            => RocksDbChainStoreBundle.Open(DataDir, null, false, BulkOptions(), _signer);

        private static void DriveFreezerPromotion(RocksDbChainStoreBundle bundle)
            => bundle.DriveFreezerPromotionForTests();

        private static void StampTipHeight(RocksDbManager rocks, long height)
            => rocks.Put(RocksDbManager.CF_METADATA, System.Text.Encoding.UTF8.GetBytes("height"),
                Nethereum.CoreChain.RocksDB.Serialization.RocksDbSerializer.BigIntegerToBytes(height));

        private static RocksDbManager CoreManagerOf(RocksDbChainStoreBundle bundle)
            => bundle.Rocks;

        [Fact]
        public async Task Given_AFrozenIndexedNode_When_Checkpointed_AndRestored_Then_FrozenBlocksStillResolveByHashAndLogs()
        {
            RocksDbChainStoreBundle bundleForRestore;
            var blocks = MakeChainedBlocksWithLogs(0, 11);

            using (var bundle = OpenBundle())
            {
                foreach (var b in blocks)
                    await WriteToHotAsync(bundle, b);

                StampTipHeight(CoreManagerOf(bundle), ImmutabilityThreshold + 10);
                DriveFreezerPromotion(bundle);

                Assert.Equal(11, bundle.FreezerHead);
                Assert.Equal(11, bundle.ByHashIndexedHead);
                Assert.True(bundle.LogIndexRenderedHead > 0);

                await bundle.SaveCheckpointAsync(500, FillBytes(0xAA), FillBytes(0xBB));

                bundleForRestore = bundle;
            }

            await bundleForRestore.RestoreCheckpointAsync(500);

            using var reopened = OpenBundle();

            Assert.Equal(11, reopened.FreezerHead);

            for (var i = 0; i < 11; i++)
            {
                var header = await reopened.Blocks.GetByHashAsync(blocks[i].Hash);
                Assert.NotNull(header);
                Assert.Equal(i, (int)header.BlockNumber.ToBigInteger());
            }

            var indexedHead = reopened.LogIndexRenderedHead - 1;
            Assert.True(indexedHead >= 0, "the log index must have survived the restore");
            var filter = new LogFilter
            {
                Addresses = new List<string> { LogAddress },
                FromBlock = 0,
                ToBlock = indexedHead,
            };
            var results = await reopened.Logs.GetLogsAsync(filter);
            Assert.NotEmpty(results);
        }

        [Fact]
        public async Task Given_BulkAppendRacingTheCheckpoint_When_Restored_Then_TheByHashIndexSelfHeals()
        {
            const int blockCount = 30;
            RocksDbChainStoreBundle bundleForRestore;
            var blocks = MakeChainedBlocks(0, blockCount);

            using (var bundle = OpenBulkBundle())
            {
                StampTipHeight(CoreManagerOf(bundle), ImmutabilityThreshold + blockCount - 1);

                var appendTask = Task.Run(() => bundle.PersistBlocksAsync(blocks));
                var checkpointTask = Task.Run(() => bundle.SaveCheckpointAsync(700, FillBytes(0xCC), FillBytes(0xDD)));
                await Task.WhenAll(appendTask, checkpointTask);

                bundleForRestore = bundle;
            }

            await bundleForRestore.RestoreCheckpointAsync(700);

            using var reopened = OpenBulkBundle();
            reopened.FinishBulkIndexing();

            Assert.InRange(reopened.FreezerHead, 0, blockCount);
            Assert.Equal(reopened.FreezerHead, reopened.ByHashIndexedHead);

            for (var i = 0; i < reopened.FreezerHead; i++)
            {
                var header = await reopened.Blocks.GetByHashAsync(blocks[i].Hash);
                Assert.NotNull(header);
                Assert.Equal(i, (int)header.BlockNumber.ToBigInteger());
            }
        }

        [Fact]
        public async Task Given_TheProductionRecoveryPath_When_ARestoreIsPending_Then_TheFreezerIsRestoredConsistently()
        {
            var blocks = MakeChainedBlocksWithLogs(0, 16);

            using (var bundle = OpenBundle())
            {
                foreach (var b in blocks.GetRange(0, 11))
                    await WriteToHotAsync(bundle, b);

                StampTipHeight(CoreManagerOf(bundle), ImmutabilityThreshold + 10);
                DriveFreezerPromotion(bundle);

                await bundle.SaveCheckpointAsync(500, FillBytes(0x11), FillBytes(0x22));

                foreach (var b in blocks.GetRange(11, 5))
                    await WriteToHotAsync(bundle, b);
                StampTipHeight(CoreManagerOf(bundle), ImmutabilityThreshold + 15);
                DriveFreezerPromotion(bundle);
                Assert.Equal(16, bundle.FreezerHead);
            }

            var gate = new BootRecoveryGate(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
            Assert.True(gate.RecordRestoreRequest(DataDir, 500));
            gate.ApplyPendingRestore(DataDir, FreezerDir, promotionEnabled: true);

            using var reopened = OpenBundle();

            Assert.Equal(11, reopened.FreezerHead);
            Assert.Equal(reopened.FreezerHead, reopened.ByHashIndexedHead);

            for (var i = 0; i < 11; i++)
            {
                var header = await reopened.Blocks.GetByHashAsync(blocks[i].Hash);
                Assert.NotNull(header);
                Assert.Equal(i, (int)header.BlockNumber.ToBigInteger());
            }

            var indexedHead = reopened.LogIndexRenderedHead - 1;
            Assert.True(indexedHead >= 0, "the log index must have survived the production restore path");
            var filter = new LogFilter
            {
                Addresses = new List<string> { LogAddress },
                FromBlock = 0,
                ToBlock = indexedHead,
            };
            var results = await reopened.Logs.GetLogsAsync(filter);
            Assert.NotEmpty(results);
        }

        [Fact]
        public async Task Given_ArchiveBehindTheCheckpoint_When_Restored_Then_FailsBeforeAnyRocksDbSwapCommits()
        {
            var blocks = MakeChainedBlocks(0, 16);
            string snapshotDir;

            using (var bundle = OpenBundle())
            {
                foreach (var b in blocks)
                    await WriteToHotAsync(bundle, b);

                StampTipHeight(CoreManagerOf(bundle), ImmutabilityThreshold + 15);
                DriveFreezerPromotion(bundle);
                Assert.Equal(16, bundle.FreezerHead);

                await bundle.SaveCheckpointAsync(600, FillBytes(0x33), FillBytes(0x44));
                snapshotDir = RocksDbCheckpointManager.ResolveCheckpointSnapshotPath(DataDir, 600);
            }

            using (var freezer = Nethereum.Freezer.Freezer.Open(
                new Nethereum.Freezer.FreezerLayout(FreezerDir), Nethereum.Freezer.FreezerOpenMode.Append))
            {
                freezer.TruncateHead(5);
            }

            var currentBefore = File.ReadAllBytes(Path.Combine(DataDir, "CURRENT"));
            var restoreNewDir = Path.GetFullPath(DataDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".restore-new";
            var restoreOldDir = Path.GetFullPath(DataDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".restore-old";

            var ex = Assert.Throws<InvalidOperationException>(() =>
                RocksDbCheckpointManager.RestoreFromCheckpointDir(
                    snapshotDir, DataDir, promotionEnabled: true, freezerDirectory: FreezerDir));
            Assert.Contains("BEHIND the checkpoint", ex.Message);

            Assert.False(Directory.Exists(restoreNewDir), "a failed pre-check must never stage a replacement");
            Assert.False(Directory.Exists(restoreOldDir), "a failed pre-check must never move the live DB aside");
            Assert.Equal(currentBefore, File.ReadAllBytes(Path.Combine(DataDir, "CURRENT")));

            using var reopened = OpenBundle();
            Assert.Equal(5, reopened.FreezerHead);
        }

        private static readonly string LogAddress = "0x" + new string('0', 38) + "aa";

        private List<PersistableBlock> MakeChainedBlocksWithLogs(long start, int count)
        {
            var list = new List<PersistableBlock>(count);
            for (var i = 0; i < count; i++) list.Add(MakeBlockWithLog(start + i));
            return list;
        }

        private PersistableBlock MakeBlockWithLog(long number)
        {
            var hash = Fill32(number);
            var parentHash = number > 0 ? Fill32(number - 1) : new byte[32];
            var tx = MakeTx(number);

            var log = new Log
            {
                Address = LogAddress,
                Topics = new List<byte[]> { Topic((byte)((number % 7) + 1)) },
                Data = new byte[] { (byte)number },
            };
            var bloom = new LogBloomFilter();
            bloom.AddLog(log);

            var header = MakeHeader(number, parentHash);
            header.LogsBloom = bloom.Data;

            var receipt = new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = 21000, Logs = new List<Log> { log } };

            return new PersistableBlock(
                header, hash,
                uncles: new List<BlockHeader>(),
                withdrawals: null,
                transactions: new List<ISignedTransaction> { tx },
                receipts: new List<ReceiptSaveItem> { new ReceiptSaveItem(receipt, tx.Hash, 0, 21000, null, 1_000_000_000) },
                logs: new List<(List<Log> Logs, byte[] TxHash, int TxIndex)>(),
                bloom: bloom.Data);
        }

        private static byte[] Topic(byte seed)
        {
            var bytes = new byte[32];
            bytes[31] = seed;
            return bytes;
        }

        private static async Task WriteToHotAsync(RocksDbChainStoreBundle bundle, PersistableBlock block)
        {
            await bundle.Blocks.SaveAsync(block.Header, block.Hash).ConfigureAwait(false);
            await bundle.Transactions.SaveManyAsync(block.Hash, block.Header.BlockNumber.ToBigInteger(), block.Transactions).ConfigureAwait(false);
            await bundle.Uncles.SaveAsync(block.Hash, block.Uncles).ConfigureAwait(false);
            await bundle.Withdrawals.SaveAsync(block.Hash, block.Withdrawals).ConfigureAwait(false);
            await bundle.Receipts.SaveManyAsync(block.Hash, block.Header.BlockNumber.ToBigInteger(), block.Receipts).ConfigureAwait(false);
        }

        private List<PersistableBlock> MakeChainedBlocks(long start, int count)
        {
            var list = new List<PersistableBlock>(count);
            for (var i = 0; i < count; i++) list.Add(MakeBlock(start + i));
            return list;
        }

        private PersistableBlock MakeBlock(long number)
        {
            var hash = Fill32(number);
            var parentHash = number > 0 ? Fill32(number - 1) : new byte[32];
            var header = MakeHeader(number, parentHash);

            var tx = MakeTx(number);
            var receipt = new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = 21000, Logs = new List<Log>() };

            return new PersistableBlock(
                header, hash,
                uncles: new List<BlockHeader>(),
                withdrawals: null,
                transactions: new List<ISignedTransaction> { tx },
                receipts: new List<ReceiptSaveItem> { new ReceiptSaveItem(receipt, tx.Hash, 0, 21000, null, 1_000_000_000) },
                logs: new List<(List<Log> Logs, byte[] TxHash, int TxIndex)>(),
                bloom: new byte[256]);
        }

        private static BlockHeader MakeHeader(long number, byte[] parentHash) => new BlockHeader
        {
            BlockNumber = new EvmUInt256((ulong)number),
            ParentHash = parentHash,
            TransactionsHash = new byte[32],
            UnclesHash = new byte[32],
            ReceiptHash = new byte[32],
            StateRoot = new byte[32],
            Difficulty = new EvmUInt256(1UL),
            GasLimit = 1,
            Timestamp = 1000 + number,
            ExtraData = Array.Empty<byte>(),
            MixHash = new byte[32],
            Nonce = new byte[8],
            LogsBloom = new byte[256],
            Coinbase = "0x0000000000000000000000000000000000000000",
        };

        private ISignedTransaction MakeTx(long number)
        {
            var tx = new LegacyTransaction(
                nonce: CanonicalTestScalars.Scalar(number + 1),
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: Array.Empty<byte>(),
                data: Array.Empty<byte>());
            _txSigner.SignTransaction(_senderKey.GetPrivateKeyAsBytes(), tx);
            return tx;
        }

        private static byte[] Fill32(long number)
        {
            var b = new byte[32];
            b[24] = 0x77;
            b[28] = (byte)(number >> 24);
            b[29] = (byte)(number >> 16);
            b[30] = (byte)(number >> 8);
            b[31] = (byte)number;
            return b;
        }

        private static byte[] FillBytes(byte value)
        {
            var b = new byte[32];
            for (var i = 0; i < 32; i++) b[i] = value;
            return b;
        }
    }
}
