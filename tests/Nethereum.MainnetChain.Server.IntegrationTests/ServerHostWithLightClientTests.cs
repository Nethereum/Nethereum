using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Consensus.LightClient;
using Nethereum.Consensus.Ssz;
using Nethereum.MainnetChain.Server.Configuration;
using Nethereum.MainnetChain.Server.Gate;
using Nethereum.MainnetChain.Server.Hosting;
using Nethereum.MainnetChain.Server.Rpc;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests;

public class ServerHostWithLightClientTests
{
    [Fact]
    public void AddMainnetChainServer_WithBeaconEndpoint_RegistersLightClientGate()
    {
        var services = new ServiceCollection();
        var config = new MainnetChainServerConfig
        {
            LightClient = new LightClientConfigSection
            {
                BeaconEndpoint = "http://beacon.test",
            },
        };

        services.AddLogging();
        services.AddSingleton<LightClientService>(BuildMockLightClientService(null));

        services.AddMainnetChainServer(config);

        using var provider = services.BuildServiceProvider();

        var gate = provider.GetRequiredService<IConsensusBlockGate>();
        Assert.IsType<LightClientConsensusBlockGate>(gate);

        var cursor = provider.GetRequiredService<IFinalityCursorProvider>();
        Assert.IsType<LightClientFinalityCursorProvider>(cursor);
    }

    [Fact]
    public async Task LightClientGate_AcceptsMatchingHash()
    {
        var matchingHash = MakeHash(0x11);
        var state = BuildStateWithFinalizedHash(blockNumber: 100, hash: matchingHash);

        var gate = new LightClientConsensusBlockGate(() => state);

        var header = new BlockHeader { BlockNumber = (EvmUInt256)(BigInteger)100 };
        var verdict = await gate.IsBlockCanonicalAsync(header, matchingHash, CancellationToken.None);

        Assert.True(verdict.Accepted);
    }

    [Fact]
    public async Task LightClientGate_RejectsMismatchedHash()
    {
        var trustedHash = MakeHash(0x11);
        var divergentHash = MakeHash(0x22);
        var state = BuildStateWithFinalizedHash(blockNumber: 100, hash: trustedHash);

        var gate = new LightClientConsensusBlockGate(() => state);

        var header = new BlockHeader { BlockNumber = (EvmUInt256)(BigInteger)100 };
        var verdict = await gate.IsBlockCanonicalAsync(header, divergentHash, CancellationToken.None);

        Assert.False(verdict.Accepted);
        Assert.NotNull(verdict.Reason);
        Assert.Contains("block 100", verdict.Reason);
    }

    [Fact]
    public async Task LightClientGate_AcceptsBlocksBeyondLightClientCursor()
    {
        var hash = MakeHash(0x33);
        var state = BuildStateWithFinalizedHash(blockNumber: 100, hash: hash);

        var gate = new LightClientConsensusBlockGate(() => state);

        var header = new BlockHeader { BlockNumber = (EvmUInt256)(BigInteger)999 };
        var verdict = await gate.IsBlockCanonicalAsync(header, MakeHash(0x99), CancellationToken.None);

        Assert.True(verdict.Accepted);
    }

    [Fact]
    public async Task LightClientGate_DegradesGracefullyWhenStateNull()
    {
        var gate = new LightClientConsensusBlockGate(() => null);

        var header = new BlockHeader { BlockNumber = (EvmUInt256)(BigInteger)100 };
        var verdict = await gate.IsBlockCanonicalAsync(header, MakeHash(0x55), CancellationToken.None);

        Assert.True(verdict.Accepted);
    }

    [Fact]
    public void LightClientCursorProvider_ReportsFinalizedAndSafeBlockNumbers()
    {
        var state = new LightClientState
        {
            FinalizedExecutionPayload = new ExecutionPayloadHeader { BlockNumber = 12_345 },
            OptimisticExecutionPayload = new ExecutionPayloadHeader { BlockNumber = 12_400 },
        };

        var cursor = new LightClientFinalityCursorProvider(() => state);

        Assert.Equal((BigInteger)12_345, cursor.GetFinalizedBlockNumber());
        Assert.Equal((BigInteger)12_400, cursor.GetSafeBlockNumber());
    }

    [Fact]
    public void LightClientCursorProvider_ReturnsNullWhenStateUnavailable()
    {
        var cursor = new LightClientFinalityCursorProvider(() => null);

        Assert.Null(cursor.GetFinalizedBlockNumber());
        Assert.Null(cursor.GetSafeBlockNumber());
    }

    private static byte[] MakeHash(byte fill)
    {
        var h = new byte[32];
        for (var i = 0; i < 32; i++) h[i] = fill;
        return h;
    }

    private static LightClientState BuildStateWithFinalizedHash(ulong blockNumber, byte[] hash)
    {
        var state = new LightClientState
        {
            FinalizedExecutionPayload = new ExecutionPayloadHeader
            {
                BlockNumber = blockNumber,
                BlockHash = hash,
            },
        };
        state.SetBlockHash(blockNumber, hash, BlockHashFinality.Finalized);
        return state;
    }

    private static LightClientService BuildMockLightClientService(LightClientState? _)
    {
        return new LightClientService(
            apiClient: new NoopLightClientApi(),
            bls: new NoopBls(),
            config: new LightClientConfig(),
            store: new InMemoryLightClientStore());
    }

    private sealed class NoopLightClientApi : Beaconchain.LightClient.ILightClientApi
    {
        public Task<Beaconchain.LightClient.Responses.LightClientBootstrapResponse> GetBootstrapAsync(string blockRoot)
            => Task.FromResult(new Beaconchain.LightClient.Responses.LightClientBootstrapResponse());
        public Task<System.Collections.Generic.IReadOnlyList<Beaconchain.LightClient.Responses.LightClientUpdateResponse>> GetUpdatesAsync(ulong startPeriod, ulong count)
            => Task.FromResult<System.Collections.Generic.IReadOnlyList<Beaconchain.LightClient.Responses.LightClientUpdateResponse>>(
                System.Array.Empty<Beaconchain.LightClient.Responses.LightClientUpdateResponse>());
        public Task<Beaconchain.LightClient.Responses.LightClientFinalityUpdateResponse> GetFinalityUpdateAsync()
            => Task.FromResult(new Beaconchain.LightClient.Responses.LightClientFinalityUpdateResponse());
        public Task<Beaconchain.LightClient.Responses.LightClientOptimisticUpdateResponse> GetOptimisticUpdateAsync()
            => Task.FromResult(new Beaconchain.LightClient.Responses.LightClientOptimisticUpdateResponse());
    }

    private sealed class NoopBls : Signer.Bls.IBls
    {
        public bool VerifyAggregate(byte[] aggregateSignature, byte[][] publicKeys, byte[][] messages, byte[] domain) => true;
        public byte[] AggregateSignatures(byte[][] signatures) => System.Array.Empty<byte>();
        public bool Verify(byte[] signature, byte[] publicKey, byte[] message) => true;
        public (byte[] Signature, byte[] PublicKey) ExtractSignatureAndPublicKey(byte[] signatureWithPubKey)
            => (System.Array.Empty<byte>(), System.Array.Empty<byte>());
    }
}
