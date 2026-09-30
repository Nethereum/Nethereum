using System;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;
using Nethereum.DevP2P.Sync.Serving;

namespace Nethereum.DevP2P.IntegrationTests;

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
            ClientId = "Nethereum.Sync.Tests/1"
        };

        await using var server = new PeerListener(serverKey, bundle, serverOptions, statusTemplate);
        await server.StartAsync();

        var clientConfig = new DevP2PConfig
        {
            ClientId = "Nethereum.Sync.Tests.client/1",
            ConnectTimeoutMs = 5000,
            HandshakeTimeoutMs = 5000,
            NetworkId = statusTemplate.NetworkId,
            GenesisHash = genesis
        };

        using var connection = new RlpxConnection(clientKey, clientConfig);
        using var ct = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await connection.ConnectAsync("127.0.0.1", server.Port, serverKey.GetPubKeyNoPrefix(), ct.Token);

        var ethOffset = connection.GetCapabilityOffset("eth");
        var ethVersion = connection.SharedCapabilities.Find(c => c.Name == "eth").Version;
        var clientStatus = new Eth68StatusMessage
        {
            ProtocolVersion = ethVersion,
            NetworkId = statusTemplate.NetworkId,
            TotalDifficulty = BigInteger.One,
            BestHash = genesis,
            GenesisHash = genesis,
            ForkHash = statusTemplate.ForkHash,
            ForkNext = 0
        };
        await connection.SendMessageAsync(
            ethOffset + Eth68MessageIds.Status,
            EncodeStatusForVersion(ethVersion, clientStatus),
            ct.Token);

        var (msgId, payload) = await connection.ReceiveMessageAsync(ct.Token);
        Assert.Equal(ethOffset + Eth68MessageIds.Status, msgId);
        var serverStatus = DecodeStatusForVersion(ethVersion, payload);
        Assert.Equal((ulong)statusTemplate.NetworkId, serverStatus.NetworkId);
        Assert.Equal(genesis, serverStatus.GenesisHash);
    }

    private static byte[] EncodeStatusForVersion(int version, Eth68StatusMessage s)
    {
        if (version >= 69)
            return Eth69StatusMessageEncoder.Encode(new Eth69StatusMessage
            {
                ProtocolVersion = version,
                NetworkId = s.NetworkId,
                GenesisHash = s.GenesisHash,
                ForkHash = s.ForkHash,
                ForkNext = s.ForkNext,
                EarliestBlock = 0,
                LatestBlock = 0,
                LatestBlockHash = s.BestHash,
            });
        return Eth68StatusMessageEncoder.Encode(s);
    }

    private static Eth68StatusMessage DecodeStatusForVersion(int version, byte[] payload)
    {
        if (version >= 69)
        {
            var s = Eth69StatusMessageEncoder.Decode(payload);
            return new Eth68StatusMessage
            {
                ProtocolVersion = s.ProtocolVersion,
                NetworkId = s.NetworkId,
                GenesisHash = s.GenesisHash,
                ForkHash = s.ForkHash,
                ForkNext = s.ForkNext,
                BestHash = s.LatestBlockHash,
            };
        }
        return Eth68StatusMessageEncoder.Decode(payload);
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
            ClientId = "Nethereum.Sync.Tests/perip"
        };

        await using var server = new PeerListener(serverKey, bundle, options, statusTemplate);
        await server.StartAsync();

        var holders = new TcpClient[3];
        try
        {
            for (int i = 0; i < holders.Length; i++)
            {
                holders[i] = new TcpClient();
                await holders[i].ConnectAsync("127.0.0.1", server.Port);
            }

            await Task.Delay(200);

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
                read = 0;
            }

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
            HandshakeTimeoutMs = 750,
            ClientId = "Nethereum.Sync.Tests/handshake-timeout"
        };

        await using var server = new PeerListener(serverKey, bundle, options, statusTemplate);
        await server.StartAsync();

        using var rogue = new TcpClient();
        await rogue.ConnectAsync("127.0.0.1", server.Port);

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
