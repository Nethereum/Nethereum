using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Freezer.FilterMaps;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class FreezerLogsScaleTests : IDisposable
    {
        private static readonly FilterMapsParams TinyParams = new FilterMapsParams(
            logMapHeight: 4, logMapWidth: 8, logMapsPerEpoch: 1, logValuesPerMap: 3,
            baseRowGroupSize: 2, baseRowLengthRatio: 4, logLayerDiff: 4);

        private readonly string _root = Path.Combine(Path.GetTempPath(), $"fmscale_{Guid.NewGuid():N}");
        private readonly ITransactionVerificationAndRecovery _signer = new TransactionVerificationAndRecoveryImp();
        private readonly EthECKey _senderKey = EthECKey.GenerateKey();
        private readonly LegacyTransactionSigner _txSigner = new LegacyTransactionSigner();

        private const long TipHeight = 90_200;
        private const long FreezeBoundary = 200;
        private const int DrainCount = 250;

        private static readonly string FrozenAddress = "0x" + new string('0', 38) + "aa";
        private static readonly string OtherAddress = "0x" + new string('0', 38) + "bb";

        private static readonly TimeSpan MaxFrozenQuery = TimeSpan.FromSeconds(20);

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        [Fact]
        public async Task Given_ManyRenderedFrozenEpochs_When_GetLogsForOneDeepFrozenBlock_Then_ResolvesOnlyThatBlockQuickly()
        {
            var (bundle, blocks, indexedHead) = await SetupAsync();
            using (bundle)
            {
                Assert.True(indexedHead >= 50, $"expected a deeply-rendered frozen band (head={indexedHead})");

                var target = indexedHead - 20;
                var filter = new LogFilter { Addresses = new List<string> { FrozenAddress }, FromBlock = target, ToBlock = target };

                var sw = Stopwatch.StartNew();
                var results = await bundle.Logs.GetLogsAsync(filter);
                sw.Stop();

                Assert.True(sw.Elapsed < MaxFrozenQuery,
                    $"single frozen-block getLogs took {sw.Elapsed.TotalSeconds:F1}s -- O(blocks) regression in the frozen query path");
                AssertLogListsEqual(DirectScan(blocks, target, target, FrozenAddress), results);
                Assert.Single(results);
            }
        }

        [Fact]
        public async Task Given_ManyRenderedFrozenEpochs_When_GetLogsSpansEveryFrozenEpoch_Then_MatchesDirectScan()
        {
            var (bundle, blocks, indexedHead) = await SetupAsync();
            using (bundle)
            {
                var filter = new LogFilter { Addresses = new List<string> { FrozenAddress }, FromBlock = 0, ToBlock = indexedHead };

                var sw = Stopwatch.StartNew();
                var results = await bundle.Logs.GetLogsAsync(filter);
                sw.Stop();

                Assert.True(sw.Elapsed < MaxFrozenQuery,
                    $"whole-frozen-band getLogs took {sw.Elapsed.TotalSeconds:F1}s -- unbounded scan across frozen batches");
                var expected = DirectScan(blocks, 0, indexedHead, FrozenAddress);
                Assert.Equal(indexedHead + 1, expected.Count);
                AssertLogListsEqual(expected, results);
            }
        }

        [Fact]
        public async Task Given_ManyRenderedFrozenEpochs_When_GetLogsStraddlesRenderedHeadIntoRecentBand_Then_MatchesDirectScan()
        {
            var (bundle, blocks, indexedHead) = await SetupAsync();
            using (bundle)
            {
                var filter = new LogFilter { Addresses = new List<string> { FrozenAddress }, FromBlock = 0, ToBlock = DrainCount - 1 };

                var sw = Stopwatch.StartNew();
                var results = await bundle.Logs.GetLogsAsync(filter);
                sw.Stop();

                Assert.True(sw.Elapsed < MaxFrozenQuery, $"straddle getLogs took {sw.Elapsed.TotalSeconds:F1}s");
                AssertLogListsEqual(DirectScan(blocks, 0, DrainCount - 1, FrozenAddress), results);
            }
        }

        private async Task<(RocksDbChainStoreBundle Bundle, List<PersistableBlock> Blocks, long IndexedHead)> SetupAsync()
        {
            var dataDir = Path.Combine(_root, "data");
            Directory.CreateDirectory(dataDir);
            var rocks = new RocksDbManager(new RocksDbStorageOptions
            {
                DatabasePath = dataDir,
                UseFreezerHistory = true,
                FreezerHistoryDirectory = Path.Combine(_root, "freezer"),
                FilterMapsIndexParams = TinyParams,
            });
            var bundle = RocksDbChainStoreBundle.FromManager(
                rocks, dataDir, journalOptions: null, ownsManager: true, bulkSync: false,
                flatStateCache: null, historyRocks: null, historyDataDir: null, signer: _signer);

            var blocks = MakeBlocks();
            var tipHash = Fill(0xEE, 32);
            await bundle.Blocks.SaveAsync(MakeHeader(TipHeight, tipHash), tipHash);
            await bundle.PersistBlocksAsync(blocks);
            bundle.RenderFilterMapsCatchUp();

            var range = new RocksDbFilterMapsStore(bundle.FreezerHistoryRocksForTests).ReadRange();
            var indexedHead = range == null ? -1L : range.BlocksAfterLast - 1;
            return (bundle, blocks, indexedHead);
        }

        private List<PersistableBlock> MakeBlocks()
        {
            var list = new List<PersistableBlock>(DrainCount);
            for (var number = 0; number < DrainCount; number++)
                list.Add(number <= FreezeBoundary
                    ? MakeBlock(number, FrozenAddress, Topic((byte)((number % 7) + 1)), (byte)number)
                    : MakeBlock(number, OtherAddress, Topic(0x99), (byte)number));
            return list;
        }

        private PersistableBlock MakeBlock(long number, string logAddress, byte[] topic, byte dataByte)
        {
            var hash = Fill((byte)(number & 0xFF), 32);
            var tx = MakeTx(number);
            var log = new Log { Address = logAddress, Topics = new List<byte[]> { topic }, Data = new byte[] { dataByte } };
            var bloom = new LogBloomFilter();
            bloom.AddLog(log);

            var rcpt = new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = 21000, Logs = new List<Log> { log } };
            return new PersistableBlock(
                MakeHeader(number, hash, bloom.Data), hash,
                uncles: new List<BlockHeader>(), withdrawals: null,
                transactions: new List<ISignedTransaction> { tx },
                receipts: new List<ReceiptSaveItem> { new ReceiptSaveItem(rcpt, tx.Hash, 0, 21000, null, 1_000_000_000) },
                logs: new List<(List<Log> Logs, byte[] TxHash, int TxIndex)>(),
                bloom: bloom.Data);
        }

        private static BlockHeader MakeHeader(long number, byte[] hash, byte[] logsBloom = null) => new BlockHeader
        {
            BlockNumber = new EvmUInt256((ulong)number),
            ParentHash = new byte[32], TransactionsHash = new byte[32], UnclesHash = new byte[32],
            ReceiptHash = new byte[32], StateRoot = new byte[32], Difficulty = new EvmUInt256(1UL),
            GasLimit = 1, Timestamp = 1, ExtraData = Array.Empty<byte>(), MixHash = new byte[32],
            Nonce = new byte[8], LogsBloom = logsBloom ?? new byte[256],
            Coinbase = "0x0000000000000000000000000000000000000000",
        };

        private ISignedTransaction MakeTx(long number)
        {
            var tx = new LegacyTransaction(
                nonce: CanonicalTestScalars.Scalar(number + 1), gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 }, receiveAddress: new byte[20],
                value: Array.Empty<byte>(), data: Array.Empty<byte>());
            _txSigner.SignTransaction(_senderKey.GetPrivateKeyAsBytes(), tx);
            return tx;
        }

        private static byte[] Fill(byte v, int len) { var b = new byte[len]; for (var i = 0; i < len; i++) b[i] = v; return b; }
        private static byte[] Topic(byte seed) { var b = new byte[32]; b[31] = seed; return b; }

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
            var e = expected.OrderBy(l => l.BlockNumber).ThenBy(l => l.TransactionIndex).ThenBy(l => l.LogIndex).ToList();
            var a = actual.OrderBy(l => l.BlockNumber).ThenBy(l => l.TransactionIndex).ThenBy(l => l.LogIndex).ToList();
            Assert.Equal(e.Count, a.Count);
            for (var i = 0; i < e.Count; i++)
            {
                Assert.True(e[i].Address.IsTheSameAddress(a[i].Address));
                Assert.Equal(e[i].BlockNumber, a[i].BlockNumber);
                Assert.Equal(e[i].TransactionHash, a[i].TransactionHash);
                Assert.Equal(e[i].LogIndex, a[i].LogIndex);
                Assert.Equal(e[i].Data ?? Array.Empty<byte>(), a[i].Data ?? Array.Empty<byte>());
            }
        }
    }
}
