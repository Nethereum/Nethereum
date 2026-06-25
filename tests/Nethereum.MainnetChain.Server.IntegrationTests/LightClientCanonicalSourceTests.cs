using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Consensus.LightClient;
using Nethereum.CoreChain.Validation;
using Nethereum.MainnetChain.Server.Bootstrap;
using Nethereum.MainnetChain.Server.Configuration;
using Nethereum.MainnetChain.Server.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests;

public class LightClientCanonicalSourceTests
{
    private static readonly byte[] BlockHashSample = MakeHash(0xAA);
    private static readonly byte[] StateRootSample = MakeHash(0xBB);
    private const ulong BlockNumberSample = 25_000_000;

    [Fact]
    public async Task GetLatestAsync_ReturnsCanonicalTip_WhenFinalizedHeaderAvailable()
    {
        var provider = new FakeTrustedHeaderProvider
        {
            FinalizedHeader = new TrustedExecutionHeader
            {
                BlockNumber = BlockNumberSample,
                BlockHash = BlockHashSample,
                StateRoot = StateRootSample,
            },
        };
        var source = new LightClientCanonicalSource(provider);

        var tip = await source.GetLatestAsync(CancellationToken.None);

        Assert.NotNull(tip);
        Assert.Equal(BlockNumberSample, tip!.BlockNumber);
        Assert.Equal(BlockHashSample, tip.BlockHash);
        Assert.Equal(StateRootSample, tip.StateRoot);
    }

    [Fact]
    public async Task GetLatestAsync_ReturnsNull_WhenLightClientHasNoState()
    {
        var provider = new FakeTrustedHeaderProvider
        {
            ThrowOnFinalized = true,
        };
        var source = new LightClientCanonicalSource(provider);

        var tip = await source.GetLatestAsync(CancellationToken.None);

        Assert.Null(tip);
    }

    [Fact]
    public async Task GetLatestAsync_UsesOptimisticHeader_WhenConfigured()
    {
        var optimisticHash = MakeHash(0xCC);
        var optimisticRoot = MakeHash(0xDD);
        var provider = new FakeTrustedHeaderProvider
        {
            OptimisticHeader = new TrustedExecutionHeader
            {
                BlockNumber = BlockNumberSample + 32,
                BlockHash = optimisticHash,
                StateRoot = optimisticRoot,
            },
        };
        var source = new LightClientCanonicalSource(provider, useOptimistic: true);

        var tip = await source.GetLatestAsync(CancellationToken.None);

        Assert.NotNull(tip);
        Assert.Equal(BlockNumberSample + 32, tip!.BlockNumber);
        Assert.Equal(optimisticHash, tip.BlockHash);
    }

    [Fact]
    public async Task GetCanonicalAsync_ReturnsRootAndHash_WhenBlockMatchesFinalizedHeader()
    {
        var provider = new FakeTrustedHeaderProvider
        {
            FinalizedHeader = new TrustedExecutionHeader
            {
                BlockNumber = BlockNumberSample,
                BlockHash = BlockHashSample,
                StateRoot = StateRootSample,
            },
        };
        var source = new LightClientCanonicalSource(provider);

        var (root, hash) = await source.GetCanonicalAsync(BlockNumberSample, CancellationToken.None);

        Assert.Equal(StateRootSample, root);
        Assert.Equal(BlockHashSample, hash);
    }

    [Fact]
    public async Task GetCanonicalAsync_ReturnsNull_WhenBlockDoesNotMatchFinalizedHeader()
    {
        var provider = new FakeTrustedHeaderProvider
        {
            FinalizedHeader = new TrustedExecutionHeader
            {
                BlockNumber = BlockNumberSample,
                BlockHash = BlockHashSample,
                StateRoot = StateRootSample,
            },
        };
        var source = new LightClientCanonicalSource(provider);

        var (root, hash) = await source.GetCanonicalAsync(BlockNumberSample - 1, CancellationToken.None);

        Assert.Null(root);
        Assert.Null(hash);
    }

    [Fact]
    public async Task GetCanonicalAsync_ReturnsNull_WhenLightClientHasNoState()
    {
        var provider = new FakeTrustedHeaderProvider { ThrowOnFinalized = true };
        var source = new LightClientCanonicalSource(provider);

        var (root, hash) = await source.GetCanonicalAsync(BlockNumberSample, CancellationToken.None);

        Assert.Null(root);
        Assert.Null(hash);
    }

    [Fact]
    public void Name_IncludesFinalizedOrOptimisticLabel()
    {
        var finalizedSource = new LightClientCanonicalSource(new FakeTrustedHeaderProvider());
        var optimisticSource = new LightClientCanonicalSource(new FakeTrustedHeaderProvider(), useOptimistic: true);

        Assert.Contains("finalized", finalizedSource.Name);
        Assert.Contains("optimistic", optimisticSource.Name);
    }

    [Fact]
    public void Constructor_ThrowsOnNullProvider()
    {
        Assert.Throws<ArgumentNullException>(() => new LightClientCanonicalSource(null!));
    }

    [Fact]
    public void AddMainnetChainServer_WithLightClient_RegistersCompositeCanonicalSourceContainingLightClient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = new FakeTrustedHeaderProvider
        {
            FinalizedHeader = new TrustedExecutionHeader
            {
                BlockNumber = BlockNumberSample,
                BlockHash = BlockHashSample,
                StateRoot = StateRootSample,
            },
        };
        services.AddSingleton<ITrustedHeaderProvider>(provider);
        services.AddSingleton<LightClientService>(BuildNoopLightClientService());

        services.AddMainnetChainServer(new MainnetChainServerConfig
        {
            LightClient = new LightClientConfigSection { BeaconEndpoint = "http://test" },
        });

        using var sp = services.BuildServiceProvider();

        var canonical = sp.GetRequiredService<ICanonicalStateRootSource>();
        Assert.IsType<CompositeCanonicalStateRootSource>(canonical);
        Assert.Contains("LightClient", canonical.Name);
    }

    [Fact]
    public async Task AddMainnetChainServer_WithLightClient_CompositeReturnsLightClientTipFromGetLatestAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = new FakeTrustedHeaderProvider
        {
            FinalizedHeader = new TrustedExecutionHeader
            {
                BlockNumber = BlockNumberSample,
                BlockHash = BlockHashSample,
                StateRoot = StateRootSample,
            },
        };
        services.AddSingleton<ITrustedHeaderProvider>(provider);
        services.AddSingleton<LightClientService>(BuildNoopLightClientService());

        services.AddMainnetChainServer(new MainnetChainServerConfig
        {
            LightClient = new LightClientConfigSection { BeaconEndpoint = "http://test" },
        });

        using var sp = services.BuildServiceProvider();
        var canonical = sp.GetRequiredService<ICanonicalStateRootSource>();

        var tip = await canonical.GetLatestAsync(CancellationToken.None);

        Assert.NotNull(tip);
        Assert.Equal(BlockNumberSample, tip!.BlockNumber);
        Assert.Equal(BlockHashSample, tip.BlockHash);
        Assert.Equal(StateRootSample, tip.StateRoot);
    }

    [Fact]
    public async Task AddMainnetChainServer_WithoutLightClient_FallsBackToCheckpointsOnly()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMainnetChainServer(new MainnetChainServerConfig());

        using var sp = services.BuildServiceProvider();
        var canonical = sp.GetRequiredService<ICanonicalStateRootSource>();

        Assert.IsType<MainnetKnownCheckpoints>(canonical);
        var tip = await canonical.GetLatestAsync(CancellationToken.None);
        Assert.Null(tip);
    }

    private static byte[] MakeHash(byte fill)
    {
        var h = new byte[32];
        for (var i = 0; i < 32; i++) h[i] = fill;
        return h;
    }

    private static LightClientService BuildNoopLightClientService()
    {
        return new LightClientService(
            apiClient: new NoopLightClientApi(),
            bls: new NoopBls(),
            config: new LightClientConfig(),
            store: new InMemoryLightClientStore());
    }

    private sealed class FakeTrustedHeaderProvider : ITrustedHeaderProvider
    {
        public TrustedExecutionHeader? FinalizedHeader { get; set; }
        public TrustedExecutionHeader? OptimisticHeader { get; set; }
        public bool ThrowOnFinalized { get; set; }
        public bool ThrowOnOptimistic { get; set; }

        public TrustedExecutionHeader GetLatestFinalized()
        {
            if (ThrowOnFinalized || FinalizedHeader is null)
                throw new InvalidOperationException("Light client has no finalized payload yet.");
            return FinalizedHeader;
        }

        public TrustedExecutionHeader GetLatestOptimistic()
        {
            if (ThrowOnOptimistic || OptimisticHeader is null)
                throw new InvalidOperationException("Light client has no optimistic payload yet.");
            return OptimisticHeader;
        }

        public byte[] GetBlockHash(ulong blockNumber) => Array.Empty<byte>();
    }

    private sealed class NoopLightClientApi : Beaconchain.LightClient.ILightClientApi
    {
        public Task<Beaconchain.LightClient.Responses.LightClientBootstrapResponse> GetBootstrapAsync(string blockRoot)
            => Task.FromResult(new Beaconchain.LightClient.Responses.LightClientBootstrapResponse());
        public Task<System.Collections.Generic.IReadOnlyList<Beaconchain.LightClient.Responses.LightClientUpdateResponse>> GetUpdatesAsync(ulong startPeriod, ulong count)
            => Task.FromResult<System.Collections.Generic.IReadOnlyList<Beaconchain.LightClient.Responses.LightClientUpdateResponse>>(Array.Empty<Beaconchain.LightClient.Responses.LightClientUpdateResponse>());
        public Task<Beaconchain.LightClient.Responses.LightClientFinalityUpdateResponse> GetFinalityUpdateAsync()
            => Task.FromResult(new Beaconchain.LightClient.Responses.LightClientFinalityUpdateResponse());
        public Task<Beaconchain.LightClient.Responses.LightClientOptimisticUpdateResponse> GetOptimisticUpdateAsync()
            => Task.FromResult(new Beaconchain.LightClient.Responses.LightClientOptimisticUpdateResponse());
    }

    private sealed class NoopBls : Signer.Bls.IBls
    {
        public bool VerifyAggregate(byte[] aggregateSignature, byte[][] publicKeys, byte[][] messages, byte[] domain) => true;
        public byte[] AggregateSignatures(byte[][] signatures) => Array.Empty<byte>();
        public bool Verify(byte[] signature, byte[] publicKey, byte[] message) => true;
        public (byte[] Signature, byte[] PublicKey) ExtractSignatureAndPublicKey(byte[] signatureWithPubKey)
            => (Array.Empty<byte>(), Array.Empty<byte>());
    }
}
