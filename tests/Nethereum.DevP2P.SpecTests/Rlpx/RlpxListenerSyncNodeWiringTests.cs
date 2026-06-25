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

namespace Nethereum.DevP2P.SpecTests.Rlpx;

/// <summary>
/// SyncNode-wiring scenarios for <see cref="PeerListener"/> covering
/// task #135 (RlpxListener wired by SyncNode, lifecycle hookable into a
/// DialScheduler ratio counter) and task #217 (serve-empty mode — accept
/// peers regardless of our sync state). Each test is loopback-only, no
/// external Geth dependency.
/// </summary>
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

    [Fact]
    public async Task ListenPortZero_BindsOsAssignedPositivePort()
    {
        // #135 spec: ListenPort=0 means OS-ephemeral. After Start, the
        // PeerListener must expose a positive bound port that SyncNode can
        // advertise in the ENR.
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
        // #135 spec: the inbound add path must be routable to a
        // DialScheduler-style ratio counter. PeerListenerOptions exposes
        // OnInboundPeerAdded / OnInboundPeerRemoved for that. Both must
        // fire 1:1 with the same peer key.
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
            Eth68StatusMessageEncoder.Encode(StatusFor(template.NetworkId, genesis, genesis)),
            ct.Token);
        // Drain the server's Status reply so the listener's lifecycle Added
        // hook actually fires (it runs after the Status round-trip).
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
        // #217 spec: with serve-empty on (PeerListener.MirrorRemoteStatus=true)
        // an inbound peer at head=N connecting to a node at head=0 (fresh) is
        // admitted. Both sides complete Status exchange and the peer accepts
        // our reply because we echo its chain identifiers.
        var serverKey = EthECKey.GenerateKey();
        var clientKey = EthECKey.GenerateKey();
        var serverGenesis = FakeGenesis(salt: 0xC0);
        var clientGenesis = FakeGenesis(salt: 0xC0); // same chain, same genesis
        await using var bundle = InMemoryChainStoreBundle.Open();

        // Server uses genesis-only template (fresh, head==genesis)
        var serverTemplate = StatusFor(7777, serverGenesis, serverGenesis);

        var options = new PeerListenerOptions
        {
            ListenPort = 0,
            BindAddress = IPAddress.Loopback,
            MaxInboundPeers = 5,
            MaxInboundPerIP = 3,
            ServeSnap = false,
            MirrorRemoteStatus = true, // serve-empty ON
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

        // Client pretends to be at head 12345 with a fake BestHash —
        // distinctly NOT genesis. With serve-empty on, server still admits.
        var clientHead = new byte[32];
        for (int i = 0; i < 32; i++) clientHead[i] = (byte)(0xAB ^ i);
        var clientStatus = StatusFor(serverTemplate.NetworkId, clientGenesis, clientHead);
        clientStatus.TotalDifficulty = new BigInteger(123_456_789);

        var ethOffset = connection.GetCapabilityOffset("eth");
        await connection.SendMessageAsync(
            ethOffset + Eth68MessageIds.Status,
            Eth68StatusMessageEncoder.Encode(clientStatus),
            ct.Token);

        var (msgId, payload) = await connection.ReceiveMessageAsync(ct.Token);
        Assert.Equal(ethOffset + Eth68MessageIds.Status, msgId);
        var serverReply = Eth68StatusMessageEncoder.Decode(payload);

        // Server mirrored our chain identifiers — BestHash echoes clientHead,
        // GenesisHash echoes our clientGenesis. The peer accepts this.
        Assert.Equal(clientHead, serverReply.BestHash);
        Assert.Equal(clientGenesis, serverReply.GenesisHash);
        Assert.Equal(clientStatus.TotalDifficulty, serverReply.TotalDifficulty);
    }

    [Fact]
    public async Task ServeEmpty_False_AssertsOurOwnStatusInsteadOfMirroring()
    {
        // #217 spec: with serve-empty off (MirrorRemoteStatus=false) the
        // listener asserts its own chain identifiers — so a peer at a
        // different head sees the truthful gap in our Status reply and can
        // decide on its own whether to continue. The peer is still ADMITTED
        // by the listener (no per-protocol "you're ahead, go away" gate); the
        // peer-side rejection is its own decision.
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
            MirrorRemoteStatus = false, // serve-empty OFF
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
        var clientStatus = StatusFor(serverTemplate.NetworkId, clientGenesis, clientHead);
        clientStatus.TotalDifficulty = new BigInteger(987_654_321);

        var ethOffset = connection.GetCapabilityOffset("eth");
        await connection.SendMessageAsync(
            ethOffset + Eth68MessageIds.Status,
            Eth68StatusMessageEncoder.Encode(clientStatus),
            ct.Token);

        var (msgId, payload) = await connection.ReceiveMessageAsync(ct.Token);
        Assert.Equal(ethOffset + Eth68MessageIds.Status, msgId);
        var serverReply = Eth68StatusMessageEncoder.Decode(payload);

        // Server asserts its OWN values — not the mirrored clientHead /
        // 987M TD. This is the truthful "I'm at genesis" reply that a peer
        // running without serve-empty would see.
        Assert.Equal(serverTemplate.BestHash, serverReply.BestHash);
        Assert.Equal(serverTemplate.GenesisHash, serverReply.GenesisHash);
        Assert.Equal(serverTemplate.TotalDifficulty, serverReply.TotalDifficulty);
        Assert.NotEqual(clientHead, serverReply.BestHash);
    }
}
