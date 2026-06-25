using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Long-running background scrub job that re-fetches receipts for the
    /// already-synced block range and overwrites stored entries with
    /// canonically-validated payloads plus correctly-computed metadata
    /// (<c>contractAddress</c>, <c>gasUsed</c>, <c>effectiveGasPrice</c>).
    ///
    /// <para>Runs alongside <c>FollowerService</c>. Walks <c>[cursor, head]</c>
    /// from <see cref="IChainMetadataStore.GetReceiptBackfillCursor"/> upward.
    /// For each block batch: fetches receipts via the shared
    /// <see cref="IFetchRequestScheduler"/> (which uses the full peer pool, not
    /// just trusted/pinned peers), validates each batch's Patricia receipts-root
    /// against the stored header's <c>ReceiptHash</c>, and on match persists
    /// each receipt with metadata recomputed from the stored transaction. On
    /// mismatch the block is skipped this iteration; the cursor stays put so
    /// the next iteration retries, naturally picking a different peer through
    /// the scheduler's peer rotation.</para>
    ///
    /// <para>Resumable across restarts via the persisted cursor. Terminal
    /// behaviour: when cursor reaches the executor's frontier
    /// (<see cref="IChainMetadataStore.GetLastBlock"/>) the job sleeps and
    /// polls; as the executor advances, new blocks become eligible and the
    /// scrub continues.</para>
    /// </summary>
    public sealed class ReceiptBackfillService
    {
        public sealed class Options
        {
            /// <summary>Block-batch size per scheduler round-trip. Default 64.</summary>
            public int BatchSize { get; set; } = 64;

            /// <summary>Lower bound on the scrub range — start from this block if
            /// the persisted cursor is below it (0 = walk from genesis).</summary>
            public ulong FromBlock { get; set; } = 0;

            /// <summary>Optional upper bound (0 = unlimited, follow the executor's
            /// frontier). Useful for forced-scope scrubs of a known-bad range.</summary>
            public ulong ToBlock { get; set; } = 0;

            /// <summary>Sleep between iterations when no progress is possible
            /// (cursor at head, or every batch in the current chunk failed
            /// merkle). Default 5 seconds.</summary>
            public TimeSpan IdlePoll { get; set; } = TimeSpan.FromSeconds(5);

            /// <summary>Per-batch fetch timeout passed to the scheduler.</summary>
            public TimeSpan FetchTimeout { get; set; } = TimeSpan.FromSeconds(30);

            /// <summary>Progress-log interval. Default 10 seconds.</summary>
            public TimeSpan ProgressLogInterval { get; set; } = TimeSpan.FromSeconds(10);
        }

        public sealed class Stats
        {
            public long BatchesProcessed { get; internal set; }
            public long BlocksValidated { get; internal set; }
            public long BlocksMismatched { get; internal set; }
            public long ReceiptsPersisted { get; internal set; }
            public ulong Cursor { get; internal set; }
        }

        private static readonly byte[] EmptyTrieRoot =
            "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();

        private readonly IChainStoreBundle _bundle;
        private readonly IFetchRequestScheduler _scheduler;
        private readonly IBlockRootsProvider _rootsProvider;
        private readonly Options _options;
        private readonly ILogger _logger;
        private readonly IHashProvider _keccak;

        public Stats Counters { get; } = new Stats();

        public ReceiptBackfillService(
            IChainStoreBundle bundle,
            IFetchRequestScheduler scheduler,
            IBlockRootsProvider rootsProvider = null,
            Options options = null,
            ILogger logger = null)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _rootsProvider = rootsProvider ?? PatriciaBlockRootsProvider.Instance;
            _options = options ?? new Options();
            _logger = logger ?? NullLogger.Instance;
            _keccak = new Sha3KeccackHashProvider();
        }

        public async Task RunAsync(CancellationToken ct)
        {
            var startCursor = Math.Max(_bundle.Metadata.GetReceiptBackfillCursor(), _options.FromBlock);
            Counters.Cursor = startCursor;

            _logger.LogInformation(
                "Receipt-backfill: starting at block {Start} (executor frontier {Head}{ToBlock})",
                startCursor,
                _bundle.Metadata.GetLastBlock(),
                _options.ToBlock > 0 ? $", upper bound {_options.ToBlock}" : "");

            var lastProgressLog = DateTime.UtcNow;

            while (!ct.IsCancellationRequested)
            {
                var cursor = Counters.Cursor;
                var head = ResolveHeadBlock();
                if (cursor >= head)
                {
                    // Caught up — wait for the executor to advance.
                    try { await Task.Delay(_options.IdlePoll, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                    continue;
                }

                ulong batchEnd = cursor + (ulong)_options.BatchSize;
                if (batchEnd > head) batchEnd = head;

                var (headers, hashes) = LoadHeaderBatch(cursor + 1, batchEnd);
                if (headers.Count == 0)
                {
                    // Headers missing locally — the initial fetch never landed
                    // them. Skip the chunk, advance cursor so we don't wedge.
                    _logger.LogWarning(
                        "Receipt-backfill: no headers in [{From},{To}] — skipping (initial sync hasn't delivered them yet)",
                        cursor + 1, batchEnd);
                    Counters.Cursor = batchEnd;
                    _bundle.Metadata.SetReceiptBackfillCursor(batchEnd);
                    continue;
                }

                List<List<Receipt>> fetched;
                try
                {
                    using var fetchCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    fetchCts.CancelAfter(_options.FetchTimeout);
                    fetched = await _scheduler.FetchReceiptsAsync(hashes, fetchCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                catch (Exception ex)
                {
                    _logger.LogDebug("Receipt-backfill: fetch batch failed [{From},{To}]: {Err}",
                        cursor + 1, batchEnd, ex.GetType().Name);
                    try { await Task.Delay(_options.IdlePoll, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                    continue;
                }

                Counters.BatchesProcessed++;

                int matched = 0, mismatched = 0;
                ulong nextCursor = cursor;
                bool advanced = false;

                for (int i = 0; i < headers.Count; i++)
                {
                    var header = headers[i];
                    var blockHash = hashes[i];
                    ulong blockNumber = cursor + 1 + (ulong)i;

                    if (i >= fetched.Count)
                    {
                        // Peer truncated batch — stop advancing cursor here, retry next iteration.
                        break;
                    }

                    var receipts = fetched[i] ?? new List<Receipt>();
                    var computed = receipts.Count == 0
                        ? EmptyTrieRoot
                        : _rootsProvider.CalculateReceiptsRoot(receipts);

                    if (!ByteArrayEquals(computed, header.ReceiptHash))
                    {
                        mismatched++;
                        Counters.BlocksMismatched++;
                        // Don't advance past a mismatched block; the next iteration
                        // re-fetches via the scheduler which rotates to other peers.
                        break;
                    }

                    try
                    {
                        await PersistReceiptsForBlockAsync(header, blockHash, blockNumber, receipts, ct)
                            .ConfigureAwait(false);
                        matched++;
                        Counters.BlocksValidated++;
                        nextCursor = blockNumber;
                        advanced = true;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Receipt-backfill: persist failed at block {Block} — leaving cursor at {Cursor}",
                            blockNumber, nextCursor);
                        break;
                    }
                }

                if (advanced)
                {
                    Counters.Cursor = nextCursor;
                    _bundle.Metadata.SetReceiptBackfillCursor(nextCursor);
                }
                else
                {
                    // Whole batch failed validation — back off briefly so we
                    // don't spin against the same peer at full rate.
                    try { await Task.Delay(_options.IdlePoll, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                }

                if (DateTime.UtcNow - lastProgressLog >= _options.ProgressLogInterval)
                {
                    _logger.LogInformation(
                        "Receipt-backfill: cursor={Cursor} validated={V} mismatched={M} receipts={R} (head={Head})",
                        Counters.Cursor, Counters.BlocksValidated, Counters.BlocksMismatched,
                        Counters.ReceiptsPersisted, head);
                    lastProgressLog = DateTime.UtcNow;
                }
            }
        }

        private ulong ResolveHeadBlock()
        {
            var executorHead = _bundle.Metadata.GetLastBlock();
            var bodyHead = _bundle.Metadata.GetLastFetchedBody();
            // Can only scrub blocks we have bodies for (need txs to recompute
            // metadata) and that the executor has accepted (no scrub above
            // the verified frontier).
            var head = executorHead < bodyHead ? executorHead : bodyHead;
            if (_options.ToBlock > 0 && _options.ToBlock < head) head = _options.ToBlock;
            return head;
        }

        private (List<BlockHeader> Headers, List<byte[]> Hashes) LoadHeaderBatch(ulong fromBlock, ulong toBlockInclusive)
        {
            var headers = new List<BlockHeader>((int)(toBlockInclusive - fromBlock + 1));
            var hashes = new List<byte[]>((int)(toBlockInclusive - fromBlock + 1));
            for (ulong n = fromBlock; n <= toBlockInclusive; n++)
            {
                var hash = _bundle.Blocks.GetHashByNumberAsync(new BigInteger(n)).GetAwaiter().GetResult();
                if (hash == null) break;
                var header = _bundle.Blocks.GetByHashAsync(hash).GetAwaiter().GetResult();
                if (header == null) break;
                headers.Add(header);
                hashes.Add(hash);
            }
            return (headers, hashes);
        }

        private async Task PersistReceiptsForBlockAsync(
            BlockHeader header, byte[] blockHash, ulong blockNumber, List<Receipt> receipts, CancellationToken ct)
        {
            var txs = await _bundle.Transactions.GetByBlockHashAsync(blockHash).ConfigureAwait(false);
            if (txs == null || txs.Count < receipts.Count)
            {
                _logger.LogWarning(
                    "Receipt-backfill: block {Block} has {RcptCount} receipts but only {TxCount} stored txs — skipping persist",
                    blockNumber, receipts.Count, txs?.Count ?? 0);
                return;
            }

            BigInteger prevCumulativeGas = 0;
            for (int j = 0; j < receipts.Count; j++)
            {
                if (ct.IsCancellationRequested) return;

                var rcpt = receipts[j];
                var tx = txs[j];
                var cumulative = rcpt.CumulativeGasUsed.ToBigInteger();
                var gasUsed = cumulative - prevCumulativeGas;
                if (gasUsed < 0) gasUsed = 0;
                prevCumulativeGas = cumulative;

                var txHash = _keccak.ComputeHash(tx.GetRLPEncoded());
                var baseFee = header.BaseFee ?? Nethereum.Util.EvmUInt256.Zero;
                var effectiveGasPrice = (BigInteger)tx.GetEffectiveGasPrice(baseFee);

                string contractAddress = null;
                if (tx.IsContractCreation())
                {
                    var sender = tx.GetSenderAddress();
                    var nonce = (BigInteger)tx.GetNonce();
                    contractAddress = ContractUtils.CalculateContractAddress(sender, nonce);
                }

                await _bundle.Receipts.SaveAsync(
                    rcpt, txHash, blockHash, new BigInteger(blockNumber), j,
                    gasUsed, contractAddress, effectiveGasPrice).ConfigureAwait(false);
                Counters.ReceiptsPersisted++;
            }
        }

        private static bool ByteArrayEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
