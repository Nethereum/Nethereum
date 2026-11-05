using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nethereum.Chain.TestData.Vectors;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Replay;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests.Replay;

[Collection("Sequential")]
public class MainnetReplayRunnerTests : IDisposable
{
    private const int FlushCadence = 4;

    private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"mainnet_replay_{Guid.NewGuid():N}");

    public MainnetReplayRunnerTests() => Directory.CreateDirectory(_workDir);

    public void Dispose()
    {
        try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true); } catch { }
    }

    private string DataDir => Path.Combine(_workDir, "data");

    private string ReportPath => Path.Combine(_workDir, "report.json");

    [Fact]
    public async Task Given_StoreWithExecutedHeadAndStoredBodies_When_ReplayingBlocks_Then_EveryRootMatchesAndReportListsEachBlock()
    {
        var fixture = await MainnetReplayFixture.BuildAsync(DataDir, new WorkloadV1());

        var (exitCode, report) = await ReplayAsync(fixture.Config(FlushCadence, statistics: true));

        Assert.True(report.Succeeded, report.FailureDetail);
        Assert.Equal(0, exitCode);
        Assert.Equal("AllRootsMatched", report.Outcome);
        Assert.Equal(MainnetReplayFixture.HeadNumber, report.ExecutedHead);
        Assert.Equal((ulong)fixture.BlockCount, report.BlocksExecuted);
        Assert.Equal(
            fixture.Blocks.Select(b => (ulong)b.Header.BlockNumber),
            report.Blocks.Select(b => b.Number));
        Assert.Equal(
            fixture.Blocks.Select(b => b.Transactions.Count),
            report.Blocks.Select(b => b.TxCount));
        Assert.Equal(
            fixture.Blocks.Select(b => b.Header.GasUsed),
            report.Blocks.Select(b => b.GasUsed));
        Assert.All(report.Blocks, b =>
        {
            Assert.True(b.WallMs > 0);
            Assert.True(b.AllocBytes > 0);
            Assert.True(b.RssBytes > 0);
        });
        Assert.NotNull(report.Summary);
        Assert.True(report.Summary!.MegagasPerSecond > 0);
        Assert.True(report.Summary.WallMs.P90 >= report.Summary.WallMs.Median);
        Assert.Contains(report.Blocks, b => b.ExecMs > 0 || b.RootMs > 0 || b.PersistMs > 0);
        Assert.Contains(report.Blocks, b => b.ExecutionMs > 0 && b.StateHashMs > 0 && b.CommitMs > 0);
        Assert.True(report.Summary.ExecutionMs.Median > 0);
        Assert.NotNull(report.Cache);
        Assert.Equal("start", report.Cache!.Snapshots.First().Label);
        Assert.Equal("end", report.Cache.Snapshots.Last().Label);
        Assert.NotEmpty(report.Cache.WindowDeltas);
        Assert.NotNull(report.Cache.Total);
    }

    [Fact]
    public async Task Given_PrefetchWorkersOnARocksDbStore_When_ReplayingTheSameWorkload_Then_EveryRootMatchesAndTheBlocksAreIdenticalToPrefetchOff()
    {
        var offFixture = await MainnetReplayFixture.BuildAsync(DataDir, new WorkloadV1());
        var (offExit, off) = await ReplayAsync(offFixture.Config(FlushCadence));
        Assert.Empty(PrefetchLines());
        var onFixture = await MainnetReplayFixture.BuildAsync(Path.Combine(_workDir, "data-prefetch"), new WorkloadV1());

        var (onExit, on) = await ReplayAsync(onFixture.Config(FlushCadence, prefetchWorkers: 4));

        var lines = PrefetchLines();
        Assert.Equal(onFixture.BlockCount, lines.Count);
        Assert.True(Sum(lines, "triePathsEnqueued") > 0);
        Assert.True(Sum(lines, "triePathsWarmed") > 0);
        Assert.Equal(0, offExit);
        Assert.Equal(0, onExit);
        Assert.Equal("AllRootsMatched", off.Outcome);
        Assert.Equal("AllRootsMatched", on.Outcome);
        Assert.Equal(off.BlocksExecuted, on.BlocksExecuted);
        Assert.Equal(off.Blocks.Select(b => (b.Number, b.TxCount, b.GasUsed)), on.Blocks.Select(b => (b.Number, b.TxCount, b.GasUsed)));
        Assert.Equal(
            offFixture.Blocks.Select(b => b.Header.StateRoot),
            onFixture.Blocks.Select(b => b.Header.StateRoot));
    }

    [Fact]
    public async Task Given_StateRootWorkersOnARocksDbStore_When_ReplayingTheSameWorkload_Then_EveryRootMatchesAndTheBlocksAreIdenticalToTheSequentialRoot()
    {
        var sequentialFixture = await MainnetReplayFixture.BuildAsync(DataDir, new WorkloadV1());
        var (sequentialExit, sequential) = await ReplayAsync(sequentialFixture.Config(FlushCadence, stateRootWorkers: 1));
        var parallelFixture = await MainnetReplayFixture.BuildAsync(Path.Combine(_workDir, "data-parallel"), new WorkloadV1());

        var (parallelExit, parallel) = await ReplayAsync(parallelFixture.Config(FlushCadence, stateRootWorkers: 4));

        Assert.Equal(0, sequentialExit);
        Assert.Equal(0, parallelExit);
        Assert.Equal("AllRootsMatched", sequential.Outcome);
        Assert.Equal("AllRootsMatched", parallel.Outcome);
        Assert.Equal(sequential.Blocks.Select(b => (b.Number, b.TxCount, b.GasUsed)), parallel.Blocks.Select(b => (b.Number, b.TxCount, b.GasUsed)));
    }

    [Fact]
    public async Task Given_FlushCadenceOfFour_When_Replaying_Then_WindowPositionsCycleOneToFourAndEndOnTheBoundary()
    {
        var fixture = await MainnetReplayFixture.BuildAsync(DataDir, new WorkloadV1());

        var (_, report) = await ReplayAsync(fixture.Config(FlushCadence));

        Assert.Equal((ulong)FlushCadence, report.FlushCadenceBlocks);
        Assert.Equal((int)(MainnetReplayFixture.HeadNumber % FlushCadence) + 1, report.Blocks[0].WindowPosition);
        for (var i = 1; i < report.Blocks.Count; i++)
            Assert.Equal(report.Blocks[i - 1].WindowPosition % FlushCadence + 1, report.Blocks[i].WindowPosition);
        Assert.All(
            report.Blocks.Where(b => b.Number % FlushCadence == 0),
            b => Assert.Equal(FlushCadence, b.WindowPosition));
        Assert.Equal(
            Enumerable.Range(1, FlushCadence),
            report.Summary!.WindowPositions.Select(p => p.Position));
    }

    [Fact]
    public async Task Given_StoredBodyTampered_When_Replaying_Then_ExitsNonZeroNamingTheBlock()
    {
        const int tamperedIndex = 5;
        var fixture = await MainnetReplayFixture.BuildAsync(DataDir, new WorkloadV1());
        await fixture.ReplaceStoredHeaderAsync(tamperedIndex, header => header.StateRoot = Enumerable.Repeat((byte)7, 32).ToArray());

        var (exitCode, report) = await ReplayAsync(fixture.Config(FlushCadence));

        Assert.Equal(1, exitCode);
        Assert.False(report.Succeeded);
        Assert.Equal("RootMismatch", report.Outcome);
        Assert.Equal((ulong)fixture.Blocks[tamperedIndex].Header.BlockNumber, report.FailedBlock);
        Assert.Equal((ulong)tamperedIndex, report.BlocksExecuted);
        Assert.Equal(tamperedIndex, report.Blocks.Count);
        Assert.Contains("state root", report.FailureDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Given_BodyMissingForNextBlock_When_Replaying_Then_FailsFastNamingTheBlock()
    {
        const int missingIndex = 4;
        var fixture = await MainnetReplayFixture.BuildAsync(DataDir, new WorkloadV1(), index => index == missingIndex);

        var (exitCode, report) = await ReplayAsync(fixture.Config(FlushCadence));

        var missingBlock = (ulong)fixture.Blocks[missingIndex].Header.BlockNumber;
        Assert.Equal(1, exitCode);
        Assert.False(report.Succeeded);
        Assert.Equal(missingBlock, report.FailedBlock);
        Assert.Equal((ulong)missingIndex, report.BlocksExecuted);
        Assert.Contains(missingBlock.ToString("N0"), report.FailureDetail);
    }

    [Fact]
    public async Task Given_FewerStoredHeadersThanRequested_When_Replaying_Then_FailsFastNamingTheFirstMissingBlock()
    {
        var fixture = await MainnetReplayFixture.BuildAsync(DataDir, new WorkloadV1());
        var config = fixture.Config(FlushCadence);
        config.ReplayBlocks = (ulong)fixture.BlockCount + 3;

        var (exitCode, report) = await ReplayAsync(config);

        Assert.Equal(1, exitCode);
        Assert.Equal(MainnetReplayFixture.HeadNumber + (ulong)fixture.BlockCount + 1, report.FailedBlock);
        Assert.Contains("never fetches from the network", report.FailureDetail);
        Assert.Empty(report.Blocks);
    }

    private readonly ConcurrentQueue<string> _logLines = new();

    private async Task<(int ExitCode, ReplayReport Report)> ReplayAsync(MainnetChainServerConfig config)
    {
        config.ReplayReport = ReportPath;
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(LogLevel.Information).AddProvider(new CapturingLoggerProvider(_logLines)));
        var exitCode = await MainnetReplayRunner.RunAsync(config, loggerFactory);
        var report = JsonSerializer.Deserialize(File.ReadAllText(ReportPath), ReplayReportJsonContext.Default.ReplayReport)!;
        return (exitCode, report);
    }

    private List<string> PrefetchLines() =>
        _logLines.Where(l => l.StartsWith("block.prefetch.stats")).ToList();

    private static int Sum(IEnumerable<string> lines, string counter) =>
        lines.Sum(l => int.Parse(System.Text.RegularExpressions.Regex.Match(l, counter + @"=(\d+)").Groups[1].Value));

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _lines;

        public CapturingLoggerProvider(ConcurrentQueue<string> lines) { _lines = lines; }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_lines);

        public void Dispose() { }

        private sealed class CapturingLogger : ILogger
        {
            private readonly ConcurrentQueue<string> _lines;

            public CapturingLogger(ConcurrentQueue<string> lines) { _lines = lines; }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => _lines.Enqueue(formatter(state, exception));
        }
    }
}
