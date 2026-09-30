using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Freezer.FilterMaps;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class FreezerLogsE2ETests : IDisposable
    {
        private static readonly FilterMapsParams TinyParams = new FilterMapsParams(
            logMapHeight: 4,
            logMapWidth: 8,
            logMapsPerEpoch: 1,
            logValuesPerMap: 3,
            baseRowGroupSize: 2,
            baseRowLengthRatio: 4,
            logLayerDiff: 4);

        private readonly string _root = Path.Combine(Path.GetTempPath(), $"fmlogs_{Guid.NewGuid():N}");
        private readonly ITransactionVerificationAndRecovery _signer = new TransactionVerificationAndRecoveryImp();
        private readonly EthECKey _senderKey = EthECKey.GenerateKey();
        private readonly LegacyTransactionSigner _txSigner = new LegacyTransactionSigner();

        private const long TipHeight = 90_010;
        private const long FreezeBoundary = 10;
        private const int DrainCount = 16;
        private const int RecentLogBlock = 12;

        private static readonly string FrozenAddress = "0x" + new string('0', 38) + "aa";
        private static readonly string RecentAddress = "0x" + new string('0', 38) + "bb";
        private static readonly string NoLogsAddress = "0x" + new string('0', 38) + "cc";

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        [Fact]
        public async Task Given_RenderedFrozenLogs_When_GetLogsByAddress_Then_MatchesDirectScan()
        {
            var (bundle, blocks, indexedHead) = await SetupRenderedBundleAsync("data1", "freezer1");
            using (bundle)
            {
                Assert.True(indexedHead >= 0, "expected at least one indexed block");
                Assert.True(indexedHead <= FreezeBoundary, "must never index past the freezer head");

                var filter = new LogFilter
                {
                    Addresses = new List<string> { FrozenAddress },
                    FromBlock = 0,
                    ToBlock = indexedHead
                };
                var results = await bundle.Logs.GetLogsAsync(filter);
                Assert.NotEmpty(results);

                var expected = DirectScan(blocks, 0, indexedHead, FrozenAddress);
                Assert.NotEmpty(expected);
                AssertLogListsEqual(expected, results);
            }
        }

        [Fact]
        public async Task Given_LogsInRecentBand_When_GetLogs_Then_ServedByBloomScan()
        {
            var (bundle, blocks, indexedHead) = await SetupRenderedBundleAsync("data2", "freezer2");
            using (bundle)
            {
                var recentBlock = blocks[RecentLogBlock];
                Assert.True(recentBlock.Header.BlockNumber.ToBigInteger() > indexedHead,
                    "sanity: the recent-band block must sit above the rendered index's own head");
                Assert.True(recentBlock.Header.BlockNumber.ToBigInteger() > FreezeBoundary,
                    "sanity: the recent-band block must never have been frozen");

                var filter = new LogFilter
                {
                    Addresses = new List<string> { RecentAddress },
                    FromBlock = 0,
                    ToBlock = DrainCount - 1
                };
                var results = await bundle.Logs.GetLogsAsync(filter);

                var expected = DirectScan(blocks, RecentLogBlock, RecentLogBlock, RecentAddress);
                Assert.Single(expected);
                AssertLogListsEqual(expected, results);
            }
        }

        [Fact]
        public async Task Given_AddressWithNoLogs_When_GetLogs_Then_Empty()
        {
            var (bundle, _, _) = await SetupRenderedBundleAsync("data3", "freezer3");
            using (bundle)
            {
                var filter = new LogFilter
                {
                    Addresses = new List<string> { NoLogsAddress },
                    FromBlock = 0,
                    ToBlock = DrainCount - 1
                };
                var results = await bundle.Logs.GetLogsAsync(filter);
                Assert.Empty(results);
            }
        }

        [Fact]
        public void Given_UseFreezerHistoryOff_When_GetLogs_Then_UsesCompositeLogStore()
        {
            var dataDir = Path.Combine(_root, "data4");
            Directory.CreateDirectory(dataDir);
            var rocks = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dataDir });
            using var bundle = RocksDbChainStoreBundle.FromManager(
                rocks, dataDir, journalOptions: null, ownsManager: true, bulkSync: false,
                flatStateCache: null, historyRocks: null, historyDataDir: null, signer: null);

            Assert.IsType<CompositeLogStore>(bundle.Logs);
        }

        private RocksDbStorageOptions FreezerOptions(string dataDir, string freezerDir) => new RocksDbStorageOptions
        {
            DatabasePath = dataDir,
            UseFreezerHistory = true,
            FreezerHistoryDirectory = freezerDir,
            FilterMapsIndexParams = TinyParams,
        };

        private RocksDbChainStoreBundle OpenBundle(RocksDbManager rocks, string dataDir)
            => RocksDbChainStoreBundle.FromManager(
                rocks, dataDir, journalOptions: null, ownsManager: true, bulkSync: false,
                flatStateCache: null, historyRocks: null, historyDataDir: null, signer: _signer);

        private async Task<(RocksDbChainStoreBundle Bundle, List<PersistableBlock> Blocks, long IndexedHead)> SetupRenderedBundleAsync(
            string dataDirName, string freezerDirName)
        {
            var dataDir = Path.Combine(_root, dataDirName);
            Directory.CreateDirectory(dataDir);
            var rocks = new RocksDbManager(FreezerOptions(dataDir, Path.Combine(_root, freezerDirName)));
            var bundle = OpenBundle(rocks, dataDir);

            var blocks = MakeBlocks();
            var hash = Fill(0xEE, 32);
            await bundle.Blocks.SaveAsync(MakeHeader(TipHeight, hash), hash);
            await bundle.PersistBlocksAsync(blocks);

            bundle.RenderFilterMapsCatchUp();

            var fmStore = new RocksDbFilterMapsStore(bundle.FreezerHistoryRocksForTests);
            var range = fmStore.ReadRange();
            var indexedHead = range == null ? -1L : range.BlocksAfterLast - 1;

            return (bundle, blocks, indexedHead);
        }

        private List<PersistableBlock> MakeBlocks()
        {
            var list = new List<PersistableBlock>(DrainCount);
            for (var number = 0; number < DrainCount; number++)
            {
                if (number <= FreezeBoundary)
                    list.Add(MakeBlock(number, FrozenAddress, Topic((byte)((number % 7) + 1)), (byte)number));
                else if (number == RecentLogBlock)
                    list.Add(MakeBlock(number, RecentAddress, Topic(0x99), 0xAB));
                else
                    list.Add(MakeBlock(number, null, null, 0));
            }
            return list;
        }

        private PersistableBlock MakeBlock(long number, string logAddress, byte[] topic, byte dataByte)
        {
            var hash = Fill((byte)number, 32);
            var tx = MakeTx(number);
            var txs = new List<ISignedTransaction> { tx };

            var logs = new List<Log>();
            var bloom = new LogBloomFilter();
            if (logAddress != null)
            {
                var log = new Log { Address = logAddress, Topics = new List<byte[]> { topic }, Data = new byte[] { dataByte } };
                logs.Add(log);
                bloom.AddLog(log);
            }

            var header = MakeHeader(number, hash, bloom.Data);
            var rcpt = new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = 21000, Logs = logs };
            var receipts = new List<ReceiptSaveItem> { new ReceiptSaveItem(rcpt, tx.Hash, 0, 21000, null, 1_000_000_000) };

            return new PersistableBlock(
                header, hash,
                uncles: new List<BlockHeader>(),
                withdrawals: null,
                transactions: txs,
                receipts: receipts,
                logs: new List<(List<Log> Logs, byte[] TxHash, int TxIndex)>(),
                bloom: bloom.Data);
        }

        private static BlockHeader MakeHeader(long number, byte[] hash, byte[] logsBloom = null) => new BlockHeader
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
            LogsBloom = logsBloom ?? new byte[256],
            Coinbase = "0x0000000000000000000000000000000000000000",
        };

        private ISignedTransaction MakeTx(long number)
        {
            var tx = new LegacyTransaction(
                nonce: CanonicalTestScalars.Scalar(number + 1),
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: new byte[] { },
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

        private static byte[] Topic(byte seed)
        {
            var bytes = new byte[32];
            bytes[31] = seed;
            return bytes;
        }

        private static List<FilteredLog> DirectScan(List<PersistableBlock> blocks, long fromBlock, long toBlock, string address)
        {
            var result = new List<FilteredLog>();
            for (var n = fromBlock; n <= toBlock; n++)
            {
                var block = blocks[(int)n];
                var logIndex = 0;
                for (var txIndex = 0; txIndex < block.Receipts.Count; txIndex++)
                {
                    var item = block.Receipts[txIndex];
                    foreach (var log in item.Receipt.Logs)
                    {
                        if (log.Address.IsTheSameAddress(address))
                            result.Add(FilteredLog.FromLog(
                                log, block.Hash, block.Header.BlockNumber.ToBigInteger(), item.TxHash, txIndex, logIndex));
                        logIndex++;
                    }
                }
            }
            return result;
        }

        private static void AssertLogListsEqual(List<FilteredLog> expected, List<FilteredLog> actual)
        {
            var sortedExpected = expected.OrderBy(l => l.BlockNumber).ThenBy(l => l.TransactionIndex).ThenBy(l => l.LogIndex).ToList();
            var sortedActual = actual.OrderBy(l => l.BlockNumber).ThenBy(l => l.TransactionIndex).ThenBy(l => l.LogIndex).ToList();

            Assert.Equal(sortedExpected.Count, sortedActual.Count);
            for (var i = 0; i < sortedExpected.Count; i++)
                AssertLogEqual(sortedExpected[i], sortedActual[i]);
        }

        private static void AssertLogEqual(FilteredLog expected, FilteredLog actual)
        {
            Assert.True(expected.Address.IsTheSameAddress(actual.Address), $"address: {expected.Address} vs {actual.Address}");
            Assert.Equal(expected.Data ?? Array.Empty<byte>(), actual.Data ?? Array.Empty<byte>());
            Assert.Equal(expected.Topics.Count, actual.Topics.Count);
            for (var i = 0; i < expected.Topics.Count; i++)
                Assert.Equal(expected.Topics[i], actual.Topics[i]);
            Assert.Equal(expected.BlockHash, actual.BlockHash);
            Assert.Equal(expected.BlockNumber, actual.BlockNumber);
            Assert.Equal(expected.TransactionHash, actual.TransactionHash);
            Assert.Equal(expected.TransactionIndex, actual.TransactionIndex);
            Assert.Equal(expected.LogIndex, actual.LogIndex);
            Assert.Equal(expected.Removed, actual.Removed);
        }
    }
}
