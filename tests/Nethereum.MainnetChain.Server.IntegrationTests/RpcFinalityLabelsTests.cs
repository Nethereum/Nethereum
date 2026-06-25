using System.Numerics;
using Nethereum.Consensus.LightClient;
using Nethereum.Consensus.Ssz;
using Nethereum.MainnetChain.Server.Rpc;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests;

public class RpcFinalityLabelsTests
{
    private static readonly BigInteger _zero = BigInteger.Zero;

    public static IEnumerable<object[]> AllLabels => new[]
    {
        new object[] { "latest" },
        new object[] { "safe" },
        new object[] { "finalized" },
    };

    [Theory]
    [MemberData(nameof(AllLabels))]
    public void ResolveLabel_WithoutLightClient_FallsBackToLatest(string label)
    {
        var cursor = new LatestOnlyFinalityCursorProvider();
        var latest = (BigInteger)10_000;

        var resolved = MainnetChainEthGetBlockByNumberHandler.ResolveLabel(label, cursor, latest);

        Assert.Equal(latest, resolved);
    }

    [Theory]
    [MemberData(nameof(AllLabels))]
    public void ResolveLabel_WithLightClient_UsesTrustedHeights(string label)
    {
        var state = new LightClientState
        {
            FinalizedExecutionPayload = new ExecutionPayloadHeader { BlockNumber = 9_500 },
            OptimisticExecutionPayload = new ExecutionPayloadHeader { BlockNumber = 9_800 },
        };
        var cursor = new LightClientFinalityCursorProvider(() => state);
        var latest = (BigInteger)10_000;

        var resolved = MainnetChainEthGetBlockByNumberHandler.ResolveLabel(label, cursor, latest);

        var expected = label switch
        {
            "finalized" => (BigInteger)9_500,
            "safe" => (BigInteger)9_800,
            "latest" => latest,
            _ => latest,
        };
        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void ResolveLabel_EarliestReturnsZero()
    {
        var cursor = new LatestOnlyFinalityCursorProvider();
        var resolved = MainnetChainEthGetBlockByNumberHandler.ResolveLabel("earliest", cursor, (BigInteger)999);
        Assert.Equal(_zero, resolved);
    }

    [Fact]
    public void ResolveLabel_HexNumberPassesThrough()
    {
        var cursor = new LatestOnlyFinalityCursorProvider();
        var resolved = MainnetChainEthGetBlockByNumberHandler.ResolveLabel("0x10", cursor, (BigInteger)999);
        Assert.Equal((BigInteger)16, resolved);
    }

    [Fact]
    public void ResolveLabel_PendingReturnsLatest()
    {
        var cursor = new LatestOnlyFinalityCursorProvider();
        var resolved = MainnetChainEthGetBlockByNumberHandler.ResolveLabel("pending", cursor, (BigInteger)1234);
        Assert.Equal((BigInteger)1234, resolved);
    }

    [Fact]
    public void ResolveLabel_FinalizedWhenLightClientNotYetBootstrappedFallsBackToLatest()
    {
        var cursor = new LightClientFinalityCursorProvider(() => null);
        var resolved = MainnetChainEthGetBlockByNumberHandler.ResolveLabel("finalized", cursor, (BigInteger)555);
        Assert.Equal((BigInteger)555, resolved);
    }
}
