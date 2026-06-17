using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync;
using Nethereum.Geth;
using Nethereum.RPC.Web3;
using Xunit;
using Xunit.Abstractions;

#nullable enable

namespace Nethereum.DevP2P.SpecTests.Eth69
{
    /// <summary>
    /// Shared bootstrap for the live mainnet-peer DevP2P integration tests.
    /// Each derived test class skips when <c>PEER_HOST</c> is unset, probes
    /// TCP, fetches the peer's head + clientVersion over JSON-RPC, resolves
    /// the enode (env var or <c>admin_nodeInfo</c>) and dials the DevP2P
    /// stack. Tests then invoke the body of their scenario via
    /// <see cref="RunWithSessionAsync"/>.
    /// </summary>
    [Trait("Category", "Integration-Peer")]
    public abstract class MainnetPeerTestBase
    {
        protected const int DefaultDevP2PPort = 30303;
        protected const int DefaultJsonRpcPort = 8545;
        protected static readonly TimeSpan TcpProbeTimeout = TimeSpan.FromSeconds(5);
        protected static readonly TimeSpan SessionTimeout = TimeSpan.FromSeconds(60);

        protected readonly ITestOutputHelper Output;

        protected MainnetPeerTestBase(ITestOutputHelper output)
        {
            Output = output;
        }

        protected sealed class PeerContext
        {
            public string Host { get; init; } = "";
            public string RpcHost { get; init; } = "";
            public int DevP2PPort { get; init; }
            public int RpcPort { get; init; }
            public string EnodeUrl { get; init; } = "";
            public ulong PeerHead { get; init; }
            public string ClientVersion { get; init; } = "";
            public Web3Geth Web3 { get; init; } = null!;
        }

        protected async Task RunWithSessionAsync(Func<MainnetPeerSession, PeerContext, CancellationToken, Task> body)
        {
            var ctx = await BuildContextOrSkipAsync();
            using var sessionCts = new CancellationTokenSource(SessionTimeout);

            Output.WriteLine($"Dialing {MainnetPeerSession.ParseHost(ctx.EnodeUrl)} via RLPx + p2p Hello + eth Status …");
            await using var session = await MainnetPeerSession.ConnectAsync(ctx.EnodeUrl, SessionTimeout, sessionCts.Token);

            Output.WriteLine($"  connected: ClientId='{session.PeerClientId}' eth/{session.EthVersion} " +
                $"peerLatest={session.PeerLatestBlock} peerForkHash=0x{session.PeerForkHash:x8}");

            Assert.True(session.EthVersion >= 68, $"Expected eth/68 or higher, got eth/{session.EthVersion}");
            Assert.True(session.Connection.IsConnected);
            Assert.NotEmpty(session.Connection.SharedCapabilities);

            await body(session, ctx, sessionCts.Token);
        }

        protected async Task<PeerContext> BuildContextOrSkipAsync()
        {
            var host = Environment.GetEnvironmentVariable("PEER_HOST")?.Trim();
            Skip.If(string.IsNullOrEmpty(host),
                "PEER_HOST env var not set — skipping live peer integration. Set PEER_HOST=<addr> to enable.");

            var rpcHost = Environment.GetEnvironmentVariable("PEER_RPC_HOST")?.Trim();
            if (string.IsNullOrEmpty(rpcHost)) rpcHost = host;

            var rpcPort = int.TryParse(Environment.GetEnvironmentVariable("PEER_RPC_PORT"), out var parsedRpc)
                ? parsedRpc : DefaultJsonRpcPort;
            var devp2pPort = int.TryParse(Environment.GetEnvironmentVariable("PEER_DEVP2P_PORT"), out var parsedDev)
                ? parsedDev : DefaultDevP2PPort;

            Output.WriteLine($"Target: DevP2P {host}:{devp2pPort}, JSON-RPC {rpcHost}:{rpcPort}");
            Output.WriteLine("Override PEER_HOST, PEER_RPC_HOST, PEER_DEVP2P_PORT, PEER_RPC_PORT, PEER_ENODE to retarget.");

            Skip.IfNot(await ProbeTcpAsync(host!, devp2pPort, TcpProbeTimeout),
                $"DevP2P at {host}:{devp2pPort} not reachable within {TcpProbeTimeout.TotalSeconds:F0}s.");
            Output.WriteLine($"TCP probe {host}:{devp2pPort} OK");

            var web3 = new Web3Geth($"http://{rpcHost}:{rpcPort}");

            ulong head;
            string clientVersion;
            try
            {
                var headHex = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                head = (ulong)headHex.Value;
                clientVersion = await new Web3ClientVersion(web3.Client).SendRequestAsync() ?? "<unknown>";
            }
            catch (Exception ex)
            {
                Skip.If(true, $"JSON-RPC at {rpcHost}:{rpcPort} unreachable: {ex.Message}");
                throw;
            }
            Output.WriteLine($"Peer clientVersion = {clientVersion}");
            Output.WriteLine($"Peer head block = {head}");

            var enodeUrl = Environment.GetEnvironmentVariable("PEER_ENODE")?.Trim();
            if (string.IsNullOrEmpty(enodeUrl))
            {
                enodeUrl = await TryGetEnodeAsync(web3, host!, devp2pPort);
            }
            Skip.If(string.IsNullOrEmpty(enodeUrl),
                "No usable enode — admin_nodeInfo is not exposed on this RPC. " +
                "Set PEER_ENODE=enode://<nodeid>@<host>:<port> to enable DevP2P dial.");

            Output.WriteLine($"Enode = {Truncate(enodeUrl!, 90)}");

            return new PeerContext
            {
                Host = host!,
                RpcHost = rpcHost!,
                DevP2PPort = devp2pPort,
                RpcPort = rpcPort,
                EnodeUrl = enodeUrl!,
                PeerHead = head,
                ClientVersion = clientVersion,
                Web3 = web3
            };
        }

        protected static async Task<bool> ProbeTcpAsync(string host, int port, TimeSpan timeout)
        {
            try
            {
                using var client = new TcpClient();
                using var cts = new CancellationTokenSource(timeout);
                var connectTask = client.ConnectAsync(host, port);
                var timeoutTask = Task.Delay(Timeout.InfiniteTimeSpan, cts.Token);
                if (await Task.WhenAny(connectTask, timeoutTask) != connectTask) return false;
                await connectTask;
                return client.Connected;
            }
            catch
            {
                return false;
            }
        }

        protected static async Task<string?> TryGetEnodeAsync(Web3Geth web3, string targetHost, int targetPort)
        {
            try
            {
                var nodeInfo = await web3.Admin.NodeInfo.SendRequestAsync();
                var enode = nodeInfo?["enode"]?.ToString();
                if (string.IsNullOrEmpty(enode)) return null;
                return NormalizeEnodeHost(enode!, targetHost, targetPort);
            }
            catch
            {
                return null;
            }
        }

        protected static string NormalizeEnodeHost(string enode, string targetHost, int targetPort)
        {
            const string prefix = "enode://";
            if (!enode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return enode;
            var atIdx = enode.IndexOf('@');
            if (atIdx < 0) return enode;
            return $"{enode.Substring(0, atIdx)}@{targetHost}:{targetPort}";
        }

        protected static string Truncate(string s, int maxLen)
            => string.IsNullOrEmpty(s) || s.Length <= maxLen ? s : s.Substring(0, maxLen) + "…";
    }
}
