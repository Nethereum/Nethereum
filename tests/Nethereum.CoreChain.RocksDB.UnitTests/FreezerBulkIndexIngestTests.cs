using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class FreezerBulkIndexIngestTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"freezerbulkidx_{Guid.NewGuid():N}");
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

        private RocksDbStorageOptions BulkOptions(bool backgroundIndexing = false) => new RocksDbStorageOptions
        {
            UseFreezerHistory = true,
            FreezerHistoryDirectory = FreezerDir,
            PromotionEnabled = false,
            BackgroundFreezeIndexing = backgroundIndexing,
        };

        private RocksDbChainStoreBundle OpenBundle(bool backgroundIndexing = false)
            => RocksDbChainStoreBundle.Open(DataDir, null, false, BulkOptions(backgroundIndexing), _signer);

        private static async Task SetTipHeightAsync(RocksDbChainStoreBundle bundle, long height)
        {
            var hash = Fill32(999);
            await bundle.Blocks.SaveAsync(MakeHeader(height, hash), hash);
        }

        [Fact]
        public async Task Given_ABulkFreezeRun_Then_TheByHashCursorAdvancesDuringTheRun_AtWindowHeadPlusOne()
        {
            using var bundle = OpenBundle();
            var blocks = MakeChainedBlocks(0, 5);
            await SetTipHeightAsync(bundle, ImmutabilityThreshold + 4);

            await bundle.PersistBlocksAsync(blocks);

            Assert.Equal(5, bundle.FreezerHead);
            Assert.Equal(bundle.FreezerHead, (long)bundle.ByHashIndexedHead);

            for (var i = 0; i < 5; i++)
            {
                var header = await bundle.Blocks.GetByHashAsync(blocks[i].Hash);
                Assert.NotNull(header);
                Assert.Equal(i, (int)header.BlockNumber.ToBigInteger());
            }
        }

        [Fact]
        public async Task Given_AKillBetweenFreezerAppendAndBoundary_When_ReopenedAndFinishBulkIndexingRuns_Then_ItHealsThatWindow()
        {
            var firstBatch = MakeChainedBlocks(0, 5);
            var secondBatch = MakeChainedBlocks(5, 3);

            using (var bundle = OpenBundle())
            {
                await SetTipHeightAsync(bundle, ImmutabilityThreshold + 7);
                await bundle.PersistBlocksAsync(firstBatch);
                Assert.Equal(5, bundle.ByHashIndexedHead);

                await bundle.PersistBlocksAsync(secondBatch);
                Assert.Equal(8, bundle.FreezerHead);
                Assert.Equal(5, bundle.ByHashIndexedHead);
            }

            using (var reopened = OpenBundle())
            {
                Assert.Equal(8, reopened.FreezerHead);
                Assert.Equal(5, reopened.ByHashIndexedHead);

                reopened.FinishBulkIndexing();
                Assert.Equal(8, reopened.ByHashIndexedHead);

                for (var i = 5; i < 8; i++)
                {
                    var header = await reopened.Blocks.GetByHashAsync(secondBatch[i - 5].Hash);
                    Assert.NotNull(header);
                    Assert.Equal(i, (int)header.BlockNumber.ToBigInteger());
                }
            }
        }

        [Fact]
        public async Task Given_TheCursorAlreadyAtHead_When_Reopened_Then_ItStaysUnchanged()
        {
            var blocks = MakeChainedBlocks(0, 5);
            using (var bundle = OpenBundle())
            {
                await SetTipHeightAsync(bundle, ImmutabilityThreshold + 4);
                await bundle.PersistBlocksAsync(blocks);
                Assert.Equal(5, bundle.ByHashIndexedHead);
            }

            using var reopened = OpenBundle();
            Assert.Equal(5, reopened.FreezerHead);
            Assert.Equal(5, reopened.ByHashIndexedHead);
        }

        [Fact]
        public async Task Given_APendingFreezerWindow_When_CheckpointBulkIsCalled_Then_ItIsIngestedImmediately()
        {
            using var bundle = OpenBundle();
            await SetTipHeightAsync(bundle, ImmutabilityThreshold + 7);

            await bundle.PersistBlocksAsync(MakeChainedBlocks(0, 5));
            var pending = MakeChainedBlocks(5, 3);
            await bundle.PersistBlocksAsync(pending);
            Assert.Equal(5, bundle.ByHashIndexedHead);

            ((IBulkDurabilityBoundary)bundle).CheckpointBulk();

            Assert.Equal(8, bundle.ByHashIndexedHead);
            for (var i = 0; i < 3; i++)
                Assert.NotNull(await bundle.Blocks.GetByHashAsync(pending[i].Hash));
        }

        [Fact]
        public void Given_NoUseFreezerHistory_When_FinishBulkIndexingIsCalled_Then_NoOp()
        {
            var dataDir = Path.Combine(_root, "nofreezer");
            using var bundle = RocksDbChainStoreBundle.Open(dataDir);
            ((IBulkDurabilityBoundary)bundle).FinishBulkIndexing();
            ((IBulkDurabilityBoundary)bundle).CheckpointBulk();
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
    }
}
