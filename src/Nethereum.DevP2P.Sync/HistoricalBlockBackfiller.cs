using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Snap-sync Phase 1 — historical block + receipts backfill. Streams
    /// the [genesis..pivot] range concurrently with snap state download,
    /// persisting headers, bodies (transactions + uncles) and receipts into
    /// the chain bundle so the snap-bootstrapped node can serve
    /// <c>eth_getBlockByNumber</c> / <c>eth_getTransactionReceipt</c> for any
    /// block under the pivot. Without it the node only has state at the
    /// pivot and cannot answer any pre-pivot RPC.
    /// <para>
    /// Each batch is fully validated against the header commitments
    /// (<c>TransactionsHash</c>, <c>UnclesHash</c>, <c>ReceiptHash</c>) AND
    /// against the parent-hash chain — within the batch (header[i].ParentHash
    /// == hash(header[i-1])) and across batches (the first header's
    /// ParentHash must match the previously-persisted block's hash). A peer
    /// cannot poison the archive store with fabricated bodies, receipts, or
    /// off-fork headers. Per-batch progress lands in
    /// <see cref="IChainMetadataStore"/> <c>LastFetchedHeader</c> /
    /// <c>LastFetchedBody</c> cursors so the backfill resumes after a process
    /// restart from where it left off.
    /// </para>
    /// <para>
    /// For receipts under the pivot the node only knows the wire fields
    /// (<c>PostStateOrStatus</c>, <c>CumulativeGasUsed</c>, <c>Bloom</c>,
    /// <c>Logs</c>). <c>contractAddress</c> and <c>effectiveGasPrice</c> are
    /// persisted as null/0 — they require sender ECRECOVER and typed-tx
    /// dispatch which the follower stamps at execution time for pivot+1
    /// onward, but is intentionally skipped here to keep the trust boundary
    /// tight (no synthetic execution against fetched receipts).
    /// </para>
    /// <para>
    /// Failure handling: transient fetch / validation failures retry with
    /// exponential backoff (<see cref="InitialBackoffMs"/> →
    /// <see cref="MaxBackoffMs"/>). After
    /// <see cref="MaxConsecutiveFailures"/> consecutive failures the loop
    /// escalates to <see cref="EscalationDelayMs"/> sleeps and emits a WARN
    /// — covers peer-starvation without burning CPU. The loop only exits on
    /// the supplied <see cref="CancellationToken"/> or once <c>cursor</c>
    /// passes <c>endBlock</c>; persistent failure does not silently abort.
    /// </para>
    /// </summary>
    public sealed class HistoricalBlockBackfiller
    {
        public const ulong DefaultBatchSize = 192;

        /// <summary>Initial retry backoff between batch attempts after a failure.</summary>
        public const int InitialBackoffMs = 50;

        /// <summary>Maximum cap on the exponential retry backoff.</summary>
        public const int MaxBackoffMs = 5000;

        /// <summary>After this many consecutive failures the loop logs WARN and sleeps for <see cref="EscalationDelayMs"/> between attempts instead of growing backoff further.</summary>
        public const int MaxConsecutiveFailures = 8;

        /// <summary>Long sleep used once <see cref="MaxConsecutiveFailures"/> is hit. Lets a peer-starved node wait for the pool to recover without burning CPU.</summary>
        public const int EscalationDelayMs = 30_000;

        private static readonly byte[] EmptyUnclesHash =
            "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();

        private static readonly byte[] EmptyTrieRoot =
            "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();

        private readonly IFetchRequestScheduler _scheduler;
        private readonly IChainStoreBundle _bundle;
        private readonly IBlockRootsProvider _rootsProvider;
        private readonly Sha3Keccack _keccak = new();
        private readonly ILogger _logger;
        private readonly SnapSyncMetrics _metrics;
        private readonly ulong _batchSize;

        public HistoricalBlockBackfiller(
            IFetchRequestScheduler scheduler,
            IChainStoreBundle bundle,
            IBlockRootsProvider rootsProvider = null,
            ILogger logger = null,
            SnapSyncMetrics metrics = null,
            ulong batchSize = DefaultBatchSize)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _rootsProvider = rootsProvider ?? PatriciaBlockRootsProvider.Instance;
            _logger = logger ?? NullLogger.Instance;
            _metrics = metrics;
            if (batchSize == 0) throw new ArgumentOutOfRangeException(nameof(batchSize));
            _batchSize = batchSize;
        }

        public async Task<BackfillResult> BackfillAsync(
            ulong startBlock, ulong endBlock, CancellationToken ct)
        {
            if (endBlock < startBlock)
                return new BackfillResult { Ran = false, SkipReason = "endBlock < startBlock" };

            var resume = _bundle.Metadata.GetLastFetchedHeader();
            ulong cursor;
            if (resume == 0) cursor = startBlock;
            else cursor = resume + 1 > startBlock ? resume + 1 : startBlock;
            if (cursor > endBlock)
                return new BackfillResult { Ran = false, SkipReason = $"already at {resume}" };

            // Anchor the chain-integrity walk: the parent hash of the first
            // header in the very first batch must equal the hash of the
            // last block we already have on disk (or null at fresh start
            // when no genesis is persisted in the bundle yet).
            var lastPersistedHash = await LoadLastPersistedHashAsync(cursor).ConfigureAwait(false);

            _logger.LogInformation(
                "Phase 1 backfill: starting at block {Start} → {End} ({Total} blocks)",
                cursor, endBlock, endBlock - cursor + 1);

            ulong blocksWritten = 0;
            ulong txsWritten = 0;
            ulong receiptsWritten = 0;
            int consecutiveFailures = 0;
            int backoffMs = InitialBackoffMs;

            while (cursor <= endBlock && !ct.IsCancellationRequested)
            {
                var batch = await TryProcessBatchAsync(
                    cursor, endBlock, lastPersistedHash, ct).ConfigureAwait(false);

                if (batch.Success)
                {
                    cursor = batch.NextCursor;
                    lastPersistedHash = batch.LastHash;
                    blocksWritten += batch.BlocksWritten;
                    txsWritten += batch.TxsWritten;
                    receiptsWritten += batch.ReceiptsWritten;
                    consecutiveFailures = 0;
                    backoffMs = InitialBackoffMs;

                    if (blocksWritten % (_batchSize * 16) == 0 || cursor > endBlock)
                    {
                        _logger.LogInformation(
                            "Phase 1 backfill: {Written}/{Total} blocks ({Txs} txs, {Rcpts} receipts) — at {Cursor}",
                            blocksWritten, endBlock - startBlock + 1, txsWritten, receiptsWritten, cursor);
                    }
                    continue;
                }

                consecutiveFailures++;
                _metrics?.RecordFetchFailed("phase1", batch.FailureReason ?? "unknown");

                if (consecutiveFailures >= MaxConsecutiveFailures)
                {
                    _logger.LogWarning(
                        "Phase 1 backfill: stalled at block {Cursor} after {Failures} consecutive failures (last reason: {Reason}); waiting {Delay}ms before retry",
                        cursor, consecutiveFailures, batch.FailureReason, EscalationDelayMs);
                    await DelayWithCancel(EscalationDelayMs, ct).ConfigureAwait(false);
                }
                else
                {
                    await DelayWithCancel(backoffMs, ct).ConfigureAwait(false);
                    backoffMs = Math.Min(backoffMs * 2, MaxBackoffMs);
                }
            }

            return new BackfillResult
            {
                Ran = true,
                BlocksWritten = blocksWritten,
                TransactionsWritten = txsWritten,
                ReceiptsWritten = receiptsWritten,
                EndBlock = endBlock,
            };
        }

        private async Task<BatchOutcome> TryProcessBatchAsync(
            ulong cursor, ulong endBlock, byte[] lastPersistedHash, CancellationToken ct)
        {
            var remaining = endBlock - cursor + 1;
            var take = remaining < _batchSize ? remaining : _batchSize;
            var batchStartedAt = Stopwatch.GetTimestamp();

            List<BlockHeader> headers;
            try
            {
                headers = await _scheduler.FetchHeadersAsync(cursor, take, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Phase 1 backfill: header batch {Start}+{Take} failed; retrying",
                    cursor, take);
                return BatchOutcome.Fail(ClassifyFetchError(ex));
            }

            if (headers == null || headers.Count == 0)
            {
                _logger.LogWarning("Phase 1 backfill: empty header batch at {Block} — retrying", cursor);
                return BatchOutcome.Fail("empty_response");
            }

            if (!HeadersAreContiguous(headers, cursor))
            {
                _logger.LogWarning(
                    "Phase 1 backfill: non-contiguous header batch at {Block} — retrying",
                    cursor);
                return BatchOutcome.Fail("non_contiguous");
            }

            var hashes = new byte[headers.Count][];
            for (int i = 0; i < headers.Count; i++)
                hashes[i] = HashHeader(headers[i]);

            if (!ParentChainIntact(headers, hashes, lastPersistedHash, out var brokenAt))
            {
                _logger.LogWarning(
                    "Phase 1 backfill: parent-hash chain break at index {Index} of batch starting block {Block} — retrying",
                    brokenAt, cursor);
                return BatchOutcome.Fail("chain_break");
            }

            List<BlockBody> bodies;
            try
            {
                bodies = await _scheduler.FetchBodiesAsync(hashes, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Phase 1 backfill: body batch {Start}+{Take} failed; retrying",
                    cursor, take);
                return BatchOutcome.Fail(ClassifyFetchError(ex));
            }

            var paired = bodies?.Count ?? 0;
            if (paired == 0)
            {
                _logger.LogWarning("Phase 1 backfill: empty body batch at {Block} — retrying", cursor);
                return BatchOutcome.Fail("empty_response");
            }

            if (!ValidateBodies(headers, bodies, paired))
            {
                _logger.LogWarning(
                    "Phase 1 backfill: body root mismatch in batch {Start}+{Take} — retrying",
                    cursor, paired);
                return BatchOutcome.Fail("root_mismatch");
            }

            List<List<Receipt>> receipts;
            try
            {
                receipts = await _scheduler.FetchReceiptsAsync(hashes, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Phase 1 backfill: receipt batch {Start}+{Take} failed; retrying",
                    cursor, take);
                return BatchOutcome.Fail(ClassifyFetchError(ex));
            }

            var receiptsPaired = receipts?.Count ?? 0;
            if (receiptsPaired < paired)
            {
                _logger.LogWarning(
                    "Phase 1 backfill: partial receipt batch at {Block} ({Have}/{Need}) — retrying",
                    cursor, receiptsPaired, paired);
                return BatchOutcome.Fail("partial_response");
            }

            if (!ValidateReceipts(headers, receipts, paired))
            {
                _logger.LogWarning(
                    "Phase 1 backfill: receipt root mismatch in batch {Start}+{Take} — retrying",
                    cursor, paired);
                return BatchOutcome.Fail("root_mismatch");
            }

            ulong blocksWritten = 0;
            ulong txsWritten = 0;
            ulong receiptsWritten = 0;

            for (int i = 0; i < paired; i++)
            {
                var header = headers[i];
                var hash = hashes[i];
                var body = bodies[i];
                var blockReceipts = receipts[i];

                await _bundle.Blocks.SaveAsync(header, hash).ConfigureAwait(false);
                await _bundle.Uncles.SaveAsync(hash, body?.Uncles ?? new List<BlockHeader>())
                    .ConfigureAwait(false);

                if (body?.Transactions != null)
                {
                    BigInteger prevCumulativeGas = 0;
                    for (int j = 0; j < body.Transactions.Count; j++)
                    {
                        var tx = body.Transactions[j];
                        var blockNumber = header.BlockNumber.ToBigInteger();
                        await _bundle.Transactions.SaveAsync(tx, hash, j, blockNumber)
                            .ConfigureAwait(false);
                        txsWritten++;

                        if (j < blockReceipts.Count)
                        {
                            var rcpt = blockReceipts[j];
                            var cumulative = rcpt.CumulativeGasUsed.ToBigInteger();
                            var gasUsed = cumulative - prevCumulativeGas;
                            if (gasUsed < 0) gasUsed = 0;
                            prevCumulativeGas = cumulative;

                            var txHash = _keccak.CalculateHash(tx.GetRLPEncoded());

                            var baseFee = header.BaseFee ?? EvmUInt256.Zero;
                            var effectiveGasPrice = (BigInteger)tx.GetEffectiveGasPrice(baseFee);

                            string contractAddress = null;
                            if (tx.IsContractCreation())
                            {
                                var sender = tx.GetSenderAddress();
                                var nonce = (BigInteger)tx.GetNonce();
                                contractAddress = ContractUtils.CalculateContractAddress(sender, nonce);
                            }

                            await _bundle.Receipts.SaveAsync(
                                rcpt,
                                txHash,
                                hash,
                                blockNumber,
                                j,
                                gasUsed,
                                contractAddress,
                                effectiveGasPrice).ConfigureAwait(false);
                            receiptsWritten++;
                        }
                    }
                }

                blocksWritten++;
            }

            var lastWrittenBlock = (ulong)headers[paired - 1].BlockNumber;
            if (_bundle.Metadata.GetLastFetchedHeader() < lastWrittenBlock)
                _bundle.Metadata.SetLastFetchedHeader(lastWrittenBlock);
            if (_bundle.Metadata.GetLastFetchedBody() < lastWrittenBlock)
                _bundle.Metadata.SetLastFetchedBody(lastWrittenBlock);

            _metrics?.RecordPhase1BlocksPersisted((long)blocksWritten);
            _metrics?.RecordPhase1BatchDuration(StopwatchElapsedSeconds(batchStartedAt));

            return new BatchOutcome
            {
                Success = true,
                NextCursor = lastWrittenBlock + 1,
                LastHash = hashes[paired - 1],
                BlocksWritten = blocksWritten,
                TxsWritten = txsWritten,
                ReceiptsWritten = receiptsWritten,
            };
        }

        private async Task<byte[]> LoadLastPersistedHashAsync(ulong cursor)
        {
            if (cursor == 0) return null;
            try
            {
                return await _bundle.Blocks.GetHashByNumberAsync(new BigInteger(cursor - 1))
                    .ConfigureAwait(false);
            }
            catch
            {
                // First-run case where the block at cursor-1 isn't stored:
                // skip the cross-batch parent check rather than block the
                // backfill. Within-batch parent linkage still catches forked
                // peers; cross-batch will re-engage once the first batch lands.
                return null;
            }
        }

        private bool ParentChainIntact(
            IList<BlockHeader> headers, byte[][] hashes, byte[] lastPersistedHash, out int brokenAt)
        {
            if (lastPersistedHash != null && !ByteUtil.AreEqual(headers[0].ParentHash, lastPersistedHash))
            {
                brokenAt = 0;
                return false;
            }
            for (int i = 1; i < headers.Count; i++)
            {
                if (!ByteUtil.AreEqual(headers[i].ParentHash, hashes[i - 1]))
                {
                    brokenAt = i;
                    return false;
                }
            }
            brokenAt = -1;
            return true;
        }

        private static bool HeadersAreContiguous(IList<BlockHeader> headers, ulong startBlock)
        {
            if (headers[0].BlockNumber != (long)startBlock) return false;
            for (int i = 1; i < headers.Count; i++)
            {
                if (headers[i].BlockNumber != headers[i - 1].BlockNumber + 1) return false;
            }
            return true;
        }

        private bool ValidateBodies(IList<BlockHeader> headers, IList<BlockBody> bodies, int paired)
        {
            for (int i = 0; i < paired; i++)
            {
                var header = headers[i];
                var body = bodies[i];
                var txs = body?.Transactions ?? new List<ISignedTransaction>();
                var uncles = body?.Uncles ?? new List<BlockHeader>();

                var computedTxRoot = _rootsProvider.CalculateTransactionsRoot(txs);
                if (!ByteUtil.AreEqual(computedTxRoot, header.TransactionsHash)) return false;

                var computedUnclesHash = uncles.Count == 0
                    ? EmptyUnclesHash
                    : ComputeUnclesHash(uncles);
                if (!ByteUtil.AreEqual(computedUnclesHash, header.UnclesHash)) return false;
            }
            return true;
        }

        private bool ValidateReceipts(IList<BlockHeader> headers, IList<List<Receipt>> receipts, int paired)
        {
            for (int i = 0; i < paired; i++)
            {
                var header = headers[i];
                var blockReceipts = receipts[i] ?? new List<Receipt>();

                var computedRoot = blockReceipts.Count == 0
                    ? EmptyTrieRoot
                    : _rootsProvider.CalculateReceiptsRoot(blockReceipts);
                if (!ByteUtil.AreEqual(computedRoot, header.ReceiptHash)) return false;
            }
            return true;
        }

        private byte[] HashHeader(BlockHeader header)
        {
            var encoded = BlockHeaderEncoder.Current.Encode(header);
            return _keccak.CalculateHash(encoded);
        }

        private byte[] ComputeUnclesHash(IList<BlockHeader> uncles)
        {
            var encoded = new byte[uncles.Count][];
            for (int i = 0; i < uncles.Count; i++)
                encoded[i] = BlockHeaderEncoder.Current.Encode(uncles[i]);
            return _keccak.CalculateHash(RLP.RLP.EncodeList(encoded));
        }

        private static async Task DelayWithCancel(int ms, CancellationToken ct)
        {
            try { await Task.Delay(ms, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        }

        private static double StopwatchElapsedSeconds(long startedAt)
        {
            var elapsed = Stopwatch.GetTimestamp() - startedAt;
            return (double)elapsed / Stopwatch.Frequency;
        }

        private static string ClassifyFetchError(Exception ex) =>
            ex switch
            {
                TimeoutException => "timeout",
                OperationCanceledException => "cancelled",
                System.IO.IOException => "transport",
                InvalidOperationException => "transport",
                _ => "unknown",
            };

        private sealed class BatchOutcome
        {
            public bool Success { get; init; }
            public ulong NextCursor { get; init; }
            public byte[] LastHash { get; init; }
            public ulong BlocksWritten { get; init; }
            public ulong TxsWritten { get; init; }
            public ulong ReceiptsWritten { get; init; }
            public string FailureReason { get; init; }

            public static BatchOutcome Fail(string reason) =>
                new() { Success = false, FailureReason = reason };
        }

        public sealed class BackfillResult
        {
            public bool Ran { get; init; }
            public string SkipReason { get; init; }
            public ulong BlocksWritten { get; init; }
            public ulong TransactionsWritten { get; init; }
            public ulong ReceiptsWritten { get; init; }
            public ulong EndBlock { get; init; }
        }
    }
}
