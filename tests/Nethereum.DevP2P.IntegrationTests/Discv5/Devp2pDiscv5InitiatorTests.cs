using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv5;
using Nethereum.DevP2P.IntegrationTests.Helpers;
using Nethereum.Model.Enr;
using Nethereum.Signer;
using Nethereum.Signer.Enr;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests.Discv5
{
    // Drives our Discv5Listener in initiator role against a reference peer
    // launched via `devp2p discv5 listen` (the go-ethereum conformance binary).
    // Verifies that PING/PONG and FINDNODE/NODES round-trip end-to-end
    // including the WHOAREYOU handshake.
    public class Devp2pDiscv5InitiatorTests
    {
        private const int OurDiscv5Port = 30305;     // coordinated to avoid conflict with parallel responder tests on 30303/30304
        private const int RefPeerPort = 30399;
        private const string RefPeerIp = "127.0.0.1";

        private readonly ITestOutputHelper _output;
        public Devp2pDiscv5InitiatorTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public async Task Given_RefPeerListening_When_WeSendPingAsync_Then_PongReceived()
        {
            var devp2pTool = GethToolLocator.FindDevp2pTool();
            _output.WriteLine($"Using devp2p tool: {devp2pTool}");

            // 1. Fresh nodekey for the reference peer.
            var refKey = EthECKey.GenerateKey();
            var refKeyHex = ToHexNoPrefix(refKey.GetPrivateKeyAsBytes());
            _output.WriteLine($"Reference peer nodekey: {refKeyHex}");

            // 2. Launch `devp2p discv5 listen`.
            var psi = new ProcessStartInfo(devp2pTool,
                $"discv5 listen --addr {RefPeerIp}:{RefPeerPort} --extaddr {RefPeerIp}:{RefPeerPort} --nodekey {refKeyHex}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            _output.WriteLine($"Invoking: {psi.FileName} {psi.Arguments}");

            using var refProc = Process.Start(psi);
            try
            {
                var enrUrl = await CaptureRefEnrAsync(refProc, TimeSpan.FromSeconds(8));
                Assert.False(string.IsNullOrEmpty(enrUrl),
                    "devp2p discv5 listen did not emit an ENR on stdout/stderr within 8s.");
                _output.WriteLine($"Reference peer ENR: {enrUrl}");

                var refEnr = EnrRecordEncoder.ParseUrl(enrUrl);
                Assert.Equal("v4", refEnr.Id);
                Assert.NotNull(refEnr.Secp256k1);
                Assert.NotNull(refEnr.IP4);
                Assert.NotNull(refEnr.UdpPort);

                var refNodeId = Discv5Crypto.ComputeNodeId(refEnr.Secp256k1);
                var refEndpoint = new IPEndPoint(refEnr.IP4, refEnr.UdpPort.Value);

                // 3. Start our listener and build a signed ENR (peer needs it to verify our id-signature).
                var localKey = EthECKey.GenerateKey();
                using var listener = new Discv5Listener(localKey);
                listener.Start(IPAddress.Loopback, OurDiscv5Port);

                var ourEnr = new EnrRecord { Sequence = 1 };
                ourEnr.Pairs["id"] = Encoding.ASCII.GetBytes("v4");
                ourEnr.Pairs["ip"] = IPAddress.Loopback.GetAddressBytes();
                ourEnr.Pairs["udp"] = new[] { (byte)((OurDiscv5Port >> 8) & 0xff), (byte)(OurDiscv5Port & 0xff) };
                EnrRecordSigner.Sign(ourEnr, localKey);
                listener.LocalEnrEncoded = EnrRecordEncoder.EncodeRecord(ourEnr);
                listener.LocalEnrSequence = ourEnr.Sequence;

                _output.WriteLine($"Our listener port: {listener.Port}, ENR: {EnrRecordEncoder.ToUrl(ourEnr)}");

                // 4. Send PING — should kick handshake then receive PONG.
                var pong = await listener.SendPingAsync(
                    refEndpoint, refNodeId, refEnr.Secp256k1,
                    TimeSpan.FromSeconds(10), CancellationToken.None);

                Assert.NotNull(pong);
                Assert.True(pong.EnrSeq > 0, $"PONG enr-seq should be positive, got {pong.EnrSeq}");
                _output.WriteLine($"PONG received: enrSeq={pong.EnrSeq}, recipient={new IPAddress(pong.RecipientIp)}:{pong.RecipientPort}");

                // 5. Optional: FINDNODE(distance=0) — peer should return its own ENR.
                var enrs = await listener.SendFindNodeAsync(
                    refEndpoint, refNodeId, refEnr.Secp256k1,
                    new uint[] { 0 },
                    TimeSpan.FromSeconds(10), CancellationToken.None);
                Assert.NotNull(enrs);
                _output.WriteLine($"FINDNODE returned {enrs.Count} ENR(s)");

                await listener.StopAsync();
            }
            finally
            {
                TryKill(refProc);
                var stderrTail = await SafeReadAllAsync(refProc?.StandardError, TimeSpan.FromSeconds(1));
                if (!string.IsNullOrEmpty(stderrTail))
                {
                    _output.WriteLine("=== devp2p discv5 listen stderr tail ===");
                    _output.WriteLine(stderrTail);
                }
            }
        }

        private static async Task<string> CaptureRefEnrAsync(Process proc, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            var pattern = new Regex(@"enr:[A-Za-z0-9\-_]+");
            // devp2p discv5 listen prints the ENR on stdout immediately after `New local node record` line.
            var stdoutLines = new System.Threading.Tasks.TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = Task.Run(async () =>
            {
                try
                {
                    string line;
                    while ((line = await proc.StandardOutput.ReadLineAsync()) != null)
                    {
                        var match = pattern.Match(line);
                        if (match.Success)
                        {
                            stdoutLines.TrySetResult(match.Value);
                            return;
                        }
                    }
                }
                catch (Exception) { /* proc died */ }
                stdoutLines.TrySetResult(null);
            });
            _ = Task.Run(async () =>
            {
                try
                {
                    string line;
                    while ((line = await proc.StandardError.ReadLineAsync()) != null)
                    {
                        var match = pattern.Match(line);
                        if (match.Success)
                        {
                            stdoutLines.TrySetResult(match.Value);
                            return;
                        }
                    }
                }
                catch (Exception) { /* proc died */ }
            });

            var delay = Task.Delay(timeout);
            var completed = await Task.WhenAny(stdoutLines.Task, delay);
            return completed == stdoutLines.Task ? await stdoutLines.Task : null;
        }

        private static void TryKill(Process p)
        {
            try { if (p != null && !p.HasExited) p.Kill(entireProcessTree: true); }
            catch (Exception) { /* already dead */ }
        }

        private static async Task<string> SafeReadAllAsync(StreamReader r, TimeSpan timeout)
        {
            if (r == null) return null;
            try
            {
                using var cts = new CancellationTokenSource(timeout);
                var sb = new StringBuilder();
                var buffer = new char[4096];
                while (!cts.Token.IsCancellationRequested)
                {
                    var read = await r.ReadAsync(buffer, 0, buffer.Length);
                    if (read == 0) break;
                    sb.Append(buffer, 0, read);
                    if (sb.Length > 8192) break;
                }
                return sb.ToString();
            }
            catch (Exception) { return null; }
        }

        private static string ToHexNoPrefix(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.AppendFormat("{0:x2}", b);
            return sb.ToString();
        }
    }
}
