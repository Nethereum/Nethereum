using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.MainnetChain.Observability;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;

namespace Nethereum.MainnetChain.Server.IntegrationTests;

public class SnapSyncProgressReporterTests
{
    private const string StateLine = "Syncing: state download in progress";
    private const string HealLine = "State heal in progress";
    private const string CatchUpLine = "Syncing: block execution catch-up";
    private const string HeadersLine = "Syncing beacon headers";
    private const string BodiesLine = "Syncing: chain download in progress";
    private const string PausedLine = "Syncing: state download paused — storage backpressure: ";
    private const string ValvePressure = "state WRITE-STOP: rocksdb.is-write-stopped=1 (test)";

    [Fact]
    public async Task Given_NoSnapState_When_Reported_Then_OnlyThePeerSummaryAndHeadersLinesAreEmitted()
    {
        using var bundle = InMemoryChainStoreBundle.Open();
        using var metrics = new SnapSyncMetrics("test");
        var pool = new EmptyFakePeerPool();
        var logger = new CapturingLogger();

        var reporter = new SnapSyncProgressReporter(bundle, metrics, logger, pool, canonical: null);
        await reporter.EmitReportAsync(CancellationToken.None);

        Assert.Contains(logger.Messages, m => m.Contains("snap.peers.summary total=0 snap_capable=0"));
        Assert.Contains(logger.Messages, m => m.Contains(HeadersLine));
        Assert.DoesNotContain(logger.Messages, m => m.Contains(BodiesLine));
        Assert.DoesNotContain(logger.Messages, m => m.Contains(StateLine));
        Assert.DoesNotContain(logger.Messages, m => m.Contains(HealLine));
        Assert.DoesNotContain(logger.Messages, m => m.Contains(CatchUpLine));
    }

    [Fact]
    public async Task Given_Phase2RunningWithAccountsMoving_When_Reported_Then_TheStateDownloadLineShowsTheCountersAndNoOtherStageLine()
    {
        using var bundle = InMemoryChainStoreBundle.Open();
        bundle.Metadata.SaveSnapSyncState(new SnapSyncState
        {
            SchemaVersion = 1,
            Phase = SnapPhase.Phase2Running,
            PivotBlockNumber = 25_000_000,
            PivotBlockHash = new byte[32],
            HealTargetRoot = new byte[32],
            Tasks = new List<SnapSyncAccountTask>(),
            Counters = new SnapSyncCounters
            {
                AccountsSynced = 1_234,
                AccountBytes = 56_789,
                StorageSlotsSynced = 9_999,
                StorageBytes = 1_234_567,
                BytecodesSynced = 42,
                BytecodeBytes = 1_024,
                TrieNodesHealed = 0,
                TrieNodeBytesHealed = 0,
                BytecodesHealed = 0,
            },
        });

        using var metrics = new SnapSyncMetrics("test");
        var logger = new CapturingLogger();

        var reporter = new SnapSyncProgressReporter(bundle, metrics, logger, peerPool: null, canonical: null);
        await reporter.EmitReportAsync(CancellationToken.None);

        var stateLine = Assert.Single(logger.Messages, m => m.Contains(StateLine));
        Assert.Contains("synced=0.00%", stateLine);
        Assert.Contains("state=1.23MiB", stateLine);
        Assert.Contains("accounts=1,234@55.46KiB", stateLine);
        Assert.Contains("slots=9,999@1.18MiB", stateLine);
        Assert.Contains("codes=42@1KiB", stateLine);
        Assert.Contains("eta=?", stateLine);
        Assert.DoesNotContain(logger.Messages, m => m.Contains(HeadersLine));
        Assert.DoesNotContain(logger.Messages, m => m.Contains(BodiesLine));
        Assert.DoesNotContain(logger.Messages, m => m.Contains(HealLine));
        Assert.DoesNotContain(logger.Messages, m => m.Contains(CatchUpLine));
    }

    [Fact]
    public async Task Given_SnapComplete_When_Reported_Then_TheCatchUpLineShowsExecutedTipAndBehindAndNoOtherStageLine()
    {
        using var bundle = InMemoryChainStoreBundle.Open();
        bundle.Metadata.SaveHeaderSyncState(new HeaderSyncState
        {
            SchemaVersion = 1,
            Subchains = new List<HeaderSubchain> { new HeaderSubchain { Head = 100, Tail = 0, Next = 0 } },
        });
        bundle.Metadata.Commit(40, new byte[32]);
        bundle.Metadata.SaveSnapSyncState(new SnapSyncState
        {
            SchemaVersion = 1,
            Phase = SnapPhase.Complete,
            PivotBlockNumber = 25_000_000,
            PivotBlockHash = new byte[32],
            HealTargetRoot = new byte[32],
            Tasks = new List<SnapSyncAccountTask>(),
            Counters = SnapSyncCounters.Zero,
        });

        using var metrics = new SnapSyncMetrics("test");
        var logger = new CapturingLogger();

        var reporter = new SnapSyncProgressReporter(bundle, metrics, logger, peerPool: null, canonical: null);
        await reporter.EmitReportAsync(CancellationToken.None);

        var catchUpLine = Assert.Single(logger.Messages, m => m.Contains(CatchUpLine));
        Assert.Contains("synced=40.00%", catchUpLine);
        Assert.Contains("executed=40", catchUpLine);
        Assert.Contains("tip=100", catchUpLine);
        Assert.Contains("behind=60", catchUpLine);
        Assert.DoesNotContain(logger.Messages, m => m.Contains(HeadersLine));
        Assert.DoesNotContain(logger.Messages, m => m.Contains(BodiesLine));
        Assert.DoesNotContain(logger.Messages, m => m.Contains(StateLine));
        Assert.DoesNotContain(logger.Messages, m => m.Contains(HealLine));
    }

    [Fact]
    public async Task Given_Phase2StateStageAndTheStoreWriteValveClosed_When_Reported_Then_TheHeartbeatSaysTheStateDownloadIsPausedForStorageBackpressure()
    {
        using var inner = InMemoryChainStoreBundle.Open();
        SaveMovingPhase2State(inner);
        using var bundle = new BackpressuredBundle(inner) { Pressure = ValvePressure };
        using var metrics = new SnapSyncMetrics("test");
        var logger = new CapturingLogger();

        var reporter = new SnapSyncProgressReporter(bundle, metrics, logger, peerPool: null, canonical: null);
        await reporter.EmitReportAsync(CancellationToken.None);

        var paused = Assert.Single(logger.Entries, e => e.Message.Contains(PausedLine));
        Assert.Equal(LogLevel.Information, paused.Level);
        Assert.EndsWith(ValvePressure, paused.Message);
        Assert.Single(logger.Messages, m => m.Contains(StateLine));
    }

    [Fact]
    public async Task Given_Phase2StateStageAndTheStoreWriteValveOpen_When_Reported_Then_NoPausedLineIsEmitted()
    {
        using var inner = InMemoryChainStoreBundle.Open();
        SaveMovingPhase2State(inner);
        using var bundle = new BackpressuredBundle(inner) { Pressure = null };
        using var metrics = new SnapSyncMetrics("test");
        var logger = new CapturingLogger();

        var reporter = new SnapSyncProgressReporter(bundle, metrics, logger, peerPool: null, canonical: null);
        await reporter.EmitReportAsync(CancellationToken.None);

        Assert.DoesNotContain(logger.Messages, m => m.Contains(PausedLine));
        Assert.Single(logger.Messages, m => m.Contains(StateLine));
    }

    [Fact]
    public async Task Given_Phase2CountersFrozenPastTheStallThresholdWhileTheWriteValveIsClosed_When_Reported_Then_ItWarnsThatStorageBackpressurePausedTheDownloadAndDoesNotBlamePeers()
    {
        using var inner = InMemoryChainStoreBundle.Open();
        SaveMovingPhase2State(inner);
        using var bundle = new BackpressuredBundle(inner) { Pressure = ValvePressure };
        using var metrics = new SnapSyncMetrics("test");
        var logger = new CapturingLogger();
        var clock = new ManualClock(new DateTimeOffset(2026, 9, 29, 0, 0, 0, TimeSpan.Zero));

        var reporter = new SnapSyncProgressReporter(bundle, metrics, logger, peerPool: null, canonical: null, clock: clock);
        await reporter.EmitReportAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(11));
        await reporter.EmitReportAsync(CancellationToken.None);

        var warning = Assert.Single(logger.Entries, e => e.Message.Contains("snap.state.paused"));
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("frozen_sec=660", warning.Message);
        Assert.Contains(ValvePressure, warning.Message);
        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Error);
        Assert.DoesNotContain(logger.Messages, m => m.Contains("snap.state.stalled") || m.Contains("no peer is serving"));
    }

    [Fact]
    public async Task Given_Phase2CountersFrozenPastTheStallThresholdWithTheWriteValveOpen_When_Reported_Then_ItStillReportsTheStallAtError()
    {
        using var inner = InMemoryChainStoreBundle.Open();
        SaveMovingPhase2State(inner);
        using var bundle = new BackpressuredBundle(inner) { Pressure = null };
        using var metrics = new SnapSyncMetrics("test");
        var logger = new CapturingLogger();
        var clock = new ManualClock(new DateTimeOffset(2026, 9, 29, 0, 0, 0, TimeSpan.Zero));

        var reporter = new SnapSyncProgressReporter(bundle, metrics, logger, peerPool: null, canonical: null, clock: clock);
        await reporter.EmitReportAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(11));
        await reporter.EmitReportAsync(CancellationToken.None);

        var stalled = Assert.Single(logger.Entries, e => e.Message.Contains("snap.state.stalled"));
        Assert.Equal(LogLevel.Error, stalled.Level);
        Assert.DoesNotContain(logger.Messages, m => m.Contains("snap.state.paused"));
    }

    private static void SaveMovingPhase2State(InMemoryChainStoreBundle bundle)
        => bundle.Metadata.SaveSnapSyncState(new SnapSyncState
        {
            SchemaVersion = 1,
            Phase = SnapPhase.Phase2Running,
            PivotBlockNumber = 25_000_000,
            PivotBlockHash = new byte[32],
            HealTargetRoot = new byte[32],
            Tasks = new List<SnapSyncAccountTask>(),
            Counters = SnapSyncCounters.Zero with { AccountsSynced = 1_234, StorageSlotsSynced = 9_999 },
        });

    [Theory]
    [InlineData(0UL, 0UL, -1.0)]
    [InlineData(50UL, 100UL, 50.0)]
    [InlineData(150UL, 100UL, 100.0)]
    public void ProgressPercent_ClampsAndSignalsUnknown(ulong done, ulong total, double expected)
    {
        Assert.Equal(expected, SnapSyncProgressReporter.ProgressPercent(done, total), 3);
    }

    [Fact]
    public async Task Given_Phase3Running_When_Reported_Then_TheStateHealLineShowsHealedNodesBytecodesAndTheHealTarget()
    {
        using var bundle = InMemoryChainStoreBundle.Open();
        var healRoot = new byte[32];
        for (int i = 0; i < 32; i++) healRoot[i] = 0xAB;

        bundle.Metadata.SaveSnapSyncState(new SnapSyncState
        {
            SchemaVersion = 1,
            Phase = SnapPhase.Phase3Running,
            PivotBlockNumber = 25_000_000,
            PivotBlockHash = new byte[32],
            HealTargetRoot = healRoot,
            Tasks = new List<SnapSyncAccountTask>(),
            Counters = new SnapSyncCounters
            {
                AccountsSynced = 0,
                AccountBytes = 0,
                StorageSlotsSynced = 0,
                StorageBytes = 0,
                BytecodesSynced = 0,
                BytecodeBytes = 0,
                TrieNodesHealed = 7_654_321,
                TrieNodeBytesHealed = 9_876_543,
                BytecodesHealed = 12,
            },
        });

        using var metrics = new SnapSyncMetrics("test");
        var logger = new CapturingLogger();

        metrics.RecordPhase3NodesHealed(7_654_321);
        metrics.RecordPhase3BytecodesHealed(12);

        var reporter = new SnapSyncProgressReporter(bundle, metrics, logger, peerPool: null, canonical: null);
        await reporter.EmitReportAsync(CancellationToken.None);

        var healLine = Assert.Single(logger.Messages, m => m.Contains(HealLine));
        Assert.Contains("nodes=7,654,321", healLine);
        Assert.Contains("bytecodes=12", healLine);
        Assert.Contains("target=0x" + string.Concat(System.Linq.Enumerable.Repeat("ab", 32)), healLine);
        Assert.DoesNotContain(logger.Messages, m => m.Contains(StateLine));
        Assert.DoesNotContain(logger.Messages, m => m.Contains(CatchUpLine));
    }

    [Fact]
    public async Task Given_HeadersDescendingFromTheTrustedTip_When_Reported_Then_TheBeaconHeadersLineShowsDownloadedLeftAndTip()
    {
        using var bundle = InMemoryChainStoreBundle.Open();
        bundle.Metadata.SaveHeaderSyncState(new HeaderSyncState
        {
            SchemaVersion = 1,
            Subchains = new List<HeaderSubchain> { new HeaderSubchain { Head = 2_000_000, Tail = 1_234_567, Next = 1_234_566 } },
        });
        bundle.Metadata.SetLastFetchedHeader(1_234_567);

        using var metrics = new SnapSyncMetrics("test");
        var logger = new CapturingLogger();

        var reporter = new SnapSyncProgressReporter(bundle, metrics, logger, peerPool: null, canonical: null);
        await reporter.EmitReportAsync(CancellationToken.None);

        var headersLine = Assert.Single(logger.Messages, m => m.Contains(HeadersLine));
        Assert.Contains("downloaded=765,433", headersLine);
        Assert.Contains("left=1,234,567", headersLine);
        Assert.Contains("tip=2,000,000", headersLine);
        Assert.DoesNotContain(logger.Messages, m => m.Contains(BodiesLine));
    }

    [Fact]
    public async Task Reporter_BodiesStage_FreezerActive_ReportsReceiptsAndIndexFrontiersFromFreezerHead()
    {
        using var inner = InMemoryChainStoreBundle.Open();
        inner.Metadata.SaveHeaderSyncState(new HeaderSyncState
        {
            SchemaVersion = 1,
            Subchains = new List<HeaderSubchain> { new HeaderSubchain { Head = 100, Tail = 0, Next = 0 } },
        });
        inner.Metadata.SetLastFetchedBody(100);

        using var bundle = new FreezerFrontierBundle(inner, freezerHead: 100, byHashIndexedHead: 100, logIndexRenderedHead: 59);
        using var metrics = new SnapSyncMetrics("test");
        var logger = new CapturingLogger();

        var reporter = new SnapSyncProgressReporter(bundle, metrics, logger, peerPool: null, canonical: null);
        await reporter.EmitReportAsync(CancellationToken.None);

        Assert.Equal(0UL, inner.Metadata.GetReceiptBackfillCursor());

        Assert.Contains(logger.Messages, m => m.Contains("receipts=100"));
        Assert.Contains(logger.Messages, m => m.Contains("frozen=100"));
        Assert.Contains(logger.Messages, m => m.Contains("txn_idx=100"));
        Assert.Contains(logger.Messages, m => m.Contains("log_idx=59"));
        Assert.DoesNotContain(logger.Messages, m => m.Contains("receipts=0"));
    }

    [Fact]
    public async Task Reporter_BodiesStage_FreezerActive_ReportsLiveLogRenderProgressAlongsideDurableLogIndex()
    {
        using var inner = InMemoryChainStoreBundle.Open();
        inner.Metadata.SaveHeaderSyncState(new HeaderSyncState
        {
            SchemaVersion = 1,
            Subchains = new List<HeaderSubchain> { new HeaderSubchain { Head = 100, Tail = 0, Next = 0 } },
        });
        inner.Metadata.SetLastFetchedBody(100);

        using var bundle = new FreezerFrontierBundle(
            inner, freezerHead: 100, byHashIndexedHead: 100, logIndexRenderedHead: 59, logRenderProgressBlock: 87);
        using var metrics = new SnapSyncMetrics("test");
        var logger = new CapturingLogger();

        var reporter = new SnapSyncProgressReporter(bundle, metrics, logger, peerPool: null, canonical: null);
        await reporter.EmitReportAsync(CancellationToken.None);

        Assert.Contains(logger.Messages, m => m.Contains("log_idx=59"));
        Assert.Contains(logger.Messages, m => m.Contains("log_lv=87"));
        Assert.DoesNotContain(logger.Messages, m => m.Contains("log_lv=59"));
    }

    [Fact]
    public async Task Reporter_BodiesStage_RenderCaughtUp_ReportsLogLvEqualToDurableLogIndex()
    {
        using var inner = InMemoryChainStoreBundle.Open();
        inner.Metadata.SaveHeaderSyncState(new HeaderSyncState
        {
            SchemaVersion = 1,
            Subchains = new List<HeaderSubchain> { new HeaderSubchain { Head = 100, Tail = 0, Next = 0 } },
        });
        inner.Metadata.SetLastFetchedBody(100);

        using var bundle = new FreezerFrontierBundle(
            inner, freezerHead: 100, byHashIndexedHead: 100, logIndexRenderedHead: 74, logRenderProgressBlock: 74);
        using var metrics = new SnapSyncMetrics("test");
        var logger = new CapturingLogger();

        var reporter = new SnapSyncProgressReporter(bundle, metrics, logger, peerPool: null, canonical: null);
        await reporter.EmitReportAsync(CancellationToken.None);

        Assert.Contains(logger.Messages, m => m.Contains("log_idx=74") && m.Contains("log_lv=74"));
    }

    [Fact]
    public async Task Reporter_BodiesStage_NoFreezer_FallsBackToReceiptBackfillCursor()
    {
        using var bundle = InMemoryChainStoreBundle.Open();
        bundle.Metadata.SaveHeaderSyncState(new HeaderSyncState
        {
            SchemaVersion = 1,
            Subchains = new List<HeaderSubchain> { new HeaderSubchain { Head = 100, Tail = 0, Next = 0 } },
        });
        bundle.Metadata.SetLastFetchedBody(100);
        bundle.Metadata.SetReceiptBackfillCursor(42);

        using var metrics = new SnapSyncMetrics("test");
        var logger = new CapturingLogger();

        var reporter = new SnapSyncProgressReporter(bundle, metrics, logger, peerPool: null, canonical: null);
        await reporter.EmitReportAsync(CancellationToken.None);

        Assert.Contains(logger.Messages, m => m.Contains("receipts=42"));
        Assert.Contains(logger.Messages, m => m.Contains("frozen=0"));
    }

    [Fact]
    public async Task Reporter_RespectsCancellation()
    {
        using var bundle = InMemoryChainStoreBundle.Open();
        using var metrics = new SnapSyncMetrics("test");
        var logger = new CapturingLogger();
        var reporter = new SnapSyncProgressReporter(bundle, metrics, logger, peerPool: null, canonical: null);

        using var cts = new CancellationTokenSource();
        var runTask = ((Microsoft.Extensions.Hosting.BackgroundService)reporter).StartAsync(cts.Token);
        await runTask;
        cts.Cancel();
        await ((Microsoft.Extensions.Hosting.BackgroundService)reporter).StopAsync(CancellationToken.None);
    }

    private sealed class EmptyFakePeerPool : IPeerPool
    {
        public IReadOnlyCollection<IEthPeer> ActivePeers => Array.Empty<IEthPeer>();
        public int TargetPeerCount => 0;
        public event EventHandler<IEthPeer>? PeerAdded;
        public event EventHandler<IEthPeer>? PeerRemoved;
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;
        public Task DropAsync(Guid peerId, string reason, CancellationToken ct) => Task.CompletedTask;
        public void ReportSuccess(Guid peerId) { }
        public Task ClearAllBansAsync() => Task.CompletedTask;
        public ValueTask DisposeAsync() => default;
        private void TouchEvents()
        {
            PeerAdded?.Invoke(this, null!);
            PeerRemoved?.Invoke(this, null!);
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now;
        public ManualClock(DateTimeOffset start) => _now = start;
        public void Advance(TimeSpan by) => _now += by;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class BackpressuredBundle : FreezerFrontierBundle, IStateWriteBackpressure
    {
        public BackpressuredBundle(InMemoryChainStoreBundle inner)
            : base(inner, freezerHead: 0, byHashIndexedHead: 0, logIndexRenderedHead: 0) { }

        public string? Pressure { get; set; }
        public bool ShouldPauseStateWrites() => Pressure != null;
        public string DescribeStateBackpressure() => Pressure ?? "none";
    }

    private class FreezerFrontierBundle : IChainStoreBundle
    {
        private readonly InMemoryChainStoreBundle _inner;

        public FreezerFrontierBundle(
            InMemoryChainStoreBundle inner, long freezerHead, long byHashIndexedHead, long logIndexRenderedHead,
            long logRenderProgressBlock = 0)
        {
            _inner = inner;
            FreezerHead = freezerHead;
            ByHashIndexedHead = byHashIndexedHead;
            LogIndexRenderedHead = logIndexRenderedHead;
            LogRenderProgressBlock = logRenderProgressBlock;
        }

        public IStateStore State => _inner.State;
        public Nethereum.Merkle.Patricia.Storage.ITrieNodeStore TrieNodes => _inner.TrieNodes;
        public Nethereum.Merkle.Patricia.Storage.ITrieNodeStore StateTrieNodes => _inner.StateTrieNodes;
        public NodeCommitBlockContext NodeCommitBlockSource => _inner.NodeCommitBlockSource;
        public IBlockStore Blocks => _inner.Blocks;
        public ITransactionStore Transactions => _inner.Transactions;
        public IUncleStore Uncles => _inner.Uncles;
        public IWithdrawalStore Withdrawals => _inner.Withdrawals;
        public IBlockAccessListStore BlockAccessLists => _inner.BlockAccessLists;
        public IReceiptStore Receipts => _inner.Receipts;
        public ILogStore Logs => _inner.Logs;
        public IChainMetadataStore Metadata => _inner.Metadata;
        public IStateDiffStore Diffs => _inner.Diffs;
        public bool JournalEnabled => _inner.JournalEnabled;
        public long FreezerHead { get; }
        public long ByHashIndexedHead { get; }
        public long LogIndexRenderedHead { get; }
        public long LogRenderProgressBlock { get; }

        public Task<ChainCheckpoint> SaveCheckpointAsync(ulong blockNumber, byte[] stateRoot, byte[] blockHash, CancellationToken ct = default)
            => _inner.SaveCheckpointAsync(blockNumber, stateRoot, blockHash, ct);
        public Task<IReadOnlyList<ChainCheckpoint>> ListCheckpointsAsync(CancellationToken ct = default) => _inner.ListCheckpointsAsync(ct);
        public Task RestoreCheckpointAsync(ulong blockNumber, CancellationToken ct = default) => _inner.RestoreCheckpointAsync(blockNumber, ct);
        public Task DeleteCheckpointAsync(ulong blockNumber, CancellationToken ct = default) => _inner.DeleteCheckpointAsync(blockNumber, ct);
        public Task ResetStateOnlyAsync(CancellationToken ct = default) => _inner.ResetStateOnlyAsync(ct);
        public Task ResetSnapBootstrapStateAsync(CancellationToken ct = default) => _inner.ResetSnapBootstrapStateAsync(ct);
        public string ResolveCheckpointSnapshotPath(ulong blockNumber) => _inner.ResolveCheckpointSnapshotPath(blockNumber);
        public Task ExportDatabaseAsync(string outputPath, CancellationToken ct = default) => _inner.ExportDatabaseAsync(outputPath, ct);
        public IBundleBatch BeginBatch() => _inner.BeginBatch();
        public void Dispose() => _inner.Dispose();
        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }

    private sealed class CapturingLogger : ILogger<SnapSyncProgressReporter>
    {
        public List<string> Messages { get; } = new();
        public List<(LogLevel Level, string Message)> Entries { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter == null) return;
            var message = formatter(state, exception);
            Messages.Add(message);
            Entries.Add((logLevel, message));
        }
    }
}
