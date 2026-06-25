using System;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Serve;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.Serve.SpecTests;

/// <summary>
/// End-to-end tests for the <see cref="PeerListener"/> composition: two
/// listeners on loopback round-trip an eth/68 Status; per-IP throttle
/// rejects when the cap is hit; handshake timeout fires when the peer
/// never sends auth.
/// </summary>
public class PeerListenerTests
{
    private static byte[] FakeGenesis()
    {
        var g = new byte[32];
        for (int i = 0; i < 32; i++) g[i] = (byte)(0xC0 ^ i);
        return g;
    }

    [Fact]
    public async Task TwoListeners_OnLoopback_RoundTripStatus()
    {
        var serverKey = EthECKey.GenerateKey();
        var clientKey = EthECKey.GenerateKey();
        var genesis = FakeGenesis();

        await using var bundle = InMemoryChainStoreBundle.Open();

        var statusTemplate = new Eth68StatusMessage
        {
            ProtocolVersion = 68,
            NetworkId = 7777,
            TotalDifficulty = BigInteger.One,
            BestHash = genesis,
            GenesisHash = genesis,
            ForkHash = 0xAABBCCDD,
            ForkNext = 0
        };

        var serverOptions = new PeerListenerOptions
        {
            ListenPort = 0,
            BindAddress = IPAddress.Loopback,
            MaxInboundPeers = 5,
            MaxInboundPerIP = 3,
            ServeSnap = false,
            MirrorRemoteStatus = false,
            ClientId = "Nethereum.Serve.Tests/1"
        };

        await using var server = new PeerListener(serverKey, bundle, serverOptions, statusTemplate);
        await server.StartAsync();

        var clientConfig = new DevP2PConfig
        {
            ClientId = "Nethereum.Serve.Tests.client/1",
            ConnectTimeoutMs = 5000,
            HandshakeTimeoutMs = 5000,
            NetworkId = statusTemplate.NetworkId,
            GenesisHash = genesis
        };

        using var connection = new RlpxConnection(clientKey, clientConfig);
        using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await connection.ConnectAsync("127.0.0.1", server.Port, serverKey.GetPubKeyNoPrefix(), ct.Token);

        // Client → server Status
        var ethOffset = connection.GetCapabilityOffset("eth");
        var clientStatus = new Eth68StatusMessage
        {
            ProtocolVersion = 68,
            NetworkId = statusTemplate.NetworkId,
            TotalDifficulty = BigInteger.One,
            BestHash = genesis,
            GenesisHash = genesis,
            ForkHash = statusTemplate.ForkHash,
            ForkNext = 0
        };
        await connection.SendMessageAsync(
            ethOffset + Eth68MessageIds.Status,
            Eth68StatusMessageEncoder.Encode(clientStatus),
            ct.Token);

        // Receive server's Status response
        var (msgId, payload) = await connection.ReceiveMessageAsync(ct.Token);
        Assert.Equal(ethOffset + Eth68MessageIds.Status, msgId);
        var serverStatus = Eth68StatusMessageEncoder.Decode(payload);
        Assert.Equal((ulong)statusTemplate.NetworkId, serverStatus.NetworkId);
        Assert.Equal(genesis, serverStatus.GenesisHash);
    }

    [Fact]
    public async Task PerIpThrottle_RejectsFourthConcurrentFromSameIp()
    {
        var serverKey = EthECKey.GenerateKey();
        var genesis = FakeGenesis();

        await using var bundle = InMemoryChainStoreBundle.Open();

        var statusTemplate = new Eth68StatusMessage
        {
            ProtocolVersion = 68,
            NetworkId = 8888,
            TotalDifficulty = BigInteger.One,
            BestHash = genesis,
            GenesisHash = genesis,
            ForkHash = 0u,
            ForkNext = 0
        };

        var options = new PeerListenerOptions
        {
            ListenPort = 0,
            BindAddress = IPAddress.Loopback,
            MaxInboundPeers = 50,
            MaxInboundPerIP = 3,
            ServeSnap = false,
            MirrorRemoteStatus = false,
            ClientId = "Nethereum.Serve.Tests/perip"
        };

        await using var server = new PeerListener(serverKey, bundle, options, statusTemplate);
        await server.StartAsync();

        // Open three sockets that hold themselves open without ever sending
        // auth — they occupy the per-IP slots without completing the
        // handshake (the listener's HandshakeTimeoutMs will eventually GC
        // them, but well after this test's check).
        var holders = new TcpClient[3];
        try
        {
            for (int i = 0; i < holders.Length; i++)
            {
                holders[i] = new TcpClient();
                await holders[i].ConnectAsync("127.0.0.1", server.Port);
            }

            // Give the listener a tick to register the three holders in its
            // per-IP counter (AcceptTcpClientAsync runs on a background loop).
            await Task.Delay(200);

            // Fourth socket from the same IP — the per-IP throttle MUST
            // close it before the handshake reads a byte. We detect this
            // by the server closing the connection during a read attempt.
            using var fourth = new TcpClient();
            await fourth.ConnectAsync("127.0.0.1", server.Port);
            var stream = fourth.GetStream();
            stream.ReadTimeout = 2_000;

            var buf = new byte[1];
            int read;
            try
            {
                read = await stream.ReadAsync(buf, 0, 1);
            }
            catch (System.IO.IOException)
            {
                read = 0; // socket reset by remote — also acceptable evidence
            }

            // Either EOF (0) or IOException indicates the server closed us.
            // A successful 1-byte read would mean the listener accepted us
            // past the cap, which the test asserts MUST NOT happen.
            Assert.Equal(0, read);
        }
        finally
        {
            foreach (var h in holders) try { h?.Close(); } catch { }
        }
    }

    [Fact]
    public async Task HandshakeTimeout_FiresWhenPeerNeverSendsAuth()
    {
        var serverKey = EthECKey.GenerateKey();
        var genesis = FakeGenesis();
        await using var bundle = InMemoryChainStoreBundle.Open();

        var statusTemplate = new Eth68StatusMessage
        {
            ProtocolVersion = 68,
            NetworkId = 9999,
            TotalDifficulty = BigInteger.One,
            BestHash = genesis,
            GenesisHash = genesis,
            ForkHash = 0u,
            ForkNext = 0
        };

        var options = new PeerListenerOptions
        {
            ListenPort = 0,
            BindAddress = IPAddress.Loopback,
            MaxInboundPeers = 5,
            MaxInboundPerIP = 3,
            ServeSnap = false,
            MirrorRemoteStatus = false,
            HandshakeTimeoutMs = 750, // short timeout to keep the test fast
            ClientId = "Nethereum.Serve.Tests/handshake-timeout"
        };

        await using var server = new PeerListener(serverKey, bundle, options, statusTemplate);
        await server.StartAsync();

        using var rogue = new TcpClient();
        await rogue.ConnectAsync("127.0.0.1", server.Port);

        // Don't send anything. After HandshakeTimeoutMs the server's
        // AcceptIncomingAsync cancels its read and tears down the socket.
        // We detect the close by reading and observing EOF or an
        // IOException (whichever the OS gives us).
        var stream = rogue.GetStream();
        stream.ReadTimeout = 5_000;
        var buf = new byte[1];
        int read;
        try
        {
            read = await stream.ReadAsync(buf, 0, 1);
        }
        catch (System.IO.IOException)
        {
            read = 0;
        }

        Assert.Equal(0, read);
    }
}
