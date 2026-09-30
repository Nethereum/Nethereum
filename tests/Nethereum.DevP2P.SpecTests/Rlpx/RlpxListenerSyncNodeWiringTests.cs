using System;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;
using Nethereum.DevP2P.Sync.Serving;

namespace Nethereum.DevP2P.SpecTests.Rlpx;

public class RlpxListenerSyncNodeWiringTests
{
    private static byte[] FakeGenesis(byte salt = 0xC0)
    {
        var g = new byte[32];
        for (int i = 0; i < 32; i++) g[i] = (byte)(salt ^ i);
        return g;
    }

    private static Eth68StatusMessage StatusFor(ulong networkId, byte[] genesis, byte[] best)
    {
        return new Eth68StatusMessage
        {
            ProtocolVersion = 68,
            NetworkId = networkId,
            TotalDifficulty = BigInteger.One,
            BestHash = best,
            GenesisHash = genesis,
            ForkHash = 0xAABBCCDDu,
            ForkNext = 0
        };
    }

    private static Eth69StatusMessage Eth69StatusFor(ulong networkId, byte[] genesis, byte[] latestBlockHash)
    {
        return new Eth69StatusMessage
        {
            ProtocolVersion = 69,
            NetworkId = networkId,
            GenesisHash = genesis,
            ForkHash = 0xAABBCCDDu,
            ForkNext = 0,
            EarliestBlock = 0,
            LatestBlock = 0,
            LatestBlockHash = latestBlockHash
        };
    }

    [Fact]
    public async Task ListenPortZero_BindsOsAssignedPositivePort()
    {
        var serverKey = EthECKey.GenerateKey();
        var genesis = FakeGenesis();
        await using var bundle = InMemoryChainStoreBundle.Open();

        var template = StatusFor(7777, genesis, genesis);
        var options = new PeerListenerOptions
        {
            ListenPort = 0,
            BindAddress = IPAddress.Loopback,
            MaxInboundPeers = 5,
            MaxInboundPerIP = 3,
            ServeSnap = false,
            MirrorRemoteStatus = false,
            ClientId = "Nethereum.Spec.Tests/listen-port-zero"
        };

        await using var listener = new PeerListener(serverKey, bundle, options, template);
        await listener.StartAsync();

        Assert.True(listener.Port > 0, $"Expected positive bound port, got {listener.Port}");
        Assert.NotNull(listener.LocalEndpoint);
        Assert.Equal(IPAddress.Loopback, listener.LocalEndpoint.Address);
    }

    [Fact]
    public async Task InboundPeerLifecycleHooks_FireOnAddAndRemove()
    {
        var serverKey = EthECKey.GenerateKey();
        var clientKey = EthECKey.GenerateKey();
        var genesis = FakeGenesis();
        await using var bundle = InMemoryChainStoreBundle.Open();

        var template = StatusFor(7777, genesis, genesis);

        var addedKey = new TaskCompletionSource<string>();
        var removedKey = new TaskCompletionSource<string>();
        var options = new PeerListenerOptions
        {
            ListenPort = 0,
            BindAddress = IPAddress.Loopback,
            MaxInboundPeers = 5,
            MaxInboundPerIP = 3,
            ServeSnap = false,
            MirrorRemoteStatus = true,
            ClientId = "Nethereum.Spec.Tests/lifecycle",
            OnInboundPeerAdded = key => addedKey.TrySetResult(key),
            OnInboundPeerRemoved = key => removedKey.TrySetResult(key)
        };

        await using var server = new PeerListener(serverKey, bundle, options, template);
        await server.StartAsync();

        var clientConfig = new DevP2PConfig
        {
            ClientId = "Nethereum.Spec.Tests.client/lifecycle",
            ConnectTimeoutMs = 5000,
            HandshakeTimeoutMs = 5000,
            NetworkId = template.NetworkId,
            GenesisHash = genesis
        };

        using var connection = new RlpxConnection(clientKey, clientConfig);
        using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await connection.ConnectAsync("127.0.0.1", server.Port, serverKey.GetPubKeyNoPrefix(), ct.Token);

        var ethOffset = connection.GetCapabilityOffset("eth");
        await connection.SendMessageAsync(
            ethOffset + Eth68MessageIds.Status,
            Eth69StatusMessageEncoder.Encode(Eth69StatusFor(template.NetworkId, genesis, genesis)),
            ct.Token);
        var (replyId, _) = await connection.ReceiveMessageAsync(ct.Token);
        Assert.Equal(ethOffset + Eth68MessageIds.Status, replyId);

        var added = await addedKey.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(string.IsNullOrEmpty(added));

        await connection.DisconnectAsync();

        var removed = await removedKey.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(added, removed);
    }

    [Fact]
    public async Task ServeEmpty_True_AcceptsPeerWhoseHeadDiffersFromOurs()
    {
        var serverKey = EthECKey.GenerateKey();
        var clientKey = EthECKey.GenerateKey();
        var serverGenesis = FakeGenesis(salt: 0xC0);
        var clientGenesis = FakeGenesis(salt: 0xC0);
        await using var bundle = InMemoryChainStoreBundle.Open();

        var serverTemplate = StatusFor(7777, serverGenesis, serverGenesis);

        var options = new PeerListenerOptions
        {
            ListenPort = 0,
            BindAddress = IPAddress.Loopback,
            MaxInboundPeers = 5,
            MaxInboundPerIP = 3,
            ServeSnap = false,
            MirrorRemoteStatus = true,
            ClientId = "Nethereum.Spec.Tests/serve-empty-on"
        };

        await using var server = new PeerListener(serverKey, bundle, options, serverTemplate);
        await server.StartAsync();

        var clientConfig = new DevP2PConfig
        {
            ClientId = "Nethereum.Spec.Tests.client/serve-empty-on",
            ConnectTimeoutMs = 5000,
            HandshakeTimeoutMs = 5000,
            NetworkId = serverTemplate.NetworkId,
            GenesisHash = clientGenesis
        };

        using var connection = new RlpxConnection(clientKey, clientConfig);
        using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await connection.ConnectAsync("127.0.0.1", server.Port, serverKey.GetPubKeyNoPrefix(), ct.Token);

        var clientHead = new byte[32];
        for (int i = 0; i < 32; i++) clientHead[i] = (byte)(0xAB ^ i);
        var clientStatus = Eth69StatusFor(serverTemplate.NetworkId, clientGenesis, clientHead);

        var ethOffset = connection.GetCapabilityOffset("eth");
        await connection.SendMessageAsync(
            ethOffset + Eth68MessageIds.Status,
            Eth69StatusMessageEncoder.Encode(clientStatus),
            ct.Token);

        var (msgId, payload) = await connection.ReceiveMessageAsync(ct.Token);
        Assert.Equal(ethOffset + Eth68MessageIds.Status, msgId);
        var serverReply = Eth69StatusMessageEncoder.Decode(payload);

        Assert.Equal(clientHead, serverReply.LatestBlockHash);
        Assert.Equal(clientGenesis, serverReply.GenesisHash);
    }

    [Fact]
    public async Task ServeEmpty_False_AssertsOurOwnStatusInsteadOfMirroring()
    {
        var serverKey = EthECKey.GenerateKey();
        var clientKey = EthECKey.GenerateKey();
        var serverGenesis = FakeGenesis(salt: 0xC0);
        var clientGenesis = FakeGenesis(salt: 0xC0);
        await using var bundle = InMemoryChainStoreBundle.Open();

        var serverTemplate = StatusFor(7777, serverGenesis, serverGenesis);

        var options = new PeerListenerOptions
        {
            ListenPort = 0,
            BindAddress = IPAddress.Loopback,
            MaxInboundPeers = 5,
            MaxInboundPerIP = 3,
            ServeSnap = false,
            MirrorRemoteStatus = false,
            ClientId = "Nethereum.Spec.Tests/serve-empty-off"
        };

        await using var server = new PeerListener(serverKey, bundle, options, serverTemplate);
        await server.StartAsync();

        var clientConfig = new DevP2PConfig
        {
            ClientId = "Nethereum.Spec.Tests.client/serve-empty-off",
            ConnectTimeoutMs = 5000,
            HandshakeTimeoutMs = 5000,
            NetworkId = serverTemplate.NetworkId,
            GenesisHash = clientGenesis
        };

        using var connection = new RlpxConnection(clientKey, clientConfig);
        using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await connection.ConnectAsync("127.0.0.1", server.Port, serverKey.GetPubKeyNoPrefix(), ct.Token);

        var clientHead = new byte[32];
        for (int i = 0; i < 32; i++) clientHead[i] = (byte)(0xAB ^ i);
        var clientStatus = Eth69StatusFor(serverTemplate.NetworkId, clientGenesis, clientHead);

        var ethOffset = connection.GetCapabilityOffset("eth");
        await connection.SendMessageAsync(
            ethOffset + Eth68MessageIds.Status,
            Eth69StatusMessageEncoder.Encode(clientStatus),
            ct.Token);

        var (msgId, payload) = await connection.ReceiveMessageAsync(ct.Token);
        Assert.Equal(ethOffset + Eth68MessageIds.Status, msgId);
        var serverReply = Eth69StatusMessageEncoder.Decode(payload);

        Assert.Equal(serverTemplate.BestHash, serverReply.LatestBlockHash);
        Assert.Equal(serverTemplate.GenesisHash, serverReply.GenesisHash);
        Assert.NotEqual(clientHead, serverReply.LatestBlockHash);
    }

    [Fact]
    public async Task Given_FreshNodeWithNoBlocks_When_InboundPeerCompletesStatus_Then_ServerRepliesValidStatusWithoutCrashing()
    {
        var serverKey = EthECKey.GenerateKey();
        var clientKey = EthECKey.GenerateKey();
        var serverGenesis = FakeGenesis(salt: 0xC0);
        var clientGenesis = FakeGenesis(salt: 0xC0);
        await using var bundle = InMemoryChainStoreBundle.Open();

        var serverTemplate = StatusFor(7777, serverGenesis, serverGenesis);

        var options = new PeerListenerOptions
        {
            ListenPort = 0,
            BindAddress = IPAddress.Loopback,
            MaxInboundPeers = 5,
            MaxInboundPerIP = 3,
            ServeSnap = false,
            MirrorRemoteStatus = false,
            ClientId = "Nethereum.Spec.Tests/empty-chain-sentinel"
        };

        await using var server = new PeerListener(serverKey, bundle, options, serverTemplate);
        await server.StartAsync();

        var clientConfig = new DevP2PConfig
        {
            ClientId = "Nethereum.Spec.Tests.client/empty-chain-sentinel",
            ConnectTimeoutMs = 5000,
            HandshakeTimeoutMs = 5000,
            NetworkId = serverTemplate.NetworkId,
            GenesisHash = clientGenesis
        };

        using var connection = new RlpxConnection(clientKey, clientConfig);
        using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await connection.ConnectAsync("127.0.0.1", server.Port, serverKey.GetPubKeyNoPrefix(), ct.Token);

        var clientStatus = Eth69StatusFor(serverTemplate.NetworkId, clientGenesis, clientGenesis);

        var ethOffset = connection.GetCapabilityOffset("eth");
        await connection.SendMessageAsync(
            ethOffset + Eth68MessageIds.Status,
            Eth69StatusMessageEncoder.Encode(clientStatus),
            ct.Token);

        var (msgId, payload) = await connection.ReceiveMessageAsync(ct.Token);
        Assert.Equal(ethOffset + Eth68MessageIds.Status, msgId);

        var serverReply = Eth69StatusMessageEncoder.Decode(payload);
        var negotiatedEth = connection.SharedCapabilities.Find(c => c.Name == "eth").Version;
        Assert.Equal(negotiatedEth, serverReply.ProtocolVersion);

        Assert.Equal(0UL, serverReply.LatestBlock);
    }
}
