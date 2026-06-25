using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv4;
using Nethereum.DevP2P.Discv5;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.Enr;
using Nethereum.Signer;
using Nethereum.Signer.Enr;
using Xunit;
using Xunit.Abstractions;

#nullable enable

namespace Nethereum.DevP2P.SpecTests.Discovery
{
    /// <summary>
    /// discv4 / discv5 UDP protocol-coverage tests against a live mainnet peer.
    /// These do NOT open the TCP DevP2P session — they bind a local UDP
    /// listener and send raw discovery packets to the peer's UDP port.
    /// <para>
    /// The peer's enode URL gives us the 64-byte uncompressed secp256k1 public
    /// key (everything after <c>enode://</c> and before <c>@</c>) — discv4
    /// needs only the endpoint, discv5 derives the 32-byte node id and uses
    /// the compressed pubkey for ECDH.
    /// </para>
    /// </summary>
    [Trait("Category", "Integration-Peer")]
    public class MainnetPeerDiscoveryTests
    {
        private const int DefaultDevP2PPort = 30303;
        private const int DefaultJsonRpcPort = 8545;
        private const int DefaultDiscv4Port = 30303;
        private const int DefaultDiscv5Port = 30303;
        private static readonly TimeSpan UdpProbeTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan ListenerSettleDelay = TimeSpan.FromMilliseconds(200);

        private readonly ITestOutputHelper _output;

        public MainnetPeerDiscoveryTests(ITestOutputHelper output) { _output = output; }

        [SkippableFact]
        public async Task Discv5_PingAsync_RoundTrip()
        {
            var ctx = await BuildDiscoveryContextOrSkipAsync();

            var localKey = EthECKey.GenerateKey();
            await using var listener = new Discv5Listener(localKey);
            listener.Start(IPAddress.Any, port: 0);
            listener.LocalEnrEncoded = BuildLocalEnrEncoded(localKey, (ushort)listener.Port);
            listener.LocalEnrSequence = 1;
            await Task.Delay(ListenerSettleDelay);

            var peerNodeId = Discv5Crypto.ComputeNodeId(ctx.PeerUncompressedPubKey);
            var peerCompressedPub = Discv5Crypto.CompressXy(ctx.PeerUncompressedPubKey);
            _output.WriteLine($"  discv5 ping -> {ctx.Discv5Endpoint} nodeId=0x{peerNodeId.ToHex().Substring(0, 16)}…");

            using var cts = new CancellationTokenSource(RequestTimeout + TimeSpan.FromSeconds(5));
            Discv5PongMessage pong;
            try
            {
                pong = await listener.SendPingAsync(
                    ctx.Discv5Endpoint, peerNodeId, peerCompressedPub, RequestTimeout, cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                Skip.If(true, $"discv5 PING to {ctx.Discv5Endpoint} timed out — peer may not run discv5 or UDP {DefaultDiscv5Port} unreachable");
                throw;
            }

            Assert.NotNull(pong);
            _output.WriteLine($"  pong: enrSeq={pong.EnrSeq} recipientIp={new IPAddress(pong.RecipientIp)} recipientPort={pong.RecipientPort}");
            Assert.True(pong.EnrSeq > 0, "peer must advertise a non-zero ENR sequence in PONG");
            Assert.NotNull(pong.RecipientIp);
            Assert.True(pong.RecipientIp.Length == 4 || pong.RecipientIp.Length == 16,
                $"recipient ip must be 4 or 16 bytes, got {pong.RecipientIp.Length}");
            Assert.True(pong.RecipientPort > 0, "peer must echo a non-zero recipient port");
        }

        [SkippableFact]
        public async Task Discv5_FindNodeAsync_AtDistance256_ReturnsEnrs()
        {
            var ctx = await BuildDiscoveryContextOrSkipAsync();

            var localKey = EthECKey.GenerateKey();
            await using var listener = new Discv5Listener(localKey);
            listener.Start(IPAddress.Any, port: 0);
            listener.LocalEnrEncoded = BuildLocalEnrEncoded(localKey, (ushort)listener.Port);
            listener.LocalEnrSequence = 1;
            await Task.Delay(ListenerSettleDelay);

            var peerNodeId = Discv5Crypto.ComputeNodeId(ctx.PeerUncompressedPubKey);
            var peerCompressedPub = Discv5Crypto.CompressXy(ctx.PeerUncompressedPubKey);
            _output.WriteLine($"  discv5 findnode distances=[256] -> {ctx.Discv5Endpoint}");

            using var cts = new CancellationTokenSource(RequestTimeout + TimeSpan.FromSeconds(5));
            List<EnrRecord> enrs;
            try
            {
                enrs = await listener.SendFindNodeAsync(
                    ctx.Discv5Endpoint, peerNodeId, peerCompressedPub,
                    new uint[] { 256 }, RequestTimeout, cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                Skip.If(true, $"discv5 FINDNODE to {ctx.Discv5Endpoint} timed out — peer may not run discv5 or UDP {DefaultDiscv5Port} unreachable");
                throw;
            }

            _output.WriteLine($"  nodes returned: {enrs.Count}");
            Assert.NotEmpty(enrs);
            foreach (var enr in enrs)
            {
                Assert.NotNull(enr.Secp256k1);
                Assert.Equal(33, enr.Secp256k1.Length);
                _output.WriteLine($"    enr: seq={enr.Sequence} ip={enr.IP4} udp={enr.UdpPort} tcp={enr.TcpPort}");
            }
        }

        [SkippableFact]
        public async Task Discv4_PingAsync_RoundTrip()
        {
            var ctx = await BuildDiscoveryContextOrSkipAsync();

            var localKey = EthECKey.GenerateKey();
            var routing = new Discv4RoutingTable(localKey.GetPubKeyNoPrefix());
            using var listener = new Discv4Listener(localKey, routing) { AutoRespond = true };
            listener.Start(udpPort: 0, bindAddress: IPAddress.Any);
            await Task.Delay(ListenerSettleDelay);

            var pongReceived = new TaskCompletionSource<Discv4PongReceivedEventArgs>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            listener.PongReceived += (_, e) =>
            {
                if (e.Sender.Address.Equals(ctx.Discv4Endpoint.Address))
                    pongReceived.TrySetResult(e);
            };

            var localEp = listener.LocalEndpoint;
            var ping = new Discv4PingMessage
            {
                Version = 4,
                From = new Discv4Endpoint
                {
                    IP = LocalIPv4ForRemote(ctx.Discv4Endpoint.Address) ?? IPAddress.Loopback,
                    UdpPort = (ushort)localEp.Port,
                    TcpPort = (ushort)localEp.Port
                },
                To = new Discv4Endpoint
                {
                    IP = ctx.Discv4Endpoint.Address,
                    UdpPort = (ushort)ctx.Discv4Endpoint.Port,
                    TcpPort = 0
                },
                Expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds()
            };

            _output.WriteLine($"  discv4 ping -> {ctx.Discv4Endpoint} from local UDP :{localEp.Port}");
            using var cts = new CancellationTokenSource(RequestTimeout + TimeSpan.FromSeconds(5));
            await listener.SendPingAsync(ctx.Discv4Endpoint, ping, cts.Token);

            var winner = await Task.WhenAny(pongReceived.Task, Task.Delay(RequestTimeout, cts.Token));
            Skip.If(winner != pongReceived.Task,
                $"discv4 PONG from {ctx.Discv4Endpoint} not received within {RequestTimeout.TotalSeconds:F0}s — peer may not run discv4 or UDP unreachable");

            var ev = await pongReceived.Task;
            _output.WriteLine($"  pong: from={ev.Sender} pingHash=0x{ev.Pong.PingHash.ToHex().Substring(0, 16)}… enrSeq={ev.Pong.EnrSeq}");
            Assert.NotNull(ev.Pong.PingHash);
            Assert.Equal(32, ev.Pong.PingHash.Length);
            Assert.True(ev.Pong.Expiration > DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                "pong.expiration must be in the future");
        }

        [SkippableFact]
        public async Task Discv4_FindNodeAsync_ReturnsNeighbours()
        {
            var ctx = await BuildDiscoveryContextOrSkipAsync();

            var localKey = EthECKey.GenerateKey();
            var routing = new Discv4RoutingTable(localKey.GetPubKeyNoPrefix());
            using var listener = new Discv4Listener(localKey, routing) { AutoRespond = true };
            listener.Start(udpPort: 0, bindAddress: IPAddress.Any);
            await Task.Delay(ListenerSettleDelay);

            // Bond first: spec mandates peers only answer FINDNODE after a
            // completed PING/PONG endpoint proof (amplification defence).
            // Without this geth silently drops the FINDNODE.
            var bondPingHash = await CompleteBondAsync(listener, ctx, RequestTimeout);
            Skip.If(bondPingHash == null,
                $"could not bond with {ctx.Discv4Endpoint} (no PONG within {RequestTimeout.TotalSeconds:F0}s) — peer may not run discv4");

            var neighborsReceived = new TaskCompletionSource<Discv4NeighborsReceivedEventArgs>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            listener.NeighborsReceived += (_, e) =>
            {
                if (e.Sender.Address.Equals(ctx.Discv4Endpoint.Address))
                    neighborsReceived.TrySetResult(e);
            };

            var target = new byte[64];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                rng.GetBytes(target);
            var findNode = new Discv4FindNodeMessage
            {
                Target = target,
                Expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds()
            };

            _output.WriteLine($"  discv4 findnode -> {ctx.Discv4Endpoint} target=0x{target.ToHex().Substring(0, 16)}…");
            using var cts = new CancellationTokenSource(RequestTimeout + TimeSpan.FromSeconds(5));
            await listener.SendFindNodeAsync(ctx.Discv4Endpoint, findNode, cts.Token);

            var winner = await Task.WhenAny(neighborsReceived.Task, Task.Delay(RequestTimeout, cts.Token));
            Skip.If(winner != neighborsReceived.Task,
                $"discv4 NEIGHBOURS from {ctx.Discv4Endpoint} not received within {RequestTimeout.TotalSeconds:F0}s");

            var ev = await neighborsReceived.Task;
            _output.WriteLine($"  neighbours: count={ev.Neighbors.Nodes.Count}");
            Assert.NotNull(ev.Neighbors);
            Assert.NotEmpty(ev.Neighbors.Nodes);
            foreach (var n in ev.Neighbors.Nodes)
            {
                Assert.NotNull(n.IP);
                Assert.True(n.UdpPort > 0, "neighbour must advertise a non-zero UDP port");
                Assert.NotNull(n.NodeId);
                Assert.Equal(64, n.NodeId.Length);
            }
        }

        private async Task<byte[]?> CompleteBondAsync(
            Discv4Listener listener, DiscoveryContext ctx, TimeSpan timeout)
        {
            var pongReceived = new TaskCompletionSource<Discv4PongReceivedEventArgs>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<Discv4PongReceivedEventArgs> handler = (_, e) =>
            {
                if (e.Sender.Address.Equals(ctx.Discv4Endpoint.Address))
                    pongReceived.TrySetResult(e);
            };
            listener.PongReceived += handler;
            try
            {
                var localEp = listener.LocalEndpoint;
                var ping = new Discv4PingMessage
                {
                    Version = 4,
                    From = new Discv4Endpoint
                    {
                        IP = LocalIPv4ForRemote(ctx.Discv4Endpoint.Address) ?? IPAddress.Loopback,
                        UdpPort = (ushort)localEp.Port,
                        TcpPort = (ushort)localEp.Port
                    },
                    To = new Discv4Endpoint
                    {
                        IP = ctx.Discv4Endpoint.Address,
                        UdpPort = (ushort)ctx.Discv4Endpoint.Port,
                        TcpPort = 0
                    },
                    Expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds()
                };
                await listener.SendPingAsync(ctx.Discv4Endpoint, ping);

                var winner = await Task.WhenAny(pongReceived.Task, Task.Delay(timeout));
                if (winner != pongReceived.Task) return null;
                var ev = await pongReceived.Task;

                // Peer's auto-back-ping should arrive next; AutoRespond=true on
                // our listener answers it, completing the symmetric proof.
                // A short settle window covers wire jitter.
                await Task.Delay(150);
                return ev.Pong.PingHash;
            }
            finally
            {
                listener.PongReceived -= handler;
            }
        }

        private async Task<DiscoveryContext> BuildDiscoveryContextOrSkipAsync()
        {
            var host = Environment.GetEnvironmentVariable("PEER_HOST")?.Trim();
            Skip.If(string.IsNullOrEmpty(host),
                "PEER_HOST env var not set — skipping live peer integration. Set PEER_HOST=<addr> to enable.");

            var discv4Port = int.TryParse(Environment.GetEnvironmentVariable("PEER_DISCV4_PORT"), out var p4)
                ? p4 : DefaultDiscv4Port;
            var discv5Port = int.TryParse(Environment.GetEnvironmentVariable("PEER_DISCV5_PORT"), out var p5)
                ? p5 : DefaultDiscv5Port;
            var devp2pPort = int.TryParse(Environment.GetEnvironmentVariable("PEER_DEVP2P_PORT"), out var pd)
                ? pd : DefaultDevP2PPort;
            var rpcHost = Environment.GetEnvironmentVariable("PEER_RPC_HOST")?.Trim();
            if (string.IsNullOrEmpty(rpcHost)) rpcHost = host;
            var rpcPort = int.TryParse(Environment.GetEnvironmentVariable("PEER_RPC_PORT"), out var pr)
                ? pr : DefaultJsonRpcPort;

            _output.WriteLine($"Target: discv4 UDP {host}:{discv4Port}, discv5 UDP {host}:{discv5Port}");
            _output.WriteLine("Override PEER_HOST, PEER_DISCV4_PORT, PEER_DISCV5_PORT, PEER_ENODE to retarget.");

            var ipAddress = await ResolveSingleIpAsync(host!);

            var enodeUrl = Environment.GetEnvironmentVariable("PEER_ENODE")?.Trim();
            if (string.IsNullOrEmpty(enodeUrl))
            {
                enodeUrl = await TryGetEnodeViaAdminAsync(rpcHost!, rpcPort, ipAddress, devp2pPort);
            }
            Skip.If(string.IsNullOrEmpty(enodeUrl),
                "No usable enode — admin_nodeInfo is not exposed on this RPC. " +
                "Set PEER_ENODE=enode://<nodeid>@<host>:<port> to enable discv4/discv5 dial.");

            var (peerPub, _, _) = ParseEnode(enodeUrl!);

            return new DiscoveryContext
            {
                Host = host!,
                PeerUncompressedPubKey = peerPub,
                Discv4Endpoint = new IPEndPoint(ipAddress, discv4Port),
                Discv5Endpoint = new IPEndPoint(ipAddress, discv5Port)
            };
        }

        private static async Task<IPAddress> ResolveSingleIpAsync(string host)
        {
            if (IPAddress.TryParse(host, out var literal)) return literal;
            var addresses = await System.Net.Dns.GetHostAddressesAsync(host);
            if (addresses.Length == 0)
                throw new InvalidOperationException($"DNS resolution returned no addresses for '{host}'");
            return addresses[0];
        }

        private static async Task<string?> TryGetEnodeViaAdminAsync(
            string rpcHost, int rpcPort, IPAddress targetIp, int targetPort)
        {
            try
            {
                var web3 = new Nethereum.Geth.Web3Geth($"http://{rpcHost}:{rpcPort}");
                var nodeInfo = await web3.Admin.NodeInfo.SendRequestAsync();
                var enode = nodeInfo?["enode"]?.ToString();
                if (string.IsNullOrEmpty(enode)) return null;
                var atIdx = enode!.IndexOf('@');
                if (atIdx < 0) return enode;
                return $"{enode.Substring(0, atIdx)}@{targetIp}:{targetPort}";
            }
            catch
            {
                return null;
            }
        }

        private static (byte[] PubKeyUncompressed, string Host, int Port) ParseEnode(string enode)
        {
            const string prefix = "enode://";
            if (!enode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Not an enode URL", nameof(enode));
            var rest = enode.Substring(prefix.Length);
            var atIdx = rest.IndexOf('@');
            var pubHex = rest.Substring(0, atIdx);
            var hostPort = rest.Substring(atIdx + 1);
            var colon = hostPort.IndexOf(':');
            var host = hostPort.Substring(0, colon);
            var port = int.Parse(hostPort.Substring(colon + 1));
            var pub = pubHex.HexToByteArray();
            if (pub.Length != 64)
                throw new FormatException(
                    $"enode pubkey must be 64 bytes (uncompressed secp256k1 x||y), got {pub.Length}");
            return (pub, host, port);
        }

        private static byte[] BuildLocalEnrEncoded(EthECKey key, ushort udpPort)
        {
            var enr = new EnrRecord { Sequence = 1 };
            enr.Pairs["id"] = System.Text.Encoding.ASCII.GetBytes("v4");
            enr.Pairs["secp256k1"] = key.GetPubKey(compresseed: true);
            enr.Pairs["udp"] = new[] { (byte)((udpPort >> 8) & 0xff), (byte)(udpPort & 0xff) };
            EnrRecordSigner.Sign(enr, key);
            return EnrRecordEncoder.EncodeRecord(enr);
        }

        private static IPAddress? LocalIPv4ForRemote(IPAddress remote)
        {
            try
            {
                using var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                sock.Connect(remote, 65530);
                if (sock.LocalEndPoint is IPEndPoint local) return local.Address;
            }
            catch
            {
            }
            return null;
        }

        private sealed class DiscoveryContext
        {
            public string Host { get; init; } = "";
            public byte[] PeerUncompressedPubKey { get; init; } = Array.Empty<byte>();
            public IPEndPoint Discv4Endpoint { get; init; } = null!;
            public IPEndPoint Discv5Endpoint { get; init; } = null!;
        }
    }
}
