using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv4;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    /// <summary>
    /// Validates our discv4 packet encoding and signing by sending PING to a
    /// real Geth 1.13.15 discv4 endpoint and verifying the PONG comes back
    /// signed by Geth's node key. The only spec-compliance proof we have for
    /// discv4 beyond round-trip self-consistency.
    /// </summary>
    public class Discv4GethInteropTests
    {
        private readonly ITestOutputHelper _output;

        public Discv4GethInteropTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task SendPing_ToGethDiscv4_ReceivePongSignedByGeth()
        {
            using var geth = await GethDiscv4Node.StartAsync(_output);
            _output.WriteLine($"Geth discv4 at {geth.UdpEndpoint}, node id prefix {geth.NodeId.ToHex().Substring(0, 16)}...");

            var localKey = EthECKey.GenerateKey();

            using var udp = new UdpClient(0, AddressFamily.InterNetwork);
            var localPort = ((IPEndPoint)udp.Client.LocalEndPoint).Port;

            var ping = new Discv4PingMessage
            {
                Version = 4,
                From = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = (ushort)localPort, TcpPort = (ushort)localPort },
                To = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = (ushort)geth.UdpEndpoint.Port, TcpPort = 0 },
                Expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds()
            };
            var pingData = Discv4MessageEncoder.EncodePing(ping);
            var pingPacket = Discv4Packet.Encode(localKey, Discv4MessageType.Ping, pingData);

            await udp.SendAsync(pingPacket, pingPacket.Length, geth.UdpEndpoint);
            _output.WriteLine($"Sent PING ({pingPacket.Length} bytes)");

            var receiveTask = udp.ReceiveAsync();
            var completed = await Task.WhenAny(receiveTask, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(receiveTask, completed);

            var result = receiveTask.Result;
            _output.WriteLine($"Received {result.Buffer.Length} bytes from {result.RemoteEndPoint}");

            var decoded = Discv4Packet.Decode(result.Buffer);
            _output.WriteLine($"Packet type {decoded.Type}, signed by {decoded.SenderPubKey.ToHex().Substring(0, 16)}...");

            Assert.True(
                decoded.Type == Discv4MessageType.Pong ||
                decoded.Type == Discv4MessageType.Ping,
                $"Expected PONG (or a peer-initiated PING) from Geth, got {decoded.Type}");

            Assert.True(
                ByteUtil.AreEqual(decoded.SenderPubKey, geth.NodeId),
                "PONG signature must recover to Geth's node id (proves the packet really came from Geth and our signature verification is spec-compliant)");

            if (decoded.Type == Discv4MessageType.Pong)
            {
                var pong = Discv4MessageEncoder.DecodePong(decoded.Data);
                _output.WriteLine($"PONG: to={pong.To.IP}:{pong.To.UdpPort}, pingHash={pong.PingHash.ToHex().Substring(0, 16)}...");

                var ourPingHash = ComputePingHash(pingPacket);
                Assert.Equal(ourPingHash.ToHex(), pong.PingHash.ToHex());
                _output.WriteLine("PONG correctly references the hash of our PING — spec compliant");
            }
        }

        private static byte[] ComputePingHash(byte[] packet)
        {
            var hash = new byte[32];
            Buffer.BlockCopy(packet, 0, hash, 0, 32);
            return hash;
        }

        private class GethDiscv4Node : IDisposable
        {
            private Process _process;
            private string _dataDir;

            public IPEndPoint UdpEndpoint { get; private set; }
            public byte[] NodeId { get; private set; }

            public static async Task<GethDiscv4Node> StartAsync(ITestOutputHelper output, CancellationToken ct = default)
            {
                var node = new GethDiscv4Node();
                node._dataDir = Path.Combine(Path.GetTempPath(), $"nethereum-discv4-geth-{Guid.NewGuid():N}");
                Directory.CreateDirectory(node._dataDir);

                int tcpPort = 30350;
                int udpPort = 30350;
                int rpcPort = 8556;

                var gethExe = FindGeth();
                var initArgs = $"--datadir=\"{node._dataDir}\" init \"{Path.Combine(Path.GetDirectoryName(gethExe), "..", "..", "testchain", "clique", "genesis_clique_modern.json")}\"";

                var resolvedGenesis = Path.GetFullPath(Path.Combine(
                    AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                    "testchain", "clique", "genesis_clique_modern.json"));

                using (var initProc = Process.Start(new ProcessStartInfo(gethExe,
                    $"--datadir=\"{node._dataDir}\" init \"{resolvedGenesis}\"")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true }))
                {
                    initProc.WaitForExit();
                }

                var args = string.Join(" ",
                    $"--datadir=\"{node._dataDir}\"",
                    "--discovery.v4",
                    $"--port {tcpPort}",
                    $"--discovery.port {udpPort}",
                    "--http",
                    $"--http.port {rpcPort}",
                    "--http.api eth,web3,net,admin",
                    "--http.addr 127.0.0.1",
                    "--mine",
                    "--miner.etherbase 0x12890d2cce102216644c59daE5baed380d84830c",
                    "--allow-insecure-unlock",
                    "--verbosity 1");

                node._process = new Process
                {
                    StartInfo = new ProcessStartInfo(gethExe, args)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true
                    }
                };
                node._process.Start();
                node._process.BeginErrorReadLine();

                await WaitForRpcAsync(rpcPort, ct);

                using var http = new System.Net.Http.HttpClient();
                var infoBody = "{\"jsonrpc\":\"2.0\",\"method\":\"admin_nodeInfo\",\"params\":[],\"id\":1}";
                var resp = await http.PostAsync($"http://127.0.0.1:{rpcPort}",
                    new System.Net.Http.StringContent(infoBody, System.Text.Encoding.UTF8, "application/json"), ct);
                var json = JObject.Parse(await resp.Content.ReadAsStringAsync(ct));
                var enode = json["result"]?["enode"]?.ToString() ?? throw new InvalidOperationException("Geth admin_nodeInfo missing enode");
                output.WriteLine($"Geth enode: {enode}");

                var nodeIdHex = enode.Substring("enode://".Length, 128);
                node.NodeId = nodeIdHex.HexToByteArray();
                node.UdpEndpoint = new IPEndPoint(IPAddress.Loopback, udpPort);
                return node;
            }

            private static async Task WaitForRpcAsync(int rpcPort, CancellationToken ct)
            {
                using var http = new System.Net.Http.HttpClient();
                var body = "{\"jsonrpc\":\"2.0\",\"method\":\"net_version\",\"params\":[],\"id\":1}";
                for (int i = 0; i < 60; i++)
                {
                    try
                    {
                        var r = await http.PostAsync($"http://127.0.0.1:{rpcPort}",
                            new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json"), ct);
                        if (r.IsSuccessStatusCode) return;
                    }
                    catch { }
                    await Task.Delay(500, ct);
                }
                throw new TimeoutException("Geth RPC did not come up");
            }

            private static string FindGeth() => Helpers.GethToolLocator.FindGethBinary();

            public void Dispose()
            {
                try
                {
                    if (_process != null && !_process.HasExited)
                    {
                        _process.Kill(entireProcessTree: true);
                        _process.WaitForExit();
                    }
                    _process?.Dispose();
                }
                catch { }
                try { Directory.Delete(_dataDir, true); } catch { }
            }
        }
    }
}
