using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;

namespace Nethereum.DevP2P.Sync.FullSync
{
    public sealed class TipBandBodyFollowService
    {
        private static readonly TimeSpan RequeueWaitStep = TimeSpan.FromMilliseconds(50);

        private readonly IFetchRequestScheduler _scheduler;
        private readonly IPeerPool _pool;
        private readonly IPeerRequestWorker _worker;
        private readonly IChainStoreBundle _bundle;
        private readonly IChainActivations _activations;
        private readonly TimeSpan _pollInterval;
        private readonly ILogger _logger;
        private readonly ExecutionHeadBodyFillCursor _cursor;

        private readonly object _requeueGate = new object();
        private readonly SemaphoreSlim _wake = new SemaphoreSlim(0);
        private ulong? _pendingRequeueFrom;
        private CancellationTokenSource _passCts;

        private ulong _lastFollowedCursor;
        private DateTimeOffset _lastFollowLogAt;

        public TipBandBodyFollowService(
            IFetchRequestScheduler scheduler,
            IPeerPool pool,
            IChainStoreBundle bundle,
            IChainActivations activations,
            ILogger logger = null,
            TimeSpan? pollInterval = null)
            : this(scheduler, pool, bundle, activations, new PeerRequestWorker(), logger, pollInterval)
        {
        }

        public TipBandBodyFollowService(
            IFetchRequestScheduler scheduler,
            IPeerPool pool,
            IChainStoreBundle bundle,
            IChainActivations activations,
            IPeerRequestWorker worker,
            ILogger logger = null,
            TimeSpan? pollInterval = null)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _activations = activations ?? throw new ArgumentNullException(nameof(activations));
            _worker = worker ?? throw new ArgumentNullException(nameof(worker));
            _logger = logger ?? NullLogger.Instance;
            _pollInterval = pollInterval ?? TimeSpan.FromSeconds(12);
            _cursor = new ExecutionHeadBodyFillCursor(bundle);
        }

        public void RequeueFrom(ulong fromBlock)
        {
            lock (_requeueGate)
            {
                _pendingRequeueFrom = _pendingRequeueFrom is ulong pending && pending < fromBlock ? pending : fromBlock;
                _passCts?.Cancel();
            }
            if (_wake.CurrentCount == 0) _wake.Release();
        }

        public async Task<bool> RequeueAndWaitAsync(ulong fromBlock, CancellationToken ct)
        {
            RequeueFrom(fromBlock);
            var waited = Stopwatch.StartNew();
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                if (IsFilledPast(fromBlock)) return true;
                if (waited.Elapsed >= _pollInterval) return false;
                await Task.Delay(RequeueWaitStep, ct).ConfigureAwait(false);
            }
        }

        private bool IsFilledPast(ulong block)
        {
            lock (_requeueGate)
                if (_pendingRequeueFrom != null) return false;
            return _cursor.Get() >= block;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            var backfiller = new ParallelBlockBackfiller(
                _scheduler, _pool, _worker, _bundle,
                rootsProvider: null, logger: _logger, activations: _activations, role: ParallelBlockBackfiller.TipBandRole);

            _logger.LogInformation(
                "tip.body.follow starting — tracking [execution head+1 .. trusted tip], poll={Poll}s",
                _pollInterval.TotalSeconds);

            while (!ct.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();
                await FillOnceAsync(backfiller, _cursor, ct).ConfigureAwait(false);
                await _wake.WaitAsync(_pollInterval, ct).ConfigureAwait(false);
            }
        }

        public async Task FillOnceAsync(
            ParallelBlockBackfiller backfiller, ExecutionHeadBodyFillCursor cursor, CancellationToken ct)
        {
            ulong floor = 0, top = 0;
            using var passCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            try
            {
                lock (_requeueGate)
                {
                    if (_pendingRequeueFrom is ulong requeueFrom)
                    {
                        cursor.Requeue(requeueFrom);
                        _pendingRequeueFrom = null;
                    }
                    _passCts = passCts;
                }

                top = HeaderSubchains.TrustedTip(_bundle.Metadata.GetHeaderSyncState());
                floor = cursor.Get();
                if (top <= floor) return;

                await backfiller.BackfillAsync(0, top, cursor, passCts.Token).ConfigureAwait(false);

                var filledTo = cursor.Get();
                var nowFollow = DateTimeOffset.UtcNow;
                double secs = _lastFollowLogAt == default ? 0 : (nowFollow - _lastFollowLogAt).TotalSeconds;
                double rate = (secs > 0 && filledTo >= _lastFollowedCursor) ? (filledTo - _lastFollowedCursor) / secs : 0;
                _lastFollowedCursor = filledTo;
                _lastFollowLogAt = nowFollow;
                _logger.LogInformation(
                    "tip.body.follow band=[{Floor}..{Top}] filled_to={Filled} rate={Rate:F1} blk/s",
                    floor + 1, top, filledTo, rate);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (OperationCanceledException) when (passCts.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "tip.body.follow.requeued floor={Floor} tip={Tip} — pass restarted from the requeued block", floor, top);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "tip.body.follow.pass_failed floor={Floor} tip={Tip} — retrying", floor, top);
            }
            finally
            {
                lock (_requeueGate) _passCts = null;
            }
        }
    }
}
