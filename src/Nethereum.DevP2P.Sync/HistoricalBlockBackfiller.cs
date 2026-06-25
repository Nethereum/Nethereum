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
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.Codecs;
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

        private readonly IFetchRequestScheduler _scheduler;
        private readonly IChainStoreBundle _bundle;
        private readonly IBlockRootsProvider _rootsProvider;
        private readonly IChainActivations _activations;
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
            ulong batchSize = DefaultBatchSize,
            IChainActivations activations = null)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _rootsProvider = rootsProvider ?? PatriciaBlockRootsProvider.Instance;
            _logger = logger ?? NullLogger.Instance;
            _metrics = metrics;
            if (batchSize == 0) throw new ArgumentOutOfRangeException(nameof(batchSize));
            _batchSize = batchSize;
            // Optional fork resolver — when provided, each peer-supplied tx is
            // checked against TransactionTypeFork.IsAcceptedAt for its block's
            // fork so a malicious peer can't poison pre-Berlin blocks with
            // typed envelopes. When null, backward-compatible (no validation).
            _activations = activations;
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

            if (!BlockBatchValidator.ValidateParentChain(headers, hashes, lastPersistedHash, out var brokenAt))
            {
                _logger.LogWarning(
                    "Phase 1 backfill: parent-hash chain break at index {Index} of batch starting block {Block} — retrying",
                    brokenAt, cursor);
                return BatchOutcome.Fail("chain_break");
            }

            // Bodies and receipts have no data dependency on each other, so
            // they are fetched as separate concurrent requests. Awaiting
            // them via Task.WhenAll cuts one full RTT off the per-batch
            // critical path — roughly a 30-50% wall-clock win on a
            // ~300ms-RTT peer pool.
            List<BlockBody> bodies;
            List<List<Receipt>> receipts;
            try
            {
                var bodiesTask = _scheduler.FetchBodiesAsync(hashes, ct);
                var receiptsTask = _scheduler.FetchReceiptsAsync(hashes, ct);
                await Task.WhenAll(bodiesTask, receiptsTask).ConfigureAwait(false);
                bodies = bodiesTask.Result;
                receipts = receiptsTask.Result;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Phase 1 backfill: body+receipt batch {Start}+{Take} failed; retrying",
                    cursor, take);
                return BatchOutcome.Fail(ClassifyFetchError(ex));
            }

            var rawCount = bodies?.Count ?? 0;
            if (rawCount == 0)
            {
                _logger.LogWarning("Phase 1 backfill: empty body batch at {Block} — retrying", cursor);
                return BatchOutcome.Fail("empty_response");
            }

            // eth/68 lets a peer omit bodies it doesn't have. Without explicit
            // re-pairing a single skipped body shifts every later slot, fails
            // the whole batch, and we re-request endlessly. We re-pair each
            // returned body to a requested hash by recomputing (txRoot,
            // unclesHash) and matching against the headers.
            bodies = BlockBatchValidator.RealignBodies(headers, bodies, _rootsProvider, out var unmatchedAt);
            var paired = bodies.Count;
            if (paired == 0)
            {
                _logger.LogWarning(
                    "Phase 1 backfill: peer-returned bodies matched no header in batch {Start}+{Take} (raw={Raw}) — retrying",
                    cursor, take, rawCount);
                return BatchOutcome.Fail("realign_zero");
            }

            if (paired < headers.Count)
            {
                _logger.LogInformation(
                    "Phase 1 backfill: realigned {Paired}/{Total} bodies in batch {Start}+{Take} (peer skipped/misordered at index {Idx}); persisting prefix and re-requesting tail",
                    paired, headers.Count, cursor, take, unmatchedAt);
            }

            if (!BlockBatchValidator.ValidateBodies(headers, bodies, paired, _rootsProvider,
                    mismatch => LogBodyMismatchDiagnostics(
                        mismatch.Header, mismatch.Transactions, mismatch.Uncles,
                        mismatch.ComputedTxRoot, mismatch.TxRootOk,
                        mismatch.ComputedUnclesHash, mismatch.UnclesOk)))
            {
                // Realignment should make ValidateBodies a no-op safety net.
                // If we still fail here it's a genuine codec/encoder bug
                // (header field set, tx encoding, uncle encoding) — not a
                // peer-skipping issue. Keep the same retry path.
                _logger.LogWarning(
                    "Phase 1 backfill: body root mismatch after realign in batch {Start}+{Take} — retrying",
                    cursor, paired);
                return BatchOutcome.Fail("root_mismatch");
            }

            var rawReceiptCount = receipts?.Count ?? 0;
            if (rawReceiptCount == 0)
            {
                _logger.LogWarning(
                    "Phase 1 backfill: empty receipt batch at {Block} — retrying", cursor);
                return BatchOutcome.Fail("empty_receipts");
            }

            // Same re-pairing principle as bodies: match each returned receipt
            // list to a requested header by recomputing the receipt-trie root
            // against header.ReceiptHash; mismatched/missing ones get re-queued
            // instead of failing the batch.
            // Pre-Byzantium has a twist: some peers drop the 32-byte PostStateRoot
            // on storage and emit a 1-byte status, so the computed wire root cannot
            // match the canonical header root. For those blocks we positional-pair
            // off the unconsumed receipts — Phase 2 re-execution will recompute
            // canonical receipts when it runs over the block.
            receipts = BlockBatchValidator.RealignReceipts(
                headers, receipts, paired, _rootsProvider,
                _activations == null ? null : (Func<BlockHeader, bool>)IsPostByzantium,
                out var receiptsUnmatchedAt);
            if (receipts.Count < paired)
            {
                paired = receipts.Count;
                if (paired == 0)
                {
                    _logger.LogWarning(
                        "Phase 1 backfill: peer-returned receipts matched no header in batch {Start}+{Take} (raw={Raw}) — retrying",
                        cursor, take, rawReceiptCount);
                    return BatchOutcome.Fail("realign_zero_receipts");
                }
                _logger.LogInformation(
                    "Phase 1 backfill: realigned {Paired}/{Total} receipts in batch {Start}+{Take} (peer skipped/misordered at index {Idx}); persisting prefix and re-requesting tail",
                    paired, headers.Count, cursor, take, receiptsUnmatchedAt);
                // Truncate bodies to the same matched prefix so per-block
                // persistence below stays aligned.
                bodies = bodies.GetRange(0, paired);
            }

            if (!BlockBatchValidator.ValidateReceipts(
                    headers, receipts, paired, _rootsProvider,
                    _activations == null ? null : (Func<BlockHeader, bool>)IsPostByzantium,
                    mismatch => LogReceiptMismatchDiagnostics(
                        mismatch.Header, (List<Receipt>)mismatch.BlockReceipts, mismatch.ComputedRoot)))
            {
                _logger.LogWarning(
                    "Phase 1 backfill: receipt root mismatch in batch {Start}+{Take} — retrying",
                    cursor, paired);
                return BatchOutcome.Fail("root_mismatch");
            }

            if (_activations != null && !ValidateTransactionTypes(headers, bodies, paired, out var rejectedAtBlock, out var rejectedType))
            {
                _logger.LogWarning(
                    "Phase 1 backfill: peer returned disallowed tx type {Type} at block {Block} " +
                    "(not active at this fork) — rejecting batch {Start}+{Take}",
                    rejectedType, rejectedAtBlock, cursor, paired);
                return BatchOutcome.Fail("tx_type_not_active_at_fork");
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

                            // Index logs so eth_getLogs across backfilled ranges
                            // returns the same data as a fully-executed node.
                            // Mirrors BlockProducer / BlockImporter pattern: the
                            // per-tx logs go into ILogStore, the combined block
                            // bloom goes in once per block (below the loop).
                            if (_bundle.Logs != null && rcpt.Logs != null && rcpt.Logs.Count > 0)
                            {
                                await _bundle.Logs.SaveLogsAsync(
                                    rcpt.Logs, txHash, hash, blockNumber, j).ConfigureAwait(false);
                            }
                        }
                    }
                }

                // Per-block bloom for eth_getLogs pre-filtering — the wire
                // bytes already carry the canonical combined bloom in
                // header.LogsBloom, so re-use it rather than re-aggregate.
                if (_bundle.Logs != null && header.LogsBloom != null && header.LogsBloom.Length == 256)
                {
                    await _bundle.Logs.SaveBlockBloomAsync(
                        header.BlockNumber.ToBigInteger(), header.LogsBloom).ConfigureAwait(false);
                }

                blocksWritten++;
            }

            var lastWrittenBlock = (ulong)headers[paired - 1].BlockNumber;
            var advanceHeader = _bundle.Metadata.GetLastFetchedHeader() < lastWrittenBlock;
            var advanceBody = _bundle.Metadata.GetLastFetchedBody() < lastWrittenBlock;
            if (advanceHeader || advanceBody)
            {
                using var cursorBatch = _bundle.BeginBatch();
                cursorBatch.SetLastFetchedHeaderAndBody(
                    advanceHeader ? lastWrittenBlock : _bundle.Metadata.GetLastFetchedHeader(),
                    advanceBody ? lastWrittenBlock : _bundle.Metadata.GetLastFetchedBody());
                await cursorBatch.CommitAsync(ct).ConfigureAwait(false);
            }

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

        private bool IsPostByzantium(BlockHeader header)
        {
            var blockNumber = (long)header.BlockNumber.ToBigInteger();
            var fork = _activations.ResolveAt(blockNumber, (ulong)header.Timestamp);
            return fork >= HardforkName.Byzantium;
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

        private void LogBodyMismatchDiagnostics(
            BlockHeader header,
            IList<ISignedTransaction> txs,
            IList<BlockHeader> uncles,
            byte[] computedTxRoot, bool txRootOk,
            byte[] computedUnclesHash, bool unclesOk)
        {
            var blockNumber = (long)header.BlockNumber.ToBigInteger();
            var fork = _activations?.ResolveAt(blockNumber, (ulong)header.Timestamp);
            var requestHash = HashHeader(header);
            _logger.LogWarning(
                "BodyRootMismatch block={Block} fork={Fork} requestHash={ReqHash} txs={TxCount} uncles={UncleCount} txRootOk={TxOk} unclesOk={UnclesOk} expectedTxRoot={ExpectedTx} computedTxRoot={ComputedTx} expectedUnclesHash={ExpectedU} computedUnclesHash={ComputedU}",
                blockNumber,
                fork?.ToString() ?? "unknown",
                requestHash.ToHex(),
                txs.Count, uncles.Count,
                txRootOk, unclesOk,
                header.TransactionsHash.ToHex(),
                computedTxRoot.ToHex(),
                header.UnclesHash.ToHex(),
                computedUnclesHash.ToHex());

            // Dump every tx — re-encoded RLP that drove the trie root, plus
            // the runtime type so we can see if we decoded a legacy tx into
            // a LegacyTransactionChainId or vice versa (V/chainId encoding
            // is the most common round-trip break pre-EIP-155 at this fork).
            for (int j = 0; j < txs.Count; j++)
            {
                var tx = txs[j];
                byte[] encoded = null;
                try { encoded = tx.GetRLPEncoded(); } catch { }
                _logger.LogWarning(
                    "  tx[{Idx}] type={Type} encodedLen={Len} encodedHead={Head}",
                    j,
                    tx.GetType().Name,
                    encoded?.Length ?? -1,
                    encoded == null ? "<null>"
                        : encoded.AsSpan(0, System.Math.Min(96, encoded.Length)).ToArray().ToHex());
            }

            for (int j = 0; j < uncles.Count; j++)
            {
                _logger.LogWarning(
                    "  uncle[{Idx}] number={Number} hash-via-encoder={Hash}",
                    j,
                    (long)uncles[j].BlockNumber.ToBigInteger(),
                    _keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(uncles[j])).ToHex());
            }
        }

        private void LogReceiptMismatchDiagnostics(
            BlockHeader header, List<Receipt> blockReceipts, byte[] computedRoot)
        {
            var blockNumber = (long)header.BlockNumber.ToBigInteger();
            var fork = _activations?.ResolveAt(blockNumber, (ulong)header.Timestamp);
            _logger.LogWarning(
                "ReceiptRootMismatch block={Block} fork={Fork} count={Count} expected={Expected} computed={Computed}",
                blockNumber,
                fork?.ToString() ?? "unknown",
                blockReceipts.Count,
                header.ReceiptHash.ToHex(),
                computedRoot.ToHex());

            for (int j = 0; j < blockReceipts.Count; j++)
            {
                var rcpt = blockReceipts[j];
                var pss = rcpt.PostStateOrStatus ?? new byte[0];
                // EncodeReceipt round-trips through whatever the rootsProvider
                // would feed into the trie — surfacing it confirms whether
                // the bytes that drive the root match what the peer sent.
                byte[] roundTrip = null;
                try { roundTrip = ReceiptEncoder.Current.Encode(rcpt); } catch { }
                _logger.LogWarning(
                    "  receipt[{Idx}] txType={Type} pssLen={PssLen} pss={Pss} cumGas={CumGas} logs={Logs} bloomNonZero={BloomNonZero} roundTripLen={RoundTripLen} roundTripHead={RoundTripHead}",
                    j,
                    rcpt.TransactionType,
                    pss.Length,
                    pss.ToHex(),
                    rcpt.CumulativeGasUsed.ToString(),
                    rcpt.Logs?.Count ?? 0,
                    rcpt.Bloom != null && !IsAllZero(rcpt.Bloom),
                    roundTrip?.Length ?? -1,
                    roundTrip == null ? "<null>" : roundTrip.AsSpan(0, System.Math.Min(64, roundTrip.Length)).ToArray().ToHex());
            }
        }

        private static bool IsAllZero(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++) if (bytes[i] != 0) return false;
            return true;
        }

        // Strict per-fork tx-type acceptance gate: rejects txs whose type
        // exceeds the chain config's active set for that block — guards the
        // archive store against peers that lie about block contents to slip
        // pre-EIP-2718 chains into accepting typed envelopes.
        private bool ValidateTransactionTypes(
            IList<BlockHeader> headers,
            IList<BlockBody> bodies,
            int paired,
            out long rejectedAtBlock,
            out string rejectedType)
        {
            rejectedAtBlock = 0;
            rejectedType = null;

            for (int i = 0; i < paired; i++)
            {
                var header = headers[i];
                var body = bodies[i];
                if (body?.Transactions == null || body.Transactions.Count == 0) continue;

                var blockNumber = (long)header.BlockNumber.ToBigInteger();
                var fork = _activations.ResolveAt(blockNumber, (ulong)header.Timestamp);

                foreach (var tx in body.Transactions)
                {
                    if (!TransactionTypeFork.IsAcceptedAt(tx, fork))
                    {
                        rejectedAtBlock = blockNumber;
                        rejectedType = tx.GetType().Name;
                        return false;
                    }
                }
            }
            return true;
        }

        private byte[] HashHeader(BlockHeader header)
        {
            var encoded = BlockHeaderEncoder.Current.Encode(header);
            return _keccak.CalculateHash(encoded);
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
