using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal sealed class SelfHeaderFollow
    {
        private readonly ILogger _logger;
        private CancellationTokenSource _cts;
        private readonly Task _followTask;

        private SelfHeaderFollow(CancellationTokenSource cts, Task followTask, bool headerFollowActive, ILogger logger)
        {
            _cts = cts;
            _followTask = followTask;
            HeaderFollowActive = headerFollowActive;
            _logger = logger;
        }

        internal bool HeaderFollowActive { get; }

        internal static SelfHeaderFollow Start(
            IFetchRequestScheduler scheduler, IChainStoreBundle bundle, ICanonicalStateRootSource canonicalTip,
            SnapSyncOrchestratorOptions options, ILogger logger, CancellationToken ct)
        {
            if (options.HeaderFollow != null)
                return new SelfHeaderFollow(null, null, true, logger);
            if (canonicalTip == null || !options.UseBackwardSkeleton)
                return new SelfHeaderFollow(null, null, false, logger);

            var headersOnlyWalker = HeaderSkeleton.CreateWalker(scheduler, bundle, logger);
            BackwardWalkerDelegate headersOnly = async (fromBlock, fromHash, toBlock, b, c, noShortCircuitAboveBlock) =>
            {
                Func<ulong, CancellationToken, Task<(byte[] hash, bool exists)>> lookup = async (bn, cc) =>
                {
                    cc.ThrowIfCancellationRequested();
                    var h = await b.Blocks.GetHashByNumberAsync(bn).ConfigureAwait(false);
                    return (h, h != null);
                };
                var r = await headersOnlyWalker.WalkAsync(fromBlock, fromHash, toBlock, lookup, c, noShortCircuitAboveBlock).ConfigureAwait(false);
                return new WalkerOutcome(r.ExitReason, r.HeadersWritten, r.DivergenceBlock,
                    SkeletonBottomBlock: r.SkeletonBottomBlock, MetExistingStore: r.MetExistingStore);
            };
            var resolver = new AncestorResolver(scheduler, bundle);
            var selfFollowOptions = new HeaderFollowOptions
            {
                PollInterval = TimeSpan.FromMilliseconds(options.RootRefreshIntervalMs),
                DescentIdleInterval = TimeSpan.FromMilliseconds(options.RootRefreshIntervalMs),
            };
            var selfFollow = new HeaderFollowService(
                canonicalTip, headersOnly, selfFollowOptions, logger,
                ancestorResolver: resolver.FindAsync);
            var selfFollowCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var selfFollowTask = Task.Run(() => selfFollow.RunAsync(bundle, selfFollowCts.Token), selfFollowCts.Token);
            return new SelfHeaderFollow(selfFollowCts, selfFollowTask, true, logger);
        }

        internal void HandOffTo(Task history)
        {
            if (_cts != null && !history.IsCompleted)
            {
                var jobCts = _cts;
                var jobTask = _followTask;
                _cts = null;
                _ = history.ContinueWith(_ => DrainJobAsync(jobCts, jobTask),
                    CancellationToken.None).Unwrap();
            }
        }

        internal async Task DrainAsync()
        {
            if (_cts != null)
                await DrainJobAsync(_cts, _followTask);
        }

        private async Task DrainJobAsync(CancellationTokenSource cts, Task task)
        {
            cts.Cancel();
            try { await task.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { _logger.LogDebug(ex, "snap.headers.self_follow_drain"); }
            cts.Dispose();
        }
    }
}
