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
using Nethereum.MainnetChain.Server.Observability;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests;

public class SnapSyncProgressReporterTests
{
    [Fact]
    public async Task Reporter_NoSnapState_OnlyEmitsPeerSummary()
    {
        using var bundle = InMemoryChainStoreBundle.Open();
        using var metrics = new SnapSyncMetrics("test");
        var pool = new EmptyFakePeerPool();
        var logger = new CapturingLogger();

        var reporter = new SnapSyncProgressReporter(bundle, metrics, logger, pool, canonical: null);
        await reporter.EmitReportAsync(CancellationToken.None);

        Assert.Contains(logger.Messages, m => m.Contains("snap.peers.summary"));
        Assert.DoesNotContain(logger.Messages, m => m.Contains("snap.phase2.state"));
        Assert.DoesNotContain(logger.Messages, m => m.Contains("snap.phase3.heal"));
        Assert.DoesNotContain(logger.Messages, m => m.Contains("snap.phase1.chain"));
    }

    [Fact]
    public async Task Reporter_Phase2Running_EmitsPhase2State()
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

        Assert.Contains(logger.Messages, m => m.Contains("snap.phase2.state"));
        Assert.Contains(logger.Messages, m => m.Contains("1234"));
        Assert.Contains(logger.Messages, m => m.Contains("9999"));
        Assert.DoesNotContain(logger.Messages, m => m.Contains("snap.phase3.heal"));
    }

    [Fact]
    public async Task Reporter_Phase3Running_EmitsPhase3Heal()
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

        var reporter = new SnapSyncProgressReporter(bundle, metrics, logger, peerPool: null, canonical: null);
        await reporter.EmitReportAsync(CancellationToken.None);

        Assert.Contains(logger.Messages, m => m.Contains("snap.phase3.heal"));
        Assert.Contains(logger.Messages, m => m.Contains("7654321"));
    }

    [Fact]
    public async Task Reporter_Phase1Cursors_EmitsChainProgress()
    {
        using var bundle = InMemoryChainStoreBundle.Open();
        bundle.Metadata.SetLastFetchedHeader(1_234_567);
        bundle.Metadata.SetLastFetchedBody(1_234_500);

        using var metrics = new SnapSyncMetrics("test");
        var logger = new CapturingLogger();

        var reporter = new SnapSyncProgressReporter(bundle, metrics, logger, peerPool: null, canonical: null);
        await reporter.EmitReportAsync(CancellationToken.None);

        Assert.Contains(logger.Messages, m => m.Contains("snap.phase1.chain"));
        Assert.Contains(logger.Messages, m => m.Contains("1234567"));
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
        public Task ClearAllBansAsync() => Task.CompletedTask;
        public ValueTask DisposeAsync() => default;
        private void TouchEvents()
        {
            PeerAdded?.Invoke(this, null!);
            PeerRemoved?.Invoke(this, null!);
        }
    }

    private sealed class CapturingLogger : ILogger<SnapSyncProgressReporter>
    {
        public List<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter != null) Messages.Add(formatter(state, exception));
        }
    }
}
