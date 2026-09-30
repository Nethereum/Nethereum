using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class FreezerHistoryDbTopologyTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"freezertopology_{Guid.NewGuid():N}");
        private readonly ITransactionVerificationAndRecovery _signer = new TransactionVerificationAndRecoveryImp();
        private readonly EthECKey _senderKey = EthECKey.GenerateKey();
        private readonly LegacyTransactionSigner _txSigner = new LegacyTransactionSigner();

        private const long TipHeight = 90_010;
        private const long FreezeBoundary = 10;
        private const int DrainCount = 16;

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        private string DataDir => Path.Combine(_root, "data");
        private string FreezerDir => Path.Combine(_root, "freezer");

        private RocksDbStorageOptions FreezerOptions() => new RocksDbStorageOptions
        {
            UseFreezerHistory = true,
            FreezerHistoryDirectory = FreezerDir,
        };

        private RocksDbChainStoreBundle OpenBundle()
            => RocksDbChainStoreBundle.Open(DataDir, null, false, FreezerOptions(), _signer);

        private RocksDbManager OpenFreezerHistoryReaderManager()
            => new RocksDbManager(
                new RocksDbStorageOptions { DatabasePath = Path.Combine(DataDir, RocksDbChainStoreBundle.FreezerHistorySubDir) },
                CatalogueScope.FreezerHistory);

        private async Task<List<PersistableBlock>> PersistDrainAsync()
        {
            var blocks = MakeBlocks(0, DrainCount);
            using (var bundle = OpenBundle())
            {
                await SetTipHeightAsync(bundle, TipHeight);
                await bundle.PersistBlocksAsync(blocks);
            }
            return blocks;
        }

        [Fact]
        public async Task Given_UseFreezerHistory_When_Opened_Then_FrozenIndexCfsLiveInFreezerHistoryDbNotCore()
        {
            var blocks = await PersistDrainAsync();
            var frozen = blocks[3];

            using (var core = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = DataDir }))
            {
                var coreIndex = new RocksDbRandomKeyIndexStore(core);
                Assert.False(coreIndex.TryGetBlockNumberByHash(frozen.Hash, out _));
            }

            using (var freezerHistory = OpenFreezerHistoryReaderManager())
            {
                var freezerIndex = new RocksDbRandomKeyIndexStore(freezerHistory);
                Assert.True(freezerIndex.TryGetBlockNumberByHash(frozen.Hash, out var number));
                Assert.Equal(frozen.Header.BlockNumber.ToBigInteger(), (BigInteger)number);
            }
        }

        [Fact]
        public async Task Given_FreezerHistoryDb_When_Opened_Then_OnlyTheThreeNarrowCfsExist()
        {
            await PersistDrainAsync();

            using var freezerHistory = OpenFreezerHistoryReaderManager();

            var expected = new[]
            {
                HistoryColumnFamilies.BlockHashIndex,
                HistoryColumnFamilies.TxHashIndex,
                HistoryColumnFamilies.LogFilterMaps,
                HistoryColumnFamilies.Control,
            };
            Assert.Equal(expected.OrderBy(n => n), freezerHistory.OpenColumnFamilyNames.OrderBy(n => n));

            Assert.True(freezerHistory.HasColumnFamily(HistoryColumnFamilies.Control));
            Assert.False(freezerHistory.HasColumnFamily(HistoryColumnFamilies.BlockHeader));
            Assert.False(freezerHistory.HasColumnFamily(HistoryColumnFamilies.TxBody));
            Assert.False(freezerHistory.HasColumnFamily(HistoryColumnFamilies.ReceiptBody));
            Assert.False(freezerHistory.HasColumnFamily(HistoryColumnFamilies.BlockMeta));
        }

        [Fact]
        public void Given_UseFreezerHistory_And_PromotionOff_When_Opened_Then_NoHotWindowBuilt_NoCrash()
        {
            RocksDbChainStoreBundle bundle = null;
            var ex = Record.Exception(() => bundle = OpenBundle());
            Assert.Null(ex);

            Assert.IsType<FreezerAwareBlockStore>(bundle.Blocks);
            bundle.Dispose();

            using var core = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = DataDir });
            Assert.False(core.HasColumnFamily(RocksDbManager.CF_HOT_BLOCK_HEADER));
        }

        [Fact]
        public async Task Given_ARecentUnfrozenBlock_When_GetByHash_Then_ItResolves_FromTheHotIndex()
        {
            var blocks = await PersistDrainAsync();
            var recent = blocks[12];

            using var bundle = OpenBundle();

            var header = await bundle.Blocks.GetByHashAsync(recent.Hash);
            Assert.NotNull(header);
            Assert.Equal(recent.Header.BlockNumber.ToBigInteger(), header.BlockNumber.ToBigInteger());
        }

        [Fact]
        public async Task Given_AFrozenBlock_When_GetByHash_Then_ItResolves_FromTheFreezerHistoryIndex()
        {
            var blocks = await PersistDrainAsync();
            var frozen = blocks[3];

            using var bundle = OpenBundle();

            var header = await bundle.Blocks.GetByHashAsync(frozen.Hash);
            Assert.NotNull(header);
            Assert.Equal(frozen.Header.BlockNumber.ToBigInteger(), header.BlockNumber.ToBigInteger());
        }

        [Fact]
        public async Task Given_NonFreezerConfig_When_Opened_Then_ByteIdenticalToday()
        {
            var dataDir = Path.Combine(_root, "nonfreezer");
            Directory.CreateDirectory(dataDir);

            using (var bundle = RocksDbChainStoreBundle.Open(dataDir))
            {
                var hash = Fill(0xAB, 32);
                await bundle.Blocks.SaveAsync(MakeHeader(1, hash), hash);
                var header = await bundle.Blocks.GetByHashAsync(hash);
                Assert.NotNull(header);
                Assert.Equal(1, (int)header.BlockNumber.ToBigInteger());
            }

            Assert.False(Directory.Exists(Path.Combine(dataDir, RocksDbChainStoreBundle.FreezerHistorySubDir)));
        }

        private static async Task SetTipHeightAsync(RocksDbChainStoreBundle bundle, long height)
        {
            var hash = Fill(0xEE, 32);
            await bundle.Blocks.SaveAsync(MakeHeader(height, hash), hash);
        }

        private List<PersistableBlock> MakeBlocks(long start, int count)
        {
            var list = new List<PersistableBlock>(count);
            for (var i = 0; i < count; i++) list.Add(MakeBlock(start + i));
            return list;
        }

        private PersistableBlock MakeBlock(long number)
        {
            var hash = Fill((byte)(number & 0xFF), 32);
            var header = MakeHeader(number, hash);

            var txs = new List<ISignedTransaction> { MakeTx(number, 0), MakeTx(number, 1) };

            var receipts = new List<ReceiptSaveItem>();
            BigInteger cumulative = 0;
            for (var j = 0; j < txs.Count; j++)
            {
                cumulative += 21000;
                var rcptLogs = new List<Log>
                {
                    new Log { Address = "0x" + new string('a', 40), Topics = new List<byte[]> { Fill(0x11, 32) }, Data = new byte[] { (byte)j } }
                };
                var rcpt = new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = cumulative, Logs = rcptLogs };
                receipts.Add(new ReceiptSaveItem(rcpt, txs[j].Hash, j, 21000, null, 1_000_000_000));
            }

            return new PersistableBlock(
                header, hash,
                uncles: new List<BlockHeader>(),
                withdrawals: null,
                transactions: txs,
                receipts: receipts,
                logs: new List<(List<Log> Logs, byte[] TxHash, int TxIndex)>(),
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

        private ISignedTransaction MakeTx(long number, int index)
        {
            var tx = new LegacyTransaction(
                nonce: CanonicalTestScalars.Scalar((number + 1) * 1000 + index),
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: Array.Empty<byte>(),
                data: Array.Empty<byte>());
            _txSigner.SignTransaction(_senderKey.GetPrivateKeyAsBytes(), tx);
            return tx;
        }

        private static byte[] Fill(byte v, int len)
        {
            var b = new byte[len];
            for (var i = 0; i < len; i++) b[i] = v;
            return b;
        }
    }
}
