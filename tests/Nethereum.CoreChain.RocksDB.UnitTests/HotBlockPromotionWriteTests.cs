using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.History;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class HotBlockPromotionWriteTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"promotionwrite_{Guid.NewGuid():N}");

        public HotBlockPromotionWriteTests() => Directory.CreateDirectory(_root);

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        [Fact]
        public async Task HotBlock_AfterPromotionFollow_HotHasCompleteMetaReceiptsUnclesWithdrawals()
        {
            var promotionDir = Path.Combine(_root, "promotion");
            var referenceDir = Path.Combine(_root, "reference");
            Directory.CreateDirectory(promotionDir);
            Directory.CreateDirectory(referenceDir);

            var block = MakeBlock();

            using var promotionManager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = promotionDir, PromotionEnabled = true });
            var bundle = RocksDbChainStoreBundle.FromManager(promotionManager, promotionDir, ownsManager: false);
            Assert.IsType<CompositeBlockStore>(bundle.Blocks);
            Assert.IsType<CompositeTransactionStore>(bundle.Transactions);
            await FollowAsync(bundle.Blocks, bundle.Transactions, bundle.Uncles, bundle.Withdrawals, bundle.Receipts, block);

            using var referenceManager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = referenceDir });
            var referenceBlocks = new RocksDbBlockStore(referenceManager);
            var referenceTransactions = new RocksDbTransactionStore(referenceManager, referenceBlocks);
            var referenceUncles = new RocksDbUncleStore(referenceManager, referenceBlocks);
            var referenceWithdrawals = new RocksDbWithdrawalStore(referenceManager, referenceBlocks);
            var referenceReceipts = new RocksDbReceiptStore(referenceManager, referenceBlocks);
            await FollowAsync(referenceBlocks, referenceTransactions, referenceUncles, referenceWithdrawals, referenceReceipts, block);

            AssertCfContentEqual(referenceManager, HistoryColumnFamilies.BlockHeader, promotionManager, RocksDbManager.CF_HOT_BLOCK_HEADER);
            AssertCfContentEqual(referenceManager, HistoryColumnFamilies.BlockMeta, promotionManager, RocksDbManager.CF_HOT_BLOCK_META);
            AssertCfContentEqual(referenceManager, HistoryColumnFamilies.BlockHashIndex, promotionManager, RocksDbManager.CF_HOT_BLOCK_HASH_INDEX);
            AssertCfContentEqual(referenceManager, HistoryColumnFamilies.TxBody, promotionManager, RocksDbManager.CF_HOT_TX_BODY);
            AssertCfContentEqual(referenceManager, HistoryColumnFamilies.TxHashIndex, promotionManager, RocksDbManager.CF_HOT_TX_HASH_INDEX);
            AssertCfContentEqual(referenceManager, HistoryColumnFamilies.ReceiptBody, promotionManager, RocksDbManager.CF_HOT_RECEIPT_BODY);

            var hotMetaBytes = promotionManager.Get(RocksDbManager.CF_HOT_BLOCK_META, HistoryKeys.BlockKey((ulong)block.Number));
            var hotMeta = BlockMetaCodec.Decode(hotMetaBytes);
            Assert.NotNull(hotMeta);
            Assert.Equal(block.Header.LogsBloom, hotMeta.Bloom);
            Assert.NotEmpty(hotMeta.Uncles);
            Assert.NotEmpty(hotMeta.Withdrawals);

            Assert.Equal(2, DumpCf(promotionManager, RocksDbManager.CF_HOT_RECEIPT_BODY).Count);
        }

        private static async Task FollowAsync(
            IBlockStore blocks, ITransactionStore transactions, IUncleStore uncles,
            IWithdrawalStore withdrawals, IReceiptStore receipts, TestBlock block)
        {
            await blocks.SaveAsync(block.Header, block.Hash).ConfigureAwait(false);
            await transactions.SaveManyAsync(block.Hash, block.Number, block.Transactions).ConfigureAwait(false);
            await uncles.SaveAsync(block.Hash, block.Uncles).ConfigureAwait(false);
            await withdrawals.SaveAsync(block.Hash, block.Withdrawals).ConfigureAwait(false);
            await receipts.SaveManyAsync(block.Hash, block.Number, block.Receipts).ConfigureAwait(false);
        }

        private static void AssertCfContentEqual(RocksDbManager referenceManager, string referenceCf, RocksDbManager promotionManager, string hotCf)
        {
            var reference = DumpCf(referenceManager, referenceCf);
            var hot = DumpCf(promotionManager, hotCf);
            Assert.True(reference.Count > 0, $"reference CF [{referenceCf}] is empty — the fixture wrote nothing to compare against");
            Assert.True(reference.Count == hot.Count, $"[{referenceCf}] vs [{hotCf}] row count differs: reference={reference.Count} hot={hot.Count}");
            foreach (var kv in reference)
            {
                Assert.True(hot.TryGetValue(kv.Key, out var hotVal), $"[{hotCf}] missing key present in [{referenceCf}]: {kv.Key}");
                Assert.Equal(kv.Value, hotVal);
            }
        }

        private static Dictionary<string, byte[]> DumpCf(RocksDbManager mgr, string cf)
        {
            var result = new Dictionary<string, byte[]>();
            using var it = mgr.CreateIterator(cf);
            it.SeekToFirst();
            while (it.Valid())
            {
                result[Convert.ToHexString(it.Key())] = it.Value();
                it.Next();
            }
            return result;
        }

        private sealed class TestBlock
        {
            public BlockHeader Header;
            public byte[] Hash;
            public BigInteger Number;
            public List<ISignedTransaction> Transactions;
            public IList<BlockHeader> Uncles;
            public IList<Withdrawal> Withdrawals;
            public IReadOnlyList<ReceiptSaveItem> Receipts;
        }

        private static TestBlock MakeBlock()
        {
            var hash = Fill32(500);
            var header = MakeHeader(500, hash);
            var txs = new List<ISignedTransaction> { MakeTx(0), MakeTx(1) };
            var uncleHash = Fill32(499);
            var uncle = MakeHeader(499, uncleHash);
            var withdrawal = new Withdrawal { Index = 1, ValidatorIndex = 7, Address = new byte[20], AmountInGwei = 42 };
            var receipts = new List<ReceiptSaveItem>
            {
                new ReceiptSaveItem(
                    new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = 21000, Logs = new List<Log>() },
                    txs[0].Hash, txIndex: 0, gasUsed: 21000, contractAddress: null, effectiveGasPrice: 1),
                new ReceiptSaveItem(
                    new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = 42000, Logs = new List<Log>() },
                    txs[1].Hash, txIndex: 1, gasUsed: 21000, contractAddress: null, effectiveGasPrice: 1),
            };
            return new TestBlock
            {
                Header = header,
                Hash = hash,
                Number = header.BlockNumber.ToBigInteger(),
                Transactions = txs,
                Uncles = new List<BlockHeader> { uncle },
                Withdrawals = new List<Withdrawal> { withdrawal },
                Receipts = receipts,
            };
        }

        private static BlockHeader MakeHeader(long number, byte[] hash)
        {
            var bloom = new byte[256];
            bloom[0] = 0xAB;
            bloom[255] = 0xCD;
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
                Timestamp = 1000,
                ExtraData = Array.Empty<byte>(),
                MixHash = hash,
                Nonce = new byte[8],
                LogsBloom = bloom,
                Coinbase = "0x0000000000000000000000000000000000000000",
            };
        }

        private static ISignedTransaction MakeTx(int index)
        {
            var r = new byte[32]; r[0] = 0x01;
            var s = new byte[32]; s[0] = 0x01;
            return new LegacyTransaction(
                nonce: CanonicalTestScalars.Scalar(index + 1),
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
            b[24] = 0x99;
            b[28] = (byte)(number >> 24);
            b[29] = (byte)(number >> 16);
            b[30] = (byte)(number >> 8);
            b[31] = (byte)number;
            return b;
        }
    }
}
