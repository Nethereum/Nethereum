using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class HistoricalBlockBackfillerTests
    {
        private static readonly byte[] EmptyUnclesHash =
            "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();
        private static readonly byte[] EmptyTrieRoot =
            "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();

        [Fact]
        public async Task Backfill_PersistsHeadersBodiesReceipts_AndAdvancesCursor()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var scheduler = new CannedScheduler(blockCount: 5);

            var backfiller = new HistoricalBlockBackfiller(
                scheduler, bundle, rootsProvider: null, logger: null, batchSize: 3);

            var result = await backfiller.BackfillAsync(0, 4, CancellationToken.None);

            Assert.True(result.Ran);
            Assert.Equal(5UL, result.BlocksWritten);
            Assert.Equal(0UL, result.TransactionsWritten);
            Assert.Equal(0UL, result.ReceiptsWritten);
            Assert.Equal(4UL, bundle.Metadata.GetLastFetchedHeader());
            Assert.Equal(4UL, bundle.Metadata.GetLastFetchedBody());

            for (long n = 0; n <= 4; n++)
            {
                var header = await bundle.Blocks.GetByNumberAsync(new BigInteger(n));
                Assert.NotNull(header);
                Assert.Equal((ulong)n, header.BlockNumber.ToBigInteger());
            }
        }

        [Fact]
        public async Task Backfill_ResumesFromCursor_AndSkipsAlreadyDoneRange()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var scheduler = new CannedScheduler(blockCount: 6);

            var first = new HistoricalBlockBackfiller(scheduler, bundle, batchSize: 3);
            var run1 = await first.BackfillAsync(0, 2, CancellationToken.None);
            Assert.True(run1.Ran);
            Assert.Equal(3UL, run1.BlocksWritten);
            Assert.Equal(2UL, bundle.Metadata.GetLastFetchedHeader());

            var second = new HistoricalBlockBackfiller(scheduler, bundle, batchSize: 3);
            var run2 = await second.BackfillAsync(0, 5, CancellationToken.None);
            Assert.True(run2.Ran);
            Assert.Equal(3UL, run2.BlocksWritten);
            Assert.Equal(5UL, bundle.Metadata.GetLastFetchedHeader());

            var third = new HistoricalBlockBackfiller(scheduler, bundle, batchSize: 3);
            var run3 = await third.BackfillAsync(0, 5, CancellationToken.None);
            Assert.False(run3.Ran);
            Assert.StartsWith("already at", run3.SkipReason);
        }

        [Fact]
        public async Task Backfill_RetriesBatch_OnReceiptRootMismatch()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var scheduler = new CannedScheduler(blockCount: 2) { PoisonFirstReceiptBatch = true };

            var backfiller = new HistoricalBlockBackfiller(scheduler, bundle, batchSize: 2);
            var result = await backfiller.BackfillAsync(0, 1, CancellationToken.None);

            Assert.True(result.Ran);
            Assert.Equal(2UL, result.BlocksWritten);
            Assert.Equal(2, scheduler.HeaderCalls);
            Assert.Equal(2, scheduler.BodyCalls);
            Assert.Equal(2, scheduler.ReceiptCalls);
        }

        [Fact]
        public async Task Backfill_RejectsBatchWithBrokenParentChain_AndRetries()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var scheduler = new CannedScheduler(blockCount: 4) { BreakParentChainAtIndexOnce = 2 };

            var backfiller = new HistoricalBlockBackfiller(scheduler, bundle, batchSize: 4);
            var result = await backfiller.BackfillAsync(0, 3, CancellationToken.None);

            Assert.True(result.Ran);
            Assert.Equal(4UL, result.BlocksWritten);
            Assert.True(scheduler.HeaderCalls >= 2);
        }

        // Regression for #346 — ReceiptMetadataPopulator for non-execute paths.
        // Backfilled receipts MUST carry effectiveGasPrice (derived from tx fields
        // + header baseFee) so eth_getTransactionReceipt returns the same value
        // as Erigon / geth for the same block. Pre-fix, both effectiveGasPrice
        // and contractAddress were hard-stamped 0 / null at the SaveAsync call.
        [Fact]
        public async Task Backfill_PopulatesEffectiveGasPrice_ForLegacyAndEip1559Txs()
        {
            using var bundle = InMemoryChainStoreBundle.Open();

            // Block 1 is London-style: baseFee = 7. Both txs target a non-zero
            // ReceiverAddress so the contract-address branch (which would need
            // signature recovery) stays out of scope for this test.
            var legacyTx = new LegacyTransaction(
                nonce: new byte[] { 0x01 },
                gasPrice: new byte[] { 0x14 },                  // 20 wei
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: "1111111111111111111111111111111111111111".HexToByteArray(),
                value: new byte[] { 0x00 },
                data: new byte[0]);

            var tx1559 = new Transaction1559(
                chainId: new EvmUInt256(1UL),
                nonce: new EvmUInt256(0UL),
                maxPriorityFeePerGas: new EvmUInt256(3UL),
                maxFeePerGas: new EvmUInt256(100UL),
                gasLimit: new EvmUInt256(21000UL),
                receiverAddress: "0x2222222222222222222222222222222222222222",
                amount: new EvmUInt256(0UL),
                data: "0x",
                accessList: new List<Model.AccessListItem>());

            var receipt1 = Receipt.CreateStatusReceipt(true, 21000, new byte[256], new List<Log>());
            var receipt2 = Receipt.CreateStatusReceipt(true, 42000, new byte[256], new List<Log>());

            var scheduler = new CannedScheduler(blockCount: 1, baseFee: new EvmUInt256(7UL));
            scheduler.BlockTransactions[0] = new List<ISignedTransaction> { legacyTx, tx1559 };
            scheduler.BlockReceipts[0] = new List<Receipt> { receipt1, receipt2 };
            // Re-build with the populated bodies so header.TransactionsHash / ReceiptHash
            // line up with the computed trie roots — the backfiller will reject the batch
            // otherwise.
            scheduler.RebuildChain();

            var backfiller = new HistoricalBlockBackfiller(
                scheduler, bundle, rootsProvider: Nethereum.CoreChain.PatriciaBlockRootsProvider.Instance, logger: null, batchSize: 1);
            var result = await backfiller.BackfillAsync(0, 0, CancellationToken.None);
            Assert.True(result.Ran);
            Assert.Equal(2UL, result.ReceiptsWritten);

            var keccak = new Sha3Keccack();
            var legacyHash = keccak.CalculateHash(legacyTx.GetRLPEncoded());
            var tx1559Hash = keccak.CalculateHash(tx1559.GetRLPEncoded());

            var legacyInfo = await bundle.Receipts.GetInfoByTxHashAsync(legacyHash);
            Assert.NotNull(legacyInfo);
            // Legacy: effective = gasPrice = 20 (baseFee ignored)
            Assert.Equal(new BigInteger(20), legacyInfo.EffectiveGasPrice);
            Assert.Null(legacyInfo.ContractAddress);  // non-creation tx

            var info1559 = await bundle.Receipts.GetInfoByTxHashAsync(tx1559Hash);
            Assert.NotNull(info1559);
            // EIP-1559: effective = baseFee + min(priority, maxFee - baseFee) = 7 + min(3, 93) = 10
            Assert.Equal(new BigInteger(10), info1559.EffectiveGasPrice);
            Assert.Null(info1559.ContractAddress);
        }

        // Regression for #357 — eth_getLogs across backfilled blocks must
        // return the same data as the execute path. Pre-fix, only the
        // receipt's embedded Logs[] survived; ILogStore was untouched, so
        // any topic/address cross-block filter came back empty.
        [Fact]
        public async Task Backfill_PopulatesLogStore_ForReceiptLogsAndBlockBloom()
        {
            using var bundle = InMemoryChainStoreBundle.Open();

            var legacyTx = new LegacyTransaction(
                nonce: new byte[] { 0x01 },
                gasPrice: new byte[] { 0x14 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: "1111111111111111111111111111111111111111".HexToByteArray(),
                value: new byte[] { 0x00 },
                data: new byte[0]);

            var contractAddress = "0x000000000000000000000000000000000000beef";
            var topic0 = new byte[32];
            topic0[31] = 0x42;

            var log = new Log
            {
                Address = contractAddress,
                Topics = new List<byte[]> { topic0 },
                Data = new byte[] { 0x01, 0x02, 0x03 }
            };
            var receipt = Receipt.CreateStatusReceipt(true, 21000, new byte[256], new List<Log> { log });

            var scheduler = new CannedScheduler(blockCount: 1, baseFee: new EvmUInt256(7UL));
            scheduler.BlockTransactions[0] = new List<ISignedTransaction> { legacyTx };
            scheduler.BlockReceipts[0] = new List<Receipt> { receipt };
            scheduler.RebuildChain();

            var backfiller = new HistoricalBlockBackfiller(
                scheduler, bundle, rootsProvider: Nethereum.CoreChain.PatriciaBlockRootsProvider.Instance, logger: null, batchSize: 1);
            var result = await backfiller.BackfillAsync(0, 0, CancellationToken.None);
            Assert.True(result.Ran);

            var keccak = new Sha3Keccack();
            var txHash = keccak.CalculateHash(legacyTx.GetRLPEncoded());

            // ILogStore.GetLogsByTxHashAsync must return the log so
            // eth_getLogs / eth_getFilterLogs can resolve it cross-block.
            var byTxHash = await bundle.Logs.GetLogsByTxHashAsync(txHash);
            Assert.Single(byTxHash);
            Assert.Equal(contractAddress, byTxHash[0].Address, ignoreCase: true);

            // GetLogsByBlockNumberAsync proves the per-block secondary index
            // landed too — needed for eth_getLogs with explicit fromBlock/toBlock.
            var byBlock = await bundle.Logs.GetLogsByBlockNumberAsync(BigInteger.Zero);
            Assert.Single(byBlock);
        }

        [Fact]
        public async Task Backfill_StopsOnCancellation_DuringEscalationDelay()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var scheduler = new CannedScheduler(blockCount: 1) { AlwaysFailHeaders = true };

            var backfiller = new HistoricalBlockBackfiller(scheduler, bundle, batchSize: 1);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => backfiller.BackfillAsync(0, 0, cts.Token));
        }

        private sealed class CannedScheduler : IFetchRequestScheduler
        {
            public int HeaderCalls;
            public int BodyCalls;
            public int ReceiptCalls;
            public bool PoisonFirstReceiptBatch;
            public bool AlwaysFailHeaders;
            public int BreakParentChainAtIndexOnce = -1;

            // Optional per-block bodies + receipts. When provided, headers
            // are built with TransactionsHash / ReceiptHash matching the
            // computed roots so ValidateBodies / ValidateReceipts pass.
            public Dictionary<long, List<ISignedTransaction>> BlockTransactions { get; } = new();
            public Dictionary<long, List<Receipt>> BlockReceipts { get; } = new();

            // Re-build the header chain after populating BlockTransactions /
            // BlockReceipts so the per-header TransactionsHash and ReceiptHash
            // pick up the computed trie roots.
            public void RebuildChain()
            {
                _headers.Clear();
                _hashes.Clear();
                _chainBreakFired = false;
                BuildChain();
            }

            private readonly int _blockCount;
            private readonly EvmUInt256? _baseFee;
            private readonly Sha3Keccack _keccak = new();
            private readonly Dictionary<long, BlockHeader> _headers = new();
            private readonly Dictionary<long, byte[]> _hashes = new();
            private readonly Nethereum.Model.IBlockRootsProvider _rootsProvider =
                Nethereum.CoreChain.PatriciaBlockRootsProvider.Instance;
            private bool _chainBreakFired;

            public CannedScheduler(int blockCount, EvmUInt256? baseFee = null)
            {
                _blockCount = blockCount;
                _baseFee = baseFee;
                BuildChain();
            }

            private void BuildChain()
            {
                byte[] prevHash = new byte[32];
                for (long n = 0; n < _blockCount; n++)
                {
                    var header = MakeEmptyHeader(n, prevHash);
                    if (_baseFee.HasValue) header.BaseFee = _baseFee.Value;

                    // If this scheduler has per-block txs/receipts, stamp the
                    // matching trie roots so backfill validation passes.
                    if (BlockTransactions.TryGetValue(n, out var txs) && txs.Count > 0)
                        header.TransactionsHash = _rootsProvider.CalculateTransactionsRoot(txs);
                    if (BlockReceipts.TryGetValue(n, out var rcpts) && rcpts.Count > 0)
                        header.ReceiptHash = _rootsProvider.CalculateReceiptsRoot(rcpts);

                    _headers[n] = header;
                    var hash = _keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(header));
                    _hashes[n] = hash;
                    prevHash = hash;
                }
            }

            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct)
            {
                var call = Interlocked.Increment(ref HeaderCalls);
                if (AlwaysFailHeaders)
                    throw new System.IO.IOException("simulated transport failure");

                var headers = new List<BlockHeader>();
                for (ulong i = 0; i < limit && startBlock + i < (ulong)_blockCount; i++)
                {
                    var n = (long)(startBlock + i);
                    var header = _headers[n];
                    if (BreakParentChainAtIndexOnce >= 0 && !_chainBreakFired && (int)i == BreakParentChainAtIndexOnce)
                    {
                        var broken = CloneHeader(header);
                        broken.ParentHash = new byte[32];
                        headers.Add(broken);
                        if (i == limit - 1 || startBlock + i == (ulong)(_blockCount - 1))
                            _chainBreakFired = true;
                    }
                    else
                    {
                        headers.Add(header);
                    }
                }
                if (BreakParentChainAtIndexOnce >= 0 && headers.Count > BreakParentChainAtIndexOnce)
                    _chainBreakFired = true;
                return Task.FromResult(headers);
            }

            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
            {
                Interlocked.Increment(ref BodyCalls);
                var bodies = new List<BlockBody>();
                for (int i = 0; i < blockHashes.Count; i++)
                {
                    var blockNumber = ResolveBlockNumber(blockHashes[i]);
                    var body = new BlockBody();
                    if (blockNumber.HasValue && BlockTransactions.TryGetValue(blockNumber.Value, out var txs))
                        body.Transactions = txs;
                    bodies.Add(body);
                }
                return Task.FromResult(bodies);
            }

            private long? ResolveBlockNumber(byte[] hash)
            {
                foreach (var kvp in _hashes)
                    if (ByteUtil.AreEqual(kvp.Value, hash)) return kvp.Key;
                return null;
            }

            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct)
                => throw new NotImplementedException();

            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
            {
                var call = Interlocked.Increment(ref ReceiptCalls);
                var receipts = new List<List<Receipt>>();
                for (int i = 0; i < blockHashes.Count; i++)
                {
                    if (PoisonFirstReceiptBatch && call == 1)
                    {
                        receipts.Add(new List<Receipt> { new Receipt() });
                        continue;
                    }
                    var blockNumber = ResolveBlockNumber(blockHashes[i]);
                    if (blockNumber.HasValue && BlockReceipts.TryGetValue(blockNumber.Value, out var rcpts))
                        receipts.Add(rcpts);
                    else
                        receipts.Add(new List<Receipt>());
                }
                return Task.FromResult(receipts);
            }

            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();

            private static BlockHeader MakeEmptyHeader(long blockNumber, byte[] parentHash)
            {
                return new BlockHeader
                {
                    BlockNumber = new EvmUInt256((ulong)blockNumber),
                    ParentHash = (byte[])parentHash.Clone(),
                    TransactionsHash = (byte[])EmptyTrieRoot.Clone(),
                    UnclesHash = (byte[])EmptyUnclesHash.Clone(),
                    ReceiptHash = (byte[])EmptyTrieRoot.Clone(),
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

            private static BlockHeader CloneHeader(BlockHeader h) =>
                new BlockHeader
                {
                    BlockNumber = h.BlockNumber,
                    ParentHash = (byte[])h.ParentHash.Clone(),
                    TransactionsHash = (byte[])h.TransactionsHash.Clone(),
                    UnclesHash = (byte[])h.UnclesHash.Clone(),
                    ReceiptHash = (byte[])h.ReceiptHash.Clone(),
                    StateRoot = (byte[])h.StateRoot.Clone(),
                    Difficulty = h.Difficulty,
                    GasLimit = h.GasLimit,
                    Timestamp = h.Timestamp,
                    ExtraData = (byte[])h.ExtraData.Clone(),
                    MixHash = (byte[])h.MixHash.Clone(),
                    Nonce = (byte[])h.Nonce.Clone(),
                    LogsBloom = (byte[])h.LogsBloom.Clone(),
                    Coinbase = h.Coinbase,
                };
        }
    }
}
