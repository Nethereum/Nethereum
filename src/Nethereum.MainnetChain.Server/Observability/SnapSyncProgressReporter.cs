using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.MainnetChain.Server.Bootstrap;

namespace Nethereum.MainnetChain.Server.Observability
{
    /// <summary>
    /// 8-second heartbeat publisher. Reads cursors
    /// from <see cref="IChainMetadataStore"/>, byte counters from the persisted
    /// <see cref="SnapSyncState"/>, peer-pool composition from
    /// <see cref="IPeerPool"/>, and canonical-source staleness from the
    /// registered <see cref="ICanonicalStateRootSource"/>. On each tick it emits:
    /// <list type="bullet">
    ///   <item><c>snap.phase1.chain</c> — chain-download progress</item>
    ///   <item><c>snap.phase2.state</c> — state-download progress</item>
    ///   <item><c>snap.phase3.heal</c> — heal progress</item>
    ///   <item><c>snap.canonical.stalled</c> — beacon-source watchdog</item>
    ///   <item><c>snap.peers.summary</c> — peer-pool composition</item>
    /// </list>
    /// Pushes the same numbers into <see cref="SnapSyncMetrics"/> so the
    /// metering surface stays in lock-step with the logged narrative.
    /// </summary>
    public sealed class SnapSyncProgressReporter : BackgroundService
    {
        public static readonly TimeSpan ReportInterval = TimeSpan.FromSeconds(8);

        public static readonly TimeSpan CanonicalStalenessThreshold = TimeSpan.FromSeconds(60);

        /// <summary>
        /// EMA smoothing factor for the per-counter rate signal. Lower α favours
        /// recent samples; 0.1 keeps ETA stable across single-tick noise.
        /// </summary>
        private const double EmaAlpha = 0.1;

        /// <summary>
        /// Estimated mainnet account-count target for ETA. Configurable via
        /// the <c>NETHEREUM_SNAP_ACCOUNT_TARGET</c> env var so AppChain
        /// followers can override.
        /// </summary>
        public ulong AccountTarget { get; set; }
            = ParseUlongEnv("NETHEREUM_SNAP_ACCOUNT_TARGET", 250_000_000UL);

        private static ulong ParseUlongEnv(string name, ulong fallback)
        {
            var raw = Environment.GetEnvironmentVariable(name);
            return ulong.TryParse(raw, out var v) ? v : fallback;
        }

        /// <summary>
        /// Exact fraction (percent) of the 256-bit account-hash keyspace the range cursors have swept.
        /// The tasks partition the space into contiguous chunks; each contributes the portion from its
        /// chunk start up to its <see cref="SnapSyncAccountTask.Next"/> cursor. Because every key slot is
        /// walked this is precise, not an estimate against a guessed account total.
        /// </summary>
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
                chunkStart = ToBig(t.Last) + 1; // next chunk begins just above this one's end
            }

            var total = System.Numerics.BigInteger.One << 256;
            return (double)done / (double)total * 100.0;
        }

        private static System.Numerics.BigInteger ToBig(byte[] hash)
            => hash == null || hash.Length == 0
                ? System.Numerics.BigInteger.Zero
                : new System.Numerics.BigInteger(hash, isUnsigned: true, isBigEndian: true);

        private double _accountsEmaPerSec;
        private double _slotsEmaPerSec;
        private double _nodesEmaPerSec;
        private ulong _lastAccountsSynced;
        private ulong _lastSlotsSynced;
        private ulong _lastNodesHealed;
        private DateTimeOffset _lastTickAt;

        private readonly IChainStoreBundle _bundle;
        private readonly SnapSyncMetrics _metrics;
        private readonly ILogger<SnapSyncProgressReporter> _logger;
        private readonly IPeerPool? _peerPool;
        private readonly ICanonicalStateRootSource? _canonical;

        public SnapSyncProgressReporter(
            IChainStoreBundle bundle,
            SnapSyncMetrics metrics,
            ILogger<SnapSyncProgressReporter>? logger = null,
            IPeerPool? peerPool = null,
            ICanonicalStateRootSource? canonical = null)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _logger = logger ?? NullLogger<SnapSyncProgressReporter>.Instance;
            _peerPool = peerPool;
            _canonical = canonical;
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
            EmitCanonicalStaleness();
            await EmitPhaseAsync(ct).ConfigureAwait(false);
        }

        private void EmitPeerSummary()
        {
            if (_peerPool == null) return;

            var snapshot = _peerPool.ActivePeers;
            long total = snapshot.Count;
            long snapCapable = 0;
            ulong latest = 0;
            foreach (var p in snapshot)
            {
                if (p is MainnetPeerSession mp)
                {
                    if (mp.SupportsSnap) snapCapable++;
                    if (mp.PeerLatestBlock > latest) latest = mp.PeerLatestBlock;
                }
            }

            _metrics.SetPeerCounts(total, snapCapable);

            _logger.LogInformation(
                "snap.peers.summary total={Total} snap_capable={SnapCapable} latest={Latest}",
                total, snapCapable, latest);
        }

        private void EmitCanonicalStaleness()
        {
            var lc = FindLightClient(_canonical);
            if (lc == null) return;

            var lastSeen = lc.LastSuccessfulTipAt;
            if (lastSeen == DateTimeOffset.MinValue) return;

            var staleness = DateTimeOffset.UtcNow - lastSeen;
            if (staleness > CanonicalStalenessThreshold)
            {
                _logger.LogWarning(
                    "snap.canonical.stalled source={Source} staleness_sec={StalenessSec}",
                    lc.Name, (long)staleness.TotalSeconds);
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
            var lastReceipts = _bundle.Metadata.GetReceiptBackfillCursor();

            HeaderSyncState headerState;
            try { headerState = _bundle.Metadata.GetHeaderSyncState(); }
            catch { headerState = HeaderSyncState.Empty; }
            var headerTip = HeaderSubchains.TrustedTip(headerState);

            // Chain-download (Phase 1). Independent of snap-state — even a vanilla
            // resume into an existing archive walks the same cursors. Suppress until
            // something has moved past genesis to avoid noise on a brand-new node.
            if (lastHeader > 0 || lastBody > 0 || lastReceipts > 0 || headerTip > 0)
            {
                if (headerState.Subchains.Count > 0)
                {
                    // Header progress as identifiable [Tail..Head] segments so a tip catch-up shows the
                    // open gap alongside the linked [0..oldTip] run; bodies/receipts track toward the tip.
                    var segs = new System.Text.StringBuilder();
                    for (int i = 0; i < headerState.Subchains.Count; i++)
                    {
                        if (i > 0) segs.Append(',');
                        var seg = headerState.Subchains[i];
                        segs.Append(seg.Tail).Append("..").Append(seg.Head);
                    }
                    _logger.LogInformation(
                        "snap.phase1 tip={Tip} headers=[{Segments}] bodies={Bodies} receipts={Receipts} executed={LastBlock}",
                        headerTip, segs.ToString(), lastBody, lastReceipts, lastBlock);
                }
                else
                {
                    _logger.LogInformation(
                        "snap.phase1.chain headers={Headers} bodies={Bodies} receipts={Receipts} last_block={LastBlock}",
                        lastHeader, lastBody, lastReceipts, lastBlock);
                }
            }

            if (state == null) return;

            _metrics.SetPhase(state.Phase);

            var now = DateTimeOffset.UtcNow;
            double tickSeconds = _lastTickAt == default ? 0 : (now - _lastTickAt).TotalSeconds;
            _lastTickAt = now;

            switch (state.Phase)
            {
                case SnapPhase.Phase2Running:
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

                    var etaSec = _accountsEmaPerSec > 0 && AccountTarget > counters.AccountsSynced
                        ? (long)((AccountTarget - counters.AccountsSynced) / _accountsEmaPerSec)
                        : -1L;
                    // Precise progress: the fraction of the 256-bit account-hash keyspace the 16-way
                    // range cursors have actually swept. We walk every key slot, so this is exact — not
                    // an estimate against a guessed account total. Accounts are keccak-uniform, so it also
                    // tracks the fraction of accounts synced.
                    var keyspacePct = HashSpaceProgressPercent(state.Tasks);

                    _logger.LogInformation(
                        "snap.phase2.state accounts={Accounts} keyspace={Pct:F3}% account_bytes={AccountBytes} " +
                        "slots={Slots} slot_bytes={SlotBytes} codes={Codes} code_bytes={CodeBytes} " +
                        "pivot={Pivot} accts_rate={AcctRate:F0}/s slot_rate={SlotRate:F0}/s eta_sec={EtaSec}",
                        counters.AccountsSynced, keyspacePct, counters.AccountBytes,
                        counters.StorageSlotsSynced, counters.StorageBytes,
                        counters.BytecodesSynced, counters.BytecodeBytes,
                        state.PivotBlockNumber,
                        _accountsEmaPerSec, _slotsEmaPerSec, etaSec);
                    break;
                }
                case SnapPhase.Phase3Running:
                {
                    var counters = state.Counters ?? SnapSyncCounters.Zero;
                    var healTargetHex = state.HealTargetRoot != null && state.HealTargetRoot.Length == 32
                        ? state.HealTargetRoot.ToHex()
                        : "<none>";

                    if (tickSeconds > 0)
                    {
                        var nodeRate = (counters.TrieNodesHealed - _lastNodesHealed) / tickSeconds;
                        _nodesEmaPerSec = EmaAlpha * nodeRate + (1 - EmaAlpha) * _nodesEmaPerSec;
                    }
                    _lastNodesHealed = counters.TrieNodesHealed;

                    _logger.LogInformation(
                        "snap.phase3.heal nodes_healed={NodesHealed} bytecodes_healed={BytecodesHealed} " +
                        "queue_depth={QueueDepth} target_root=0x{TargetRoot} pivot={Pivot} nodes_rate={NodeRate:F0}/s",
                        counters.TrieNodesHealed, counters.BytecodesHealed,
                        _metrics.Phase3QueueDepth, healTargetHex, state.PivotBlockNumber,
                        _nodesEmaPerSec);
                    break;
                }
                case SnapPhase.Complete:
                {
                    _logger.LogInformation(
                        "snap.phase4.handoff pivot={Pivot} last_block={LastBlock}",
                        state.PivotBlockNumber, lastBlock);
                    break;
                }
                case SnapPhase.NotStarted:
                default:
                    break;
            }

            await Task.CompletedTask.ConfigureAwait(false);
            _ = ct;
        }
    }
}
