using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class PromotionFlagOffByteIdentityTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"promotionoff_{Guid.NewGuid():N}");

        public PromotionFlagOffByteIdentityTests()
        {
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        private string SubDir(string name)
        {
            var path = Path.Combine(_root, name);
            Directory.CreateDirectory(path);
            return path;
        }

        [Fact]
        public void PromotionDisabled_NeverOpensHotWindowColumnFamilies()
        {
            var dir = Path.Combine(_root, "flagoff-cfs");
            using (var bundle = RocksDbChainStoreBundle.Open(dir, storageOptions: new RocksDbStorageOptions { PromotionEnabled = false }))
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
        public async Task PromotionOff_FollowReorgGetLogs_ByteIdenticalToPreRefactor()
        {
            var dirReference = SubDir("reference");
            var dirBundle = SubDir("bundle");

            using var referenceManager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirReference });
            var referenceBlocks = new RocksDbBlockStore(referenceManager);
            var referenceTransactions = new RocksDbTransactionStore(referenceManager, referenceBlocks);

            using var bundleManager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirBundle, PromotionEnabled = false });
            var bundle = RocksDbChainStoreBundle.FromManager(bundleManager, dirBundle, ownsManager: false);

            Assert.IsType<RocksDbBlockStore>(bundle.Blocks);
            Assert.IsType<RocksDbTransactionStore>(bundle.Transactions);

            for (long n = 1; n <= 4; n++)
                await FollowBlockAsync(referenceBlocks, referenceTransactions, bundle, MakeBlock(n, branch: 0));

            for (long n = 4; n >= 3; n--)
            {
                await referenceTransactions.DeleteByBlockNumberAsync(n);
                await referenceBlocks.DeleteByNumberAsync(n);
                await bundle.Transactions.DeleteByBlockNumberAsync(n);
                await bundle.Blocks.DeleteByNumberAsync(n);
            }
            for (long n = 3; n <= 4; n++)
                await FollowBlockAsync(referenceBlocks, referenceTransactions, bundle, MakeBlock(n, branch: 1));

            var logs = await bundle.Logs.GetLogsAsync(new LogFilter { FromBlock = 1, ToBlock = 4 });
            Assert.Empty(logs);

            Assert.Equal(
                new SortedSet<string>(referenceManager.OpenColumnFamilyNames),
                new SortedSet<string>(bundleManager.OpenColumnFamilyNames));

            var nonEmptyCfCount = 0;
            foreach (var cf in referenceManager.OpenColumnFamilyNames)
            {
                var referenceDump = DumpCf(referenceManager, cf);
                var bundleDump = DumpCf(bundleManager, cf);
                if (referenceDump.Count > 0) nonEmptyCfCount++;
                AssertCfIdentical(cf, referenceDump, bundleDump);
            }

            Assert.True(nonEmptyCfCount > 0);
            Assert.True(DumpCf(referenceManager, HistoryColumnFamilies.BlockHeader).Count > 0);
            Assert.True(DumpCf(referenceManager, HistoryColumnFamilies.TxBody).Count > 0);
        }

        private static async Task FollowBlockAsync(
            RocksDbBlockStore referenceBlocks, RocksDbTransactionStore referenceTransactions,
            RocksDbChainStoreBundle bundle, (BlockHeader Header, byte[] Hash, List<ISignedTransaction> Transactions) block)
        {
            var blockNumber = block.Header.BlockNumber.ToBigInteger();
            await referenceBlocks.SaveAsync(block.Header, block.Hash);
            await referenceTransactions.SaveManyAsync(block.Hash, blockNumber, block.Transactions);
            await bundle.Blocks.SaveAsync(block.Header, block.Hash);
            await bundle.Transactions.SaveManyAsync(block.Hash, blockNumber, block.Transactions);
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

        private static void AssertCfIdentical(string cf, Dictionary<string, byte[]> reference, Dictionary<string, byte[]> actual)
        {
            Assert.True(reference.Count == actual.Count,
                $"[{cf}] row count differs: reference={reference.Count} promotion-off-bundle={actual.Count}");
            foreach (var kv in reference)
            {
                Assert.True(actual.TryGetValue(kv.Key, out var actualVal), $"[{cf}] key {kv.Key} present in reference, missing in bundle");
                Assert.Equal(kv.Value, actualVal);
            }
        }

        private static (BlockHeader Header, byte[] Hash, List<ISignedTransaction> Transactions) MakeBlock(long number, int branch)
        {
            var hash = Fill32(number, branch);
            var header = MakeHeader(number, hash, branch);
            var txs = new List<ISignedTransaction> { MakeTx(number, branch, 0), MakeTx(number, branch, 1) };
            return (header, hash, txs);
        }

        private static BlockHeader MakeHeader(long number, byte[] hash, int branch)
        {
            return new BlockHeader
            {
                BlockNumber = new EvmUInt256((ulong)number),
                ParentHash = Fill32(number - 1, branch),
                TransactionsHash = new byte[32],
                UnclesHash = new byte[32],
                ReceiptHash = new byte[32],
                StateRoot = new byte[32],
                Difficulty = new EvmUInt256(1UL),
                GasLimit = 1,
                Timestamp = 1000 + branch,
                ExtraData = Array.Empty<byte>(),
                MixHash = hash,
                Nonce = new byte[8],
                LogsBloom = new byte[256],
                Coinbase = "0x0000000000000000000000000000000000000000",
            };
        }

        private static ISignedTransaction MakeTx(long number, int branch, int index)
        {
            var r = new byte[32]; r[0] = (byte)(0x01 + branch);
            var s = new byte[32]; s[0] = 0x01;
            return new LegacyTransaction(
                nonce: CanonicalTestScalars.Scalar(((number + 1) * 1000 + branch) * 100 + index),
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: new byte[] { },
                data: Array.Empty<byte>(),
                r: r, s: s, v: 27);
        }

        private static byte[] Fill32(long number, int branch)
        {
            var b = new byte[32];
            b[27] = (byte)branch;
            b[28] = (byte)(number >> 24);
            b[29] = (byte)(number >> 16);
            b[30] = (byte)(number >> 8);
            b[31] = (byte)number;
            return b;
        }
    }
}
