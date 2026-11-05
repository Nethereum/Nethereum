using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using HealResult = Nethereum.DevP2P.Sync.Snap.Healing.TrieHealer.HealResult;

namespace Nethereum.DevP2P.Sync.Snap.Healing
{
    internal sealed class HealRotation
    {
        private const int StallThresholdRounds = 32;

        private readonly HealSession _session;
        private readonly HealFrontier _frontier;
        private readonly ILogger _logger;
        private readonly SnapSyncMetrics? _metrics;
        private readonly Func<bool, CancellationToken, Task<(byte[] Root, ulong Block)?>>? _pivotRefresher;

        internal HealRotation(
            HealSession session, HealFrontier frontier, ILogger logger, SnapSyncMetrics? metrics,
            Func<bool, CancellationToken, Task<(byte[] Root, ulong Block)?>>? pivotRefresher)
        {
            _session = session;
            _frontier = frontier;
            _logger = logger;
            _metrics = metrics;
            _pivotRefresher = pivotRefresher;
        }

        internal async Task<HealResult?> MaybeConcludeAsync(
            int processed, long nodesAddedThisRound, bool codeProgress, CancellationToken ct)
        {
            _session.RecordRoundProgress(processed, nodesAddedThisRound, codeProgress);

            bool stallTrigger = _session.StallRounds >= StallThresholdRounds;
            bool checkTrigger = _session.PivotPollSw.Elapsed >= PivotTiming.PollInterval;
            if (checkTrigger) _session.PivotPollSw.Restart();
            if ((stallTrigger || checkTrigger) && _pivotRefresher != null)
            {
                var rotated = await TryRotatePivotAsync(stallTrigger, ct).ConfigureAwait(false);
                if (rotated.HasValue) return rotated.Value;
            }

            if (stallTrigger)
            {
                _logger.LogWarning(
                    "snap.phase3.stalled at round={Round} stall={Stall} queue={Queue} — pivot refresh returned same root",
                    _session.Round, _session.StallRounds, _frontier.ToFetch.Count);
                return _session.NotConverged(_session.Stats);
            }

            return null;
        }

        private async Task<HealResult?> TryRotatePivotAsync(bool stallTrigger, CancellationToken ct)
        {
            try
            {
                var refreshed = await RefreshWithinLimitAsync(stallTrigger, ct).ConfigureAwait(false);
                var newRoot = refreshed?.Root;
                bool rootChanged = newRoot != null && newRoot.Length == 32
                    && !ByteUtil.AreEqual(newRoot, _session.LiveTargetRoot);
                if (rootChanged
                    && TrieHealer.ShouldRotateHealPivot(stallTrigger, rootChanged, _session.CurrentPivotBlock, refreshed.Value.Block, PivotTiming.StaleDistanceBlocks))
                {
                    _logger.LogWarning(
                        "Heal {Trigger} at round {Round} (stall={Stall}) — root 0x{Old} (blk {OldBlk}) stale, signalling retarget to 0x{New} (blk {NewBlk})",
                        stallTrigger ? "stalled" : "pivot-stale",
                        _session.Round, _session.StallRounds, _session.LiveTargetRoot.ToHex(), _session.CurrentPivotBlock, newRoot.ToHex(), refreshed.Value.Block);
                    _metrics?.RecordPhase3PivotRotation();
                    return _session.Retarget(_session.Stats, newRoot, refreshed.Value.Block);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Heal pivot refresh failed; continuing against current root");
            }
            return null;
        }

        private async Task<(byte[] Root, ulong Block)?> RefreshWithinLimitAsync(bool stallTrigger, CancellationToken ct)
        {
            var refreshSw = System.Diagnostics.Stopwatch.StartNew();
            var (timedOut, refreshed) = await TimeBounded.RunAsync(
                refreshCt => _pivotRefresher(stallTrigger, refreshCt), PivotTiming.PollTimeout, ct).ConfigureAwait(false);
            if (timedOut)
                _logger.LogWarning("heal.pivot.refresh_timeout after {Ms}ms — keeping current root", refreshSw.ElapsedMilliseconds);
            if (refreshSw.ElapsedMilliseconds > 5000)
                _logger.LogWarning("heal.pivot.refresh slow: {Ms}ms", refreshSw.ElapsedMilliseconds);
            return refreshed;
        }
    }
}
