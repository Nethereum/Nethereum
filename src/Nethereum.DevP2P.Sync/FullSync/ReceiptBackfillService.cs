using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.FullSync
{
    public sealed class ReceiptBackfillService
    {
        public sealed class Options
        {
            public int BatchSize { get; set; } = 64;

            public ulong FromBlock { get; set; } = 0;

            public ulong ToBlock { get; set; } = 0;

            public TimeSpan IdlePoll { get; set; } = TimeSpan.FromSeconds(5);

            public TimeSpan FetchTimeout { get; set; } = TimeSpan.FromSeconds(30);

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

        private sealed class ReceiptCursor : IBodyFillCursor
        {
            private readonly IChainStoreBundle _bundle;
            private readonly Stats _stats;

            public ReceiptCursor(IChainStoreBundle bundle, Stats stats)
            {
                _bundle = bundle;
                _stats = stats;
            }

            public ulong Get() => _stats.Cursor;

            public Task AdvanceAsync(ulong highestPersisted, CancellationToken ct)
            {
                if (highestPersisted > _stats.Cursor)
                {
                    _stats.Cursor = highestPersisted;
                    _bundle.Metadata.SetReceiptBackfillCursor(highestPersisted);
                }
                return Task.CompletedTask;
            }
        }

        private readonly IChainStoreBundle _bundle;
        private readonly IPeerPool _pool;
        private readonly IFetchRequestScheduler _scheduler;
        private readonly IPeerRequestWorker _worker;
        private readonly IBlockRootsProvider _rootsProvider;
        private readonly Options _options;
        private readonly ILogger _logger;

        public Stats Counters { get; } = new Stats();

        [Obsolete("Receipts are fetched by the backfiller receipt role, which needs the peer pool: use the overload that takes an IPeerPool.")]
        public ReceiptBackfillService(
            IChainStoreBundle bundle,
            IFetchRequestScheduler scheduler,
            IBlockRootsProvider rootsProvider = null,
            Options options = null,
            ILogger logger = null)
            : this(bundle, scheduler, null, rootsProvider, options, logger, null)
        {
        }

        public ReceiptBackfillService(
            IChainStoreBundle bundle,
            IFetchRequestScheduler scheduler,
            IPeerPool pool,
            IBlockRootsProvider rootsProvider = null,
            Options options = null,
            ILogger logger = null,
            IPeerRequestWorker worker = null)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _pool = pool;
            _worker = worker ?? new PeerRequestWorker();
            _rootsProvider = rootsProvider ?? PatriciaBlockRootsProvider.Instance;
            _options = options ?? new Options();
            _logger = logger ?? NullLogger.Instance;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            if (_pool == null)
                throw new InvalidOperationException(
                    "ReceiptBackfillService has no peer pool: construct it with the overload that takes an IPeerPool.");

            var startCursor = Math.Max(_bundle.Metadata.GetReceiptBackfillCursor(), _options.FromBlock);
            Counters.Cursor = startCursor;
            LogStarting(startCursor);

            var backfiller = new ParallelBlockBackfiller(
                _scheduler, _pool, _worker, _bundle, _rootsProvider, _logger, role: ParallelBlockBackfiller.ReceiptsRole);
            var cursorStore = new ReceiptCursor(_bundle, Counters);
            ulong lastBlocks = 0, lastReceipts = 0, lastUnmatched = 0;

            var lastProgressLog = DateTime.UtcNow;

            while (!ct.IsCancellationRequested)
            {
                if ((_bundle as IBackfillPauseControl)?.ShouldYieldToExecution() == true)
                {
                    if (!await IdleAsync(ct).ConfigureAwait(false)) return;
                    continue;
                }

                var cursor = Counters.Cursor;
                var head = ResolveHeadBlock();
                if (cursor >= head)
                {
                    if (!await IdleAsync(ct).ConfigureAwait(false)) return;
                    continue;
                }

                ulong batchEnd = cursor + (ulong)_options.BatchSize;
                if (batchEnd > head) batchEnd = head;

                var firstHash = await _bundle.Blocks.GetHashByNumberAsync(new BigInteger(cursor + 1)).ConfigureAwait(false);
                if (firstHash == null)
                {
                    SkipUndeliveredBatch(cursor, batchEnd);
                    continue;
                }

                ParallelBlockBackfiller.BackfillResult result;
                try
                {
                    result = await backfiller.BackfillAsync(cursor + 1, batchEnd, cursorStore, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Receipt-backfill: batch [{From},{To}] failed - retrying", cursor + 1, batchEnd);
                    if (!await IdleAsync(ct).ConfigureAwait(false)) return;
                    continue;
                }

                Counters.BatchesProcessed++;
                Counters.BlocksValidated += (long)(result.BlocksWritten - lastBlocks);
                Counters.ReceiptsPersisted += (long)(result.ReceiptsWritten - lastReceipts);
                Counters.BlocksMismatched += (long)(result.ReceiptsUnmatched - lastUnmatched);
                lastBlocks = result.BlocksWritten;
                lastReceipts = result.ReceiptsWritten;
                lastUnmatched = result.ReceiptsUnmatched;

                if (Counters.Cursor == cursor && !await IdleAsync(ct).ConfigureAwait(false)) return;

                lastProgressLog = LogProgressWhenDue(lastProgressLog, head);
            }
        }

        private void LogStarting(ulong startCursor)
        {
            _logger.LogInformation(
                "Receipt-backfill: starting at block {Start} (executor frontier {Head}{ToBlock})",
                startCursor,
                _bundle.Metadata.GetLastBlock(),
                _options.ToBlock > 0 ? $", upper bound {_options.ToBlock}" : "");
        }

        private async Task<bool> IdleAsync(CancellationToken ct)
        {
            try { await Task.Delay(_options.IdlePoll, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return false; }
            return true;
        }

        private void SkipUndeliveredBatch(ulong cursor, ulong batchEnd)
        {
            _logger.LogWarning(
                "Receipt-backfill: no headers in [{From},{To}] - skipping (initial sync has not delivered them yet)",
                cursor + 1, batchEnd);
            Counters.Cursor = batchEnd;
            _bundle.Metadata.SetReceiptBackfillCursor(batchEnd);
        }

        private DateTime LogProgressWhenDue(DateTime lastProgressLog, ulong head)
        {
            if (DateTime.UtcNow - lastProgressLog < _options.ProgressLogInterval) return lastProgressLog;
            _logger.LogInformation(
                "Receipt-backfill: cursor={Cursor} validated={V} mismatched={M} receipts={R} (head={Head})",
                Counters.Cursor, Counters.BlocksValidated, Counters.BlocksMismatched,
                Counters.ReceiptsPersisted, head);
            return DateTime.UtcNow;
        }

        private ulong ResolveHeadBlock()
        {
            var executorHead = _bundle.Metadata.GetLastBlock();
            var bodyHead = _bundle.Metadata.GetLastFetchedBody();
            var head = executorHead < bodyHead ? executorHead : bodyHead;
            if (_options.ToBlock > 0 && _options.ToBlock < head) head = _options.ToBlock;
            return head;
        }
    }
}
