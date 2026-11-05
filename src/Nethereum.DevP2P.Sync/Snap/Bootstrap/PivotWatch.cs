using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.DevP2P.Sync.Common;
using Nethereum.Model;
using PivotState = Nethereum.DevP2P.Sync.Snap.Bootstrap.SnapBootstrapper.PivotState;
using RollingPivot = Nethereum.DevP2P.Sync.Snap.Bootstrap.SnapBootstrapper.RollingPivot;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal sealed class PivotWatch
    {
        internal static readonly TimeSpan RecycleWarnAfter = TimeSpan.FromMinutes(10);
        internal static readonly TimeSpan RecycleErrorAfter = TimeSpan.FromHours(1);

        private readonly IFetchRequestScheduler _scheduler;
        private readonly ILogger _logger;

        internal PivotWatch(
            RollingPivot rolling,
            Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? refresher,
            IFetchRequestScheduler scheduler,
            ILogger logger)
        {
            Rolling = rolling;
            Refresher = refresher;
            _scheduler = scheduler;
            _logger = logger;
        }

        internal RollingPivot Rolling { get; }

        internal PivotState Current => Rolling.Current;

        internal Func<bool, CancellationToken, Task<(BlockHeader Header, byte[] Hash)?>>? Refresher { get; }

        internal Func<CancellationToken, Task<byte[]>> AsClientRefresher()
        {
            return async refreshCt =>
            {
                var fresh = await FetchFreshAsync(false, refreshCt).ConfigureAwait(false);
                if (fresh.HasValue)
                    _logger.LogInformation(
                        "snap.pivot.target block={Block} root=0x{Root}",
                        fresh.Value.Header.BlockNumber, fresh.Value.Header.StateRoot.ToHex());
                return Current.Header.StateRoot;
            };
        }

        internal async Task<byte[]> AnchorFreshAtStartAsync(BlockHeader bootPivot, CancellationToken ct)
        {
            if (Refresher == null)
                return bootPivot.StateRoot;

            await FetchFreshAsync(true, ct).ConfigureAwait(false);

            var anchored = Current.Header;
            _logger.LogInformation(
                "snap.pivot.phase2_start block={Block} root=0x{Root}",
                anchored.BlockNumber, anchored.StateRoot.ToHex());
            return anchored.StateRoot;
        }

        internal async Task<PivotState?> AdvanceAsync(PivotState current, CancellationToken ct)
        {
            if (Refresher != null)
            {
                await FetchFreshAsync(true, ct).ConfigureAwait(false);
            }
            return SameRoot(Current, current) ? null : Current;
        }

        internal async Task<(BlockHeader Header, byte[] Hash)?> FetchFreshAsync(bool force, CancellationToken ct)
        {
            var fresh = await Refresher!(force, ct).ConfigureAwait(false);
            if (fresh.HasValue)
                Rolling.Adopt(new PivotState(fresh.Value.Header, fresh.Value.Hash));
            return fresh;
        }

        internal static bool Stalled(TrieHealer.HealResult result, PivotState healed, PivotState next)
            => result.TotalNodesFetched == 0 && SameRoot(next, healed);

        internal void ResetSchedulerIfMoved(byte[] previousRoot, PivotState next)
        {
            if (!Nethereum.Util.ByteUtil.AreEqual(next.Header.StateRoot, previousRoot))
                _scheduler?.OnTargetRootChanged();
        }

        internal async Task<PivotState?> WaitAfterNoProgressAsync(
            byte[] previousTarget, PeerJoinWatch peerJoins, SnapSyncMetrics? metrics, CancellationToken ct)
        {
            metrics?.RecordPhase3RecycleNoProgress();
            return await WaitForAdvancedAsync(previousTarget, peerJoins, ct).ConfigureAwait(false);
        }

        internal static bool SameRoot(PivotState a, PivotState b)
            => Nethereum.Util.ByteUtil.AreEqual(a.Header.StateRoot, b.Header.StateRoot);

        internal async Task<PivotState?> WaitForAdvancedAsync(
            byte[] previousTarget, PeerJoinWatch peerJoins, CancellationToken ct)
        {
            var waitingSince = DateTimeOffset.UtcNow;
            DateTimeOffset? lastLoggedAt = waitingSince;
            _logger.LogWarning(
                "snap.phase3.waiting_for_pivot root=0x{Root} pivot={Pivot} — no peer served this root " +
                "and the pivot has not moved; waiting for a new pivot or a new snap peer",
                previousTarget.ToHex(), Current.Header.BlockNumber);
            while (true)
            {
                var poll = Task.Delay(PivotTiming.PollInterval, ct);
                if (await Task.WhenAny(poll, peerJoins.Joined).ConfigureAwait(false) == peerJoins.Joined)
                {
                    _logger.LogInformation(
                        "snap.phase3.peer_joined peer={Host} root=0x{Root} waited={Waited} — healing the same root with the new peer",
                        peerJoins.Joined.Result.Host, previousTarget.ToHex(), DateTimeOffset.UtcNow - waitingSince);
                    return null;
                }
                await poll.ConfigureAwait(false);

                await PollAsync(ct).ConfigureAwait(false);
                var current = Current;
                if (!Nethereum.Util.ByteUtil.AreEqual(current.Header.StateRoot, previousTarget))
                {
                    _logger.LogInformation(
                        "snap.phase3.pivot_advanced block={Block} root=0x{Root} waited={Waited}",
                        current.Header.BlockNumber, current.Header.StateRoot.ToHex(), DateTimeOffset.UtcNow - waitingSince);
                    return current;
                }

                var now = DateTimeOffset.UtcNow;
                if (!PivotTiming.ShouldLogStalled(lastLoggedAt, now, RecycleWarnAfter)) continue;
                lastLoggedAt = now;
                LogStalledWait(previousTarget, now - waitingSince);
            }
        }

        private void LogStalledWait(byte[] previousTarget, TimeSpan waited)
        {
            if (waited >= RecycleErrorAfter)
                _logger.LogError(
                    "snap.phase3.waiting_for_pivot root=0x{Root} waited={Waited} — no new pivot and no new snap peer this long",
                    previousTarget.ToHex(), waited);
            else
                _logger.LogWarning(
                    "snap.phase3.waiting_for_pivot root=0x{Root} waited={Waited} — still waiting for a new pivot or a new snap peer",
                    previousTarget.ToHex(), waited);
        }

        internal async Task<bool> PollAsync(CancellationToken ct)
        {
            if (Refresher == null) return false;
            var (timedOut, refreshed) = await TimeBounded.RunAsync(
                pollCt => FetchFreshAsync(true, pollCt), PivotTiming.PollTimeout, ct).ConfigureAwait(false);
            if (!timedOut) return refreshed.HasValue;
            _logger.LogWarning(
                "snap.phase3.pivot_poll_timeout after={Timeout} — the pivot poll did not answer; keeping the current pivot",
                PivotTiming.PollTimeout);
            return false;
        }

        internal Func<bool, CancellationToken, Task<(byte[] Root, ulong Block)?>> AsHealerRefresher()
        {
            return async (forceFresh, refreshCt) =>
            {
                await FetchFreshAsync(forceFresh, refreshCt).ConfigureAwait(false);
                var cur = Current;
                return (cur.Header.StateRoot, (ulong)cur.Header.BlockNumber);
            };
        }
    }
}
