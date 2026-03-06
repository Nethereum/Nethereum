using System.Net.Http.Json;
using Nethereum.Aspire.IntegrationTests.Infrastructure;
using Xunit;

namespace Nethereum.Aspire.IntegrationTests;

[Collection("Aspire")]
public class LoadGeneratorTests
{
    private readonly AspireFixture _fixture;

    public LoadGeneratorTests(AspireFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task LoadGenerator_IsRunning_ReturnsMetrics()
    {
        await Task.Delay(TimeSpan.FromSeconds(10));

        var response = await _fixture.LoadGeneratorClient.GetAsync("/metrics/stats");
        Assert.True(response.IsSuccessStatusCode,
            $"Load generator /metrics/stats returned {response.StatusCode}");

        var stats = await response.Content.ReadFromJsonAsync<LoadGeneratorStats>();
        Assert.NotNull(stats);
    }

    [Fact]
    public async Task LoadGenerator_AfterWarmup_HasSuccessfulTransactions()
    {
        await Task.Delay(TimeSpan.FromSeconds(15));

        var stats = await _fixture.LoadGeneratorClient
            .GetFromJsonAsync<LoadGeneratorStats>("/metrics/stats");

        Assert.NotNull(stats);
        Assert.True(stats.TotalSuccess > 0,
            $"Expected TotalSuccess > 0 but got {stats.TotalSuccess}");
    }

    [Fact]
    public async Task LoadGenerator_Traffic_IsIndexedInPostgres()
    {
        await Task.Delay(TimeSpan.FromSeconds(15));

        var blockNumber = await _fixture.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
        Assert.True(blockNumber.Value > 0);

        await using var connection = await _fixture.CreateDbConnectionAsync();
        await TestContractDeployer.WaitForIndexerCaughtUpAsync(
            connection, (long)blockNumber.Value);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"SELECT COUNT(*) FROM ""Blocks""";
        var blockCount = (long)(await cmd.ExecuteScalarAsync())!;

        Assert.True(blockCount > 1,
            $"Expected multiple blocks from load generator traffic, got {blockCount}");
    }

    public class LoadGeneratorStats
    {
        public long TotalSuccess { get; set; }
        public long TotalFailed { get; set; }
        public double CurrentTps { get; set; }
        public double PeakTps { get; set; }
        public long UptimeSeconds { get; set; }
        public double P50LatencyMs { get; set; }
        public double P95LatencyMs { get; set; }
        public double P99LatencyMs { get; set; }
        public double AvgLatencyMs { get; set; }
    }
}
