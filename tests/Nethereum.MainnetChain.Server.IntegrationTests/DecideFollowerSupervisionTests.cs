using System;
using Nethereum.CoreChain.Sync;
using Nethereum.MainnetChain.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests;

public class DecideFollowerSupervisionTests
{
    private static readonly TimeSpan Backoff = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);

    [Fact]
    public void Cancelled_Stops_AndLeavesBackoffUnchanged()
    {
        var decision = MainnetChainHostedService.DecideFollowerSupervision(
            FollowerExitReason.Cancelled, Backoff, MaxBackoff);

        Assert.False(decision.KeepFollowing);
        Assert.Equal(Backoff, decision.NextBackoff);
    }

    [Fact]
    public void SourceCompleted_Stops_AndLeavesBackoffUnchanged()
    {
        var decision = MainnetChainHostedService.DecideFollowerSupervision(
            FollowerExitReason.SourceCompleted, Backoff, MaxBackoff);

        Assert.False(decision.KeepFollowing);
        Assert.Equal(Backoff, decision.NextBackoff);
    }

    [Fact]
    public void SourceUnavailable_Retries_AndDoublesBackoff()
    {
        var decision = MainnetChainHostedService.DecideFollowerSupervision(
            FollowerExitReason.SourceUnavailable, Backoff, MaxBackoff);

        Assert.True(decision.KeepFollowing);
        Assert.Equal(TimeSpan.FromSeconds(30), decision.NextBackoff);
    }

    [Fact]
    public void SourceUnavailable_Retries_ButCapsBackoffAtMax()
    {
        var nearCap = TimeSpan.FromMinutes(4);

        var decision = MainnetChainHostedService.DecideFollowerSupervision(
            FollowerExitReason.SourceUnavailable, nearCap, MaxBackoff);

        Assert.True(decision.KeepFollowing);
        Assert.Equal(MaxBackoff, decision.NextBackoff);
    }

    [Fact]
    public void SnapshotRestoreRequested_Stops_AndLeavesBackoffUnchanged()
    {
        var decision = MainnetChainHostedService.DecideFollowerSupervision(
            FollowerExitReason.SnapshotRestoreRequested, Backoff, MaxBackoff);

        Assert.False(decision.KeepFollowing);
        Assert.Equal(Backoff, decision.NextBackoff);
    }

    [Fact]
    public void FatalVerdict_Stops_AndLeavesBackoffUnchanged()
    {
        var decision = MainnetChainHostedService.DecideFollowerSupervision(
            FollowerExitReason.FatalVerdict, Backoff, MaxBackoff);

        Assert.False(decision.KeepFollowing);
        Assert.Equal(Backoff, decision.NextBackoff);
    }

    [Fact]
    public void RewindUnavailable_Stops_AndLeavesBackoffUnchanged()
    {
        var decision = MainnetChainHostedService.DecideFollowerSupervision(
            FollowerExitReason.RewindUnavailable, Backoff, MaxBackoff);

        Assert.False(decision.KeepFollowing);
        Assert.Equal(Backoff, decision.NextBackoff);
    }
}
