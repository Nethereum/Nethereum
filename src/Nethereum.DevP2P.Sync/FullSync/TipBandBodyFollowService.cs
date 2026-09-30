using System;
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
        private readonly IFetchRequestScheduler _scheduler;
        private readonly IPeerPool _pool;
        private readonly IChainStoreBundle _bundle;
        private readonly IChainActivations _activations;
        private readonly TimeSpan _pollInterval;
        private readonly ILogger _logger;

        private ulong _lastFollowedCursor;
        private DateTimeOffset _lastFollowLogAt;

        public TipBandBodyFollowService(
            IFetchRequestScheduler scheduler,
            IPeerPool pool,
            IChainStoreBundle bundle,
            IChainActivations activations,
            ILogger logger = null,
            TimeSpan? pollInterval = null)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _activations = activations ?? throw new ArgumentNullException(nameof(activations));
            _logger = logger ?? NullLogger.Instance;
            _pollInterval = pollInterval ?? TimeSpan.FromSeconds(12);
        }

        public async Task RunAsync(CancellationToken ct)
        {
            var worker = new PeerRequestWorker();
            var backfiller = new ParallelBlockBackfiller(
                _scheduler, _pool, worker, _bundle,
                rootsProvider: null, logger: _logger, activations: _activations, role: "tipband");
            var cursor = new ExecutionHeadBodyFillCursor(_bundle);

            _logger.LogInformation(
                "tip.body.follow starting — tracking [execution head+1 .. trusted tip], poll={Poll}s",
                _pollInterval.TotalSeconds);

            while (!ct.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();
                await FillOnceAsync(backfiller, cursor, ct).ConfigureAwait(false);
                await Task.Delay(_pollInterval, ct).ConfigureAwait(false);
            }
        }

        internal async Task FillOnceAsync(
            ParallelBlockBackfiller backfiller, ExecutionHeadBodyFillCursor cursor, CancellationToken ct)
        {
            ulong floor = 0, top = 0;
            try
            {
                top = HeaderSubchains.TrustedTip(_bundle.Metadata.GetHeaderSyncState());
                floor = cursor.Get();
                if (top <= floor) return;

                await backfiller.BackfillAsync(0, top, cursor, ct).ConfigureAwait(false);

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
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "tip.body.follow.pass_failed floor={Floor} tip={Tip} — retrying", floor, top);
            }
        }
    }
}
