using System.Collections.Generic;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.MainnetChain.Server.Configuration;
using Nethereum.MainnetChain.Server.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests;

public class MainnetChainHostedServiceStartBlockTests
{
    private static SnapSyncState BuildSnapState(SnapPhase phase, ulong pivotBlock)
        => new SnapSyncState
        {
            SchemaVersion = 1,
            Phase = phase,
            PivotBlockNumber = pivotBlock,
            PivotBlockHash = new byte[32],
            HealTargetRoot = new byte[32],
            Tasks = new List<SnapSyncAccountTask>(),
            Counters = SnapSyncCounters.Zero,
        };

    [Fact]
    public void Resolve_FreshStart_NoSnap_NoBlocks_ReturnsConfiguredStartBlock()
    {
        var config = new MainnetChainServerConfig { StartBlock = 1, Blocks = ulong.MaxValue };
        using var bundle = InMemoryChainStoreBundle.Open();

        var bounds = EffectiveStartBlockResolver.Resolve(
            bundle.Metadata.GetSnapSyncState(), bundle.Metadata.GetLastBlock(), config);

        Assert.Equal(1UL, bounds.StartBlock);
        Assert.Null(bounds.EndBlock);
        Assert.Equal(EffectiveStartBlockResolver.StartBlockReason.FreshStart, bounds.Reason);
    }

    [Fact]
    public void Resolve_FreshStart_CustomStartBlock_HonoursConfig()
    {
        var config = new MainnetChainServerConfig { StartBlock = 100, Blocks = ulong.MaxValue };
        using var bundle = InMemoryChainStoreBundle.Open();

        var bounds = EffectiveStartBlockResolver.Resolve(
            bundle.Metadata.GetSnapSyncState(), bundle.Metadata.GetLastBlock(), config);

        Assert.Equal(100UL, bounds.StartBlock);
        Assert.Null(bounds.EndBlock);
        Assert.Equal(EffectiveStartBlockResolver.StartBlockReason.FreshStart, bounds.Reason);
    }

    [Fact]
    public void Resolve_FreshStart_FiniteBlocks_ComputesEndBlockRelativeToConfiguredStart()
    {
        var config = new MainnetChainServerConfig { StartBlock = 100, Blocks = 50 };
        using var bundle = InMemoryChainStoreBundle.Open();

        var bounds = EffectiveStartBlockResolver.Resolve(
            bundle.Metadata.GetSnapSyncState(), bundle.Metadata.GetLastBlock(), config);

        Assert.Equal(100UL, bounds.StartBlock);
        Assert.Equal(149UL, bounds.EndBlock);
    }

    [Fact]
    public void Resolve_ResumeFromCommittedBlocks_NoSnap_StartsAtLastBlockPlusOne()
    {
        var config = new MainnetChainServerConfig { StartBlock = 1, Blocks = ulong.MaxValue };
        using var bundle = InMemoryChainStoreBundle.Open();
        bundle.Metadata.Commit(500, new byte[32]);

        var bounds = EffectiveStartBlockResolver.Resolve(
            bundle.Metadata.GetSnapSyncState(), bundle.Metadata.GetLastBlock(), config);

        Assert.Equal(501UL, bounds.StartBlock);
        Assert.Null(bounds.EndBlock);
        Assert.Equal(EffectiveStartBlockResolver.StartBlockReason.ResumeFromLastBlock, bounds.Reason);
    }

    [Fact]
    public void Resolve_PostSnap_NoExecutorProgress_StartsAtPivotPlusOne()
    {
        var config = new MainnetChainServerConfig { StartBlock = 1, Blocks = ulong.MaxValue };
        using var bundle = InMemoryChainStoreBundle.Open();
        bundle.Metadata.SaveSnapSyncState(BuildSnapState(SnapPhase.Complete, 25_000_000));

        var bounds = EffectiveStartBlockResolver.Resolve(
            bundle.Metadata.GetSnapSyncState(), bundle.Metadata.GetLastBlock(), config);

        Assert.Equal(25_000_001UL, bounds.StartBlock);
        Assert.Null(bounds.EndBlock);
        Assert.Equal(EffectiveStartBlockResolver.StartBlockReason.PostSnapPivotFastStart, bounds.Reason);
    }

    [Fact]
    public void Resolve_PostSnap_WithPriorExecutorProgress_StartsAtLastBlockPlusOne()
    {
        var config = new MainnetChainServerConfig { StartBlock = 1, Blocks = ulong.MaxValue };
        using var bundle = InMemoryChainStoreBundle.Open();
        bundle.Metadata.SaveSnapSyncState(BuildSnapState(SnapPhase.Complete, 25_000_000));
        bundle.Metadata.Commit(25_000_050, new byte[32]);

        var bounds = EffectiveStartBlockResolver.Resolve(
            bundle.Metadata.GetSnapSyncState(), bundle.Metadata.GetLastBlock(), config);

        Assert.Equal(25_000_051UL, bounds.StartBlock);
        Assert.Null(bounds.EndBlock);
        Assert.Equal(EffectiveStartBlockResolver.StartBlockReason.ResumeFromLastBlock, bounds.Reason);
    }

    [Fact]
    public void Resolve_MidSnap_Phase2Running_NoCommittedBlocks_FallsBackToConfiguredStart()
    {
        var config = new MainnetChainServerConfig { StartBlock = 1, Blocks = ulong.MaxValue };
        using var bundle = InMemoryChainStoreBundle.Open();
        bundle.Metadata.SaveSnapSyncState(BuildSnapState(SnapPhase.Phase2Running, 25_000_000));

        var bounds = EffectiveStartBlockResolver.Resolve(
            bundle.Metadata.GetSnapSyncState(), bundle.Metadata.GetLastBlock(), config);

        Assert.Equal(1UL, bounds.StartBlock);
        Assert.Null(bounds.EndBlock);
        Assert.Equal(EffectiveStartBlockResolver.StartBlockReason.FreshStart, bounds.Reason);
    }

    [Fact]
    public void Resolve_PostSnap_FiniteBlocks_EndBlockRelativeToPivotPlusOne()
    {
        var config = new MainnetChainServerConfig { StartBlock = 1, Blocks = 100 };
        using var bundle = InMemoryChainStoreBundle.Open();
        bundle.Metadata.SaveSnapSyncState(BuildSnapState(SnapPhase.Complete, 25_000_000));

        var bounds = EffectiveStartBlockResolver.Resolve(
            bundle.Metadata.GetSnapSyncState(), bundle.Metadata.GetLastBlock(), config);

        Assert.Equal(25_000_001UL, bounds.StartBlock);
        Assert.Equal(25_000_100UL, bounds.EndBlock);
    }

    [Fact]
    public void BuildOptions_PropagatesCheckpointAndKeepLatestSettings()
    {
        var config = new MainnetChainServerConfig
        {
            StartBlock = 1,
            Blocks = ulong.MaxValue,
            CheckpointEvery = 12345UL,
            KeepLatestCheckpoints = 7,
        };
        using var bundle = InMemoryChainStoreBundle.Open();

        var bounds = EffectiveStartBlockResolver.Resolve(
            bundle.Metadata.GetSnapSyncState(), bundle.Metadata.GetLastBlock(), config);
        var options = EffectiveStartBlockResolver.BuildOptions(bounds, config);

        Assert.Equal(1UL, options.StartBlock);
        Assert.Equal(12345UL, options.CheckpointEvery);
        Assert.Equal(0UL, options.AnchorEvery);
        Assert.Null(options.EndBlock);
        Assert.Equal(7, options.KeepLatestCheckpoints);
    }
}
