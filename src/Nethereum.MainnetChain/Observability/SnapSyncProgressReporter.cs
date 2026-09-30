using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Common;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.MainnetChain.Bootstrap;
using Nethereum.DevP2P.Sync.Peering;

namespace Nethereum.MainnetChain.Observability
{
    public enum SnapStage
    {
        Headers,
        Bodies,
        State,
        Heal,
        CatchUp,
    }

    public readonly record struct SnapStageDerivation(SnapStage Stage, bool ShouldArmStateStall, bool Phase2Reached);

    public sealed class SnapSyncProgressReporter : BackgroundService
    {
        public static readonly TimeSpan ReportInterval = TimeSpan.FromSeconds(8);

        public static readonly TimeSpan CanonicalStalenessThreshold = TimeSpan.FromSeconds(60);

        private const double EmaAlpha = 0.1;

        public ulong AccountTarget { get; set; }
            = ParseUlongEnv("NETHEREUM_SNAP_ACCOUNT_TARGET", 250_000_000UL);

        private static ulong ParseUlongEnv(string name, ulong fallback)
        {
            var raw = Environment.GetEnvironmentVariable(name);
            return ulong.TryParse(raw, out var v) ? v : fallback;
        }

        private static double HashSpaceProgressPercent(IReadOnlyList<SnapSyncAccountTask> tasks)
        {
            if (tasks == null || tasks.Count == 0) return 0.0;

            var sorted = new List<SnapSyncAccountTask>(tasks);
            sorted.Sort((a, b) => ToBig(a.Last).CompareTo(ToBig(b.Last)));

            System.Numerics.BigInteger done = 0;
            System.Numerics.BigInteger chunkStart = 0;
            foreach (var t in sorted)
            {
                var next = ToBig(t.Next);
                if (next > chunkStart) done += next - chunkStart;
                chunkStart = ToBig(t.Last) + 1;
            }

            var total = System.Numerics.BigInteger.One << 256;
            return (double)done / (double)total * 100.0;
        }

        private static System.Numerics.BigInteger ToBig(byte[] hash)
            => hash == null || hash.Length == 0
                ? System.Numerics.BigInteger.Zero
                : new System.Numerics.BigInteger(hash, isUnsigned: true, isBigEndian: true);

        public static SnapStageDerivation DeriveStage(
            SnapSyncState? state,
            ulong headerTip,
            HeaderSyncState headerState,
            ulong lastBody,
            bool phase2Reached)
        {
            var headersToGenesis = headerTip > 0
                && headerState.Subchains.Count == 1
                && headerState.Subchains[0].Tail == 0;

            var pivot = state?.PivotBlockNumber ?? 0;
            var phase1CompleteNow = headersToGenesis && pivot > 0 && lastBody >= pivot;

            var accountsMoving = ((state?.Counters?.AccountsSynced ?? 0) + (state?.Counters?.StorageSlotsSynced ?? 0)) > 0;

            var newReached = phase2Reached
                || (state?.Phase == SnapPhase.Phase2Running && (accountsMoving || phase1CompleteNow));

            var stage = state?.Phase switch
            {
                SnapPhase.Complete => SnapStage.CatchUp,
                SnapPhase.Phase3Running => SnapStage.Heal,
                SnapPhase.Phase2Running when newReached => SnapStage.State,
                _ => headersToGenesis ? SnapStage.Bodies : SnapStage.Headers,
            };

            return new SnapStageDerivation(stage, stage == SnapStage.State, newReached);
        }

        public static string FmtBytes(ulong bytes)
        {
            const double KiB = 1024.0;
            const double MiB = KiB * 1024.0;
            const double GiB = MiB * 1024.0;

            if (bytes >= GiB) return (bytes / GiB).ToString("0.##", CultureInfo.InvariantCulture) + "GiB";
            if (bytes >= MiB) return (bytes / MiB).ToString("0.##", CultureInfo.InvariantCulture) + "MiB";
            if (bytes >= KiB) return (bytes / KiB).ToString("0.##", CultureInfo.InvariantCulture) + "KiB";
            return bytes.ToString("N0", CultureInfo.InvariantCulture) + "B";
        }

        public static string FmtEta(long seconds)
        {
            if (seconds < 0) return "?";

            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h{ts.Minutes:D2}m";
            return $"{ts.Minutes}m{ts.Seconds:D2}s";
        }

        public static string FmtPct(double pct)
            => pct < 0 ? "n/a" : pct.ToString("F2", CultureInfo.InvariantCulture) + "%";

        public static (ulong downloaded, ulong left) HeaderProgress(ulong headerTip, ulong lastHeader)
        {
            if (lastHeader == 0) return (0, headerTip);
            if (lastHeader <= headerTip) return (headerTip - lastHeader, lastHeader);
            return (0, headerTip);
        }

        private double _accountsEmaPerSec;
        private double _slotsEmaPerSec;
        private double _nodesEmaPerSec;
        private ulong _lastAccountsSynced;
        private ulong _lastSlotsSynced;
        private long _lastNodesHealed;
        private DateTimeOffset _lastTickAt;

        private double _headerEmaPerSec;
        private ulong _lastHeaderCursorForRate;
        private double _bodyEmaPerSec;
        private ulong _lastBodyCursorForRate;
        private double _catchupEmaPerSec;
        private ulong _lastCatchupBlock;

        private bool _phase2Reached;

        private static readonly TimeSpan StallThreshold = TimeSpan.FromMinutes(10);
        private readonly ProgressStallDetector _tipStall = new(StallThreshold);
        private readonly ProgressStallDetector _acctStall = new(StallThreshold);

        private readonly IChainStoreBundle _bundle;
        private readonly SnapSyncMetrics _metrics;
        private readonly ILogger<SnapSyncProgressReporter> _logger;
        private readonly IPeerPool? _peerPool;
        private readonly ICanonicalStateRootSource? _canonical;
        private readonly IFetchRequestScheduler? _scheduler;
        private readonly TimeProvider _clock;

        public SnapSyncProgressReporter(
            IChainStoreBundle bundle,
            SnapSyncMetrics metrics,
            ILogger<SnapSyncProgressReporter>? logger = null,
            IPeerPool? peerPool = null,
            ICanonicalStateRootSource? canonical = null,
            IFetchRequestScheduler? scheduler = null,
            TimeProvider? clock = null)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _logger = logger ?? NullLogger<SnapSyncProgressReporter>.Instance;
            _peerPool = peerPool;
            _canonical = canonical;
            _scheduler = scheduler;
            _clock = clock ?? TimeProvider.System;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(ReportInterval);
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                {
                    try
                    {
                        await EmitReportAsync(stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "snap.reporter.tick_failed");
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }

        public async Task EmitReportAsync(CancellationToken ct)
        {
            EmitPeerSummary();
            await EmitCanonicalStalenessAsync(ct).ConfigureAwait(false);
            await EmitPhaseAsync(ct).ConfigureAwait(false);
        }

        private void EmitPeerSummary()
        {
            if (_peerPool == null) return;

            var snapshot = _peerPool.ActivePeers;
            long total = snapshot.Count;
            long snapCapable = 0;
            long snapServing = 0;
            ulong latest = 0;
            foreach (var p in snapshot)
            {
                if (p is SyncPeerSession mp)
                {
                    if (mp.SupportsSnap)
                    {
                        snapCapable++;
                        if (_scheduler == null || _scheduler.IsSnapStateServing(mp)) snapServing++;
                    }
                    if (mp.PeerLatestBlock > latest) latest = mp.PeerLatestBlock;
                }
            }
            long snapQuarantined = snapCapable - snapServing;

            _metrics.SetPeerCounts(total, snapCapable);

            _logger.LogInformation(
                "snap.peers.summary total={Total} snap_capable={SnapCapable} serving={Serving} quarantined={Quarantined} latest={Latest}",
                total, snapCapable, snapServing, snapQuarantined, latest);
        }

        private async Task EmitCanonicalStalenessAsync(CancellationToken ct)
        {
            var lc = FindLightClient(_canonical);
            if (lc == null) return;

            var now = _clock.GetUtcNow();

            var lastSeen = lc.LastSuccessfulTipAt;
            if (lastSeen != DateTimeOffset.MinValue)
            {
                var staleness = now - lastSeen;
                if (staleness > CanonicalStalenessThreshold)
                    _logger.LogWarning(
                        "snap.canonical.stalled source={Source} staleness_sec={StalenessSec}",
                        lc.Name, (long)staleness.TotalSeconds);
            }

            CanonicalTip tip = null;
            try { tip = await _canonical.GetLatestAsync(ct).ConfigureAwait(false); }
            catch { }
            if (tip == null) return;

            if (_tipStall.Observe((ulong)tip.BlockNumber, now))
            {
                _logger.LogError(
                    "snap.canonical.frozen source={Source} tip_block={Block} frozen_sec={FrozenSec} — " +
                    "canonical tip is NOT advancing; the snap pivot cannot roll. Light client is likely stuck " +
                    "(e.g. a sync-committee period boundary). Restart/recover the canonical source.",
                    lc.Name, tip.BlockNumber, (long)_tipStall.StalledFor(now).TotalSeconds);
            }
        }

        private static LightClientCanonicalSource? FindLightClient(ICanonicalStateRootSource? source)
        {
            if (source is LightClientCanonicalSource direct) return direct;
            if (source is CompositeCanonicalStateRootSource composite)
            {
                foreach (var inner in composite.Sources)
                {
                    if (inner is LightClientCanonicalSource match) return match;
                }
            }
            return null;
        }

        private async Task EmitPhaseAsync(CancellationToken ct)
        {
            SnapSyncState? state;
            try
            {
                state = _bundle.Metadata.GetSnapSyncState();
            }
            catch
            {
                state = null;
            }

            var lastBlock = _bundle.Metadata.GetLastBlock();
            var lastHeader = _bundle.Metadata.GetLastFetchedHeader();
            var lastBody = _bundle.Metadata.GetLastFetchedBody();

            var frozenHead = _bundle.FreezerHead;
            var lastReceipts = frozenHead > 0 ? (ulong)frozenHead : _bundle.Metadata.GetReceiptBackfillCursor();

            HeaderSyncState headerState;
            try { headerState = _bundle.Metadata.GetHeaderSyncState(); }
            catch { headerState = HeaderSyncState.Empty; }
            var headerTip = HeaderSubchains.TrustedTip(headerState);

            var now = _clock.GetUtcNow();
            double tickSeconds = _lastTickAt == default ? 0 : (now - _lastTickAt).TotalSeconds;
            _lastTickAt = now;

            var headerEtaSec = AdvanceHeaderDescentRate(lastHeader, tickSeconds);
            var bodyEtaSec = headerTip > 0 ? AdvanceBodyRate(lastBody, headerTip, tickSeconds) : -1L;

            if (state != null) _metrics.SetPhase(state.Phase);

            var derivation = DeriveStage(state, headerTip, headerState, lastBody, _phase2Reached);
            _phase2Reached = derivation.Phase2Reached;

            switch (state?.Phase)
            {
                case SnapPhase.Phase2Running:
                    EmitPhase2Progress(state!, tickSeconds, now, derivation.Stage == SnapStage.State);
                    break;
                case SnapPhase.Phase3Running:
                    EmitPhase3Progress(state!, tickSeconds);
                    break;
                case SnapPhase.Complete:
                    EmitCompleteProgress(lastBlock, headerTip, tickSeconds);
                    break;
            }

            switch (derivation.Stage)
            {
                case SnapStage.Headers:
                    EmitHeadersLine(headerTip, lastHeader, headerEtaSec);
                    break;
                case SnapStage.Bodies:
                    EmitBodiesLine(
                        state?.PivotBlockNumber ?? 0, lastBody, lastReceipts,
                        frozenHead, _bundle.ByHashIndexedHead, _bundle.LogIndexRenderedHead, _bundle.LogRenderProgressBlock,
                        bodyEtaSec);
                    break;
            }

            await Task.CompletedTask.ConfigureAwait(false);
            _ = ct;
        }

        private long AdvanceHeaderDescentRate(ulong lastHeader, double tickSeconds)
        {
            if (tickSeconds > 0 && lastHeader <= _lastHeaderCursorForRate)
            {
                var headerRate = (_lastHeaderCursorForRate - lastHeader) / tickSeconds;
                _headerEmaPerSec = EmaAlpha * headerRate + (1 - EmaAlpha) * _headerEmaPerSec;
            }
            _lastHeaderCursorForRate = lastHeader;

            return _headerEmaPerSec > 0 && lastHeader > 0
                ? (long)(lastHeader / _headerEmaPerSec)
                : -1L;
        }

        private long AdvanceBodyRate(ulong lastBody, ulong headerTip, double tickSeconds)
        {
            if (tickSeconds > 0 && lastBody >= _lastBodyCursorForRate)
            {
                var bodyRate = (lastBody - _lastBodyCursorForRate) / tickSeconds;
                _bodyEmaPerSec = EmaAlpha * bodyRate + (1 - EmaAlpha) * _bodyEmaPerSec;
            }
            _lastBodyCursorForRate = lastBody;

            _metrics.SetBodyProgress((long)lastBody, (long)headerTip);

            return _bodyEmaPerSec > 0 && headerTip > lastBody
                ? (long)((headerTip - lastBody) / _bodyEmaPerSec)
                : -1L;
        }

        private void EmitPhase2Progress(SnapSyncState state, double tickSeconds, DateTimeOffset now, bool isActiveStage)
        {
            var counters = state.Counters ?? SnapSyncCounters.Zero;
            if (tickSeconds > 0)
            {
                var acctRate = (counters.AccountsSynced - _lastAccountsSynced) / tickSeconds;
                var slotRate = (counters.StorageSlotsSynced - _lastSlotsSynced) / tickSeconds;
                _accountsEmaPerSec = EmaAlpha * acctRate + (1 - EmaAlpha) * _accountsEmaPerSec;
                _slotsEmaPerSec = EmaAlpha * slotRate + (1 - EmaAlpha) * _slotsEmaPerSec;
            }
            _lastAccountsSynced = counters.AccountsSynced;
            _lastSlotsSynced = counters.StorageSlotsSynced;

            var keyspacePct = HashSpaceProgressPercent(state.Tasks);
            _metrics.SetKeyspacePercent(keyspacePct);

            if (!isActiveStage) return;

            var backpressure = StateWriteBackpressure();
            if (backpressure != null)
                _logger.LogInformation(
                    "Syncing: state download paused — storage backpressure: {Pressure}", backpressure);

            if (_acctStall.Observe(counters.AccountsSynced + counters.StorageSlotsSynced, now))
                LogStateNotAdvancing(counters, (long)_acctStall.StalledFor(now).TotalSeconds, backpressure);

            var etaSec = _accountsEmaPerSec > 0 && AccountTarget > counters.AccountsSynced
                ? (long)((AccountTarget - counters.AccountsSynced) / _accountsEmaPerSec)
                : -1L;
            var stateBytes = counters.AccountBytes + counters.StorageBytes + counters.BytecodeBytes;

            _logger.LogInformation(
                "Syncing: state download in progress      synced={Synced} state={State} accounts={Accounts:N0}@{AccountBytes} slots={Slots:N0}@{SlotBytes} codes={Codes:N0}@{CodeBytes} eta={Eta}",
                FmtPct(keyspacePct), FmtBytes(stateBytes),
                counters.AccountsSynced, FmtBytes(counters.AccountBytes),
                counters.StorageSlotsSynced, FmtBytes(counters.StorageBytes),
                counters.BytecodesSynced, FmtBytes(counters.BytecodeBytes),
                FmtEta(etaSec));
        }

        private void LogStateNotAdvancing(SnapSyncCounters counters, long frozenSec, string? backpressure)
        {
            if (backpressure != null)
                _logger.LogWarning(
                    "snap.state.paused accounts={Accounts} slots={Slots} frozen_sec={FrozenSec} — Phase-2 " +
                    "state download is paused by storage backpressure, not by peers: {Pressure}. It resumes " +
                    "when the store's write valve releases.",
                    counters.AccountsSynced, counters.StorageSlotsSynced, frozenSec, backpressure);
            else
                _logger.LogError(
                    "snap.state.stalled accounts={Accounts} slots={Slots} frozen_sec={FrozenSec} — Phase-2 " +
                    "state sync is NOT advancing; no peer is serving the current pivot root. Roll the pivot " +
                    "to the latest canonical tip or recover serving peers.",
                    counters.AccountsSynced, counters.StorageSlotsSynced, frozenSec);
        }

        private string? StateWriteBackpressure()
            => _bundle is IStateWriteBackpressure valve && valve.ShouldPauseStateWrites()
                ? valve.DescribeStateBackpressure()
                : null;

        private void EmitPhase3Progress(SnapSyncState state, double tickSeconds)
        {
            var healTargetHex = state.HealTargetRoot != null && state.HealTargetRoot.Length == 32
                ? state.HealTargetRoot.ToHex()
                : "<none>";

            var nodesHealed = _metrics.Phase3NodesHealedTotal;
            var bytecodesHealed = _metrics.Phase3BytecodesHealedTotal;

            if (tickSeconds > 0)
            {
                var nodeRate = (nodesHealed - _lastNodesHealed) / tickSeconds;
                _nodesEmaPerSec = EmaAlpha * nodeRate + (1 - EmaAlpha) * _nodesEmaPerSec;
            }
            _lastNodesHealed = nodesHealed;

            _logger.LogInformation(
                "State heal in progress                   nodes={Nodes:N0} bytecodes={Bytecodes:N0} queue={Queue:N0} target=0x{Target} rate={Rate:F0}/s",
                nodesHealed, bytecodesHealed, _metrics.Phase3QueueDepth, healTargetHex, _nodesEmaPerSec);
        }

        private void EmitCompleteProgress(ulong lastBlock, ulong headerTip, double tickSeconds)
        {
            var behind = headerTip > lastBlock ? headerTip - lastBlock : 0;
            if (tickSeconds > 0 && lastBlock >= _lastCatchupBlock)
            {
                var catchupRate = (lastBlock - _lastCatchupBlock) / tickSeconds;
                _catchupEmaPerSec = EmaAlpha * catchupRate + (1 - EmaAlpha) * _catchupEmaPerSec;
            }
            _lastCatchupBlock = lastBlock;
            var etaSec = _catchupEmaPerSec > 0 && behind > 0
                ? (long)(behind / _catchupEmaPerSec)
                : -1L;

            _metrics.SetCatchup((long)lastBlock, (long)behind);

            _logger.LogInformation(
                "Syncing: block execution catch-up        synced={Synced} executed={Executed:N0} tip={Tip:N0} behind={Behind:N0} rate={Rate:F1} blk/s eta={Eta}",
                FmtPct(ProgressPercent(lastBlock, headerTip)), lastBlock, headerTip, behind, _catchupEmaPerSec, FmtEta(etaSec));
        }

        private void EmitHeadersLine(ulong headerTip, ulong lastHeader, long etaSec)
        {
            var (downloaded, left) = HeaderProgress(headerTip, lastHeader);
            _logger.LogInformation(
                "Syncing beacon headers                   downloaded={Downloaded:N0} left={Left:N0} tip={Tip:N0} eta={Eta}",
                downloaded, left, headerTip, FmtEta(etaSec));
        }

        private void EmitBodiesLine(
            ulong pivot, ulong lastBody, ulong lastReceipts,
            long frozenHead, long byHashIndexedHead, long logIndexRenderedHead, long logRenderProgressBlock,
            long etaSec)
        {
            _logger.LogInformation(
                "Syncing: chain download in progress      synced={Synced} bodies={Bodies:N0} receipts={Receipts:N0} frozen={Frozen:N0} txn_idx={TxnIdx:N0} log_idx={LogIdx:N0} log_lv={LogLv:N0} eta={Eta}",
                FmtPct(ProgressPercent(lastBody, pivot)), lastBody, lastReceipts,
                frozenHead, byHashIndexedHead, logIndexRenderedHead, logRenderProgressBlock, FmtEta(etaSec));
        }

        public static double ProgressPercent(ulong done, ulong total)
            => total == 0 ? -1.0 : Math.Min(100.0, (double)done / total * 100.0);
    }
}
