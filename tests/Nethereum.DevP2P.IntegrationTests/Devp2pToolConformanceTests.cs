using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Discv4;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.Enr;
using Nethereum.Signer;
using Nethereum.Signer.Enr;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    /// <summary>
    /// Spec compliance tests using the canonical go-ethereum devp2p tool from
    /// cmd/devp2p (built from go-ethereum v1.16.4 source, located at
    /// geth-tools/devp2p.exe). These tests start a Nethereum listener and
    /// invoke devp2p's conformance test suites against us, then assert the
    /// suite passes.
    ///
    /// These are the only spec-compliance tests we have that aren't just
    /// self-consistency round-trips.
    /// </summary>
    public class Devp2pToolConformanceTests
    {
        private readonly ITestOutputHelper _output;

        public Devp2pToolConformanceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Discv4_AgainstNethereumListener_ConformanceSuitePasses()
        {
            var devp2pTool = FindDevp2pTool();
            _output.WriteLine($"Using devp2p tool: {devp2pTool}");

            var localKey = EthECKey.GenerateKey();
            var routingTable = new Discv4RoutingTable(localKey.GetPubKeyNoPrefix());
            using var listener = new Discv4Listener(localKey, routingTable);
            int pingsRx = 0, pongsRx = 0, findRx = 0, enrRx = 0;
            var senders = new System.Collections.Concurrent.ConcurrentBag<string>();
            listener.PingReceived += (_, e) =>
            {
                System.Threading.Interlocked.Increment(ref pingsRx);
                senders.Add($"PING from {e.Sender} (family={e.Sender.AddressFamily})");
            };
            listener.PongReceived += (_, e) => { System.Threading.Interlocked.Increment(ref pongsRx); };
            listener.FindNodeReceived += (_, e) => { System.Threading.Interlocked.Increment(ref findRx); };
            listener.EnrRequestReceived += (_, e) => { System.Threading.Interlocked.Increment(ref enrRx); };
            listener.ErrorOccurred += (_, e) => _output.WriteLine($"!! listener error [{e.Phase}]: {e.Exception.GetType().Name}: {e.Exception.Message}");
            listener.Start(udpPort: 0, bindAddress: IPAddress.Loopback);

            var enr = new EnrRecord { Sequence = 1 };
            enr.Pairs["id"] = Encoding.ASCII.GetBytes("v4");
            enr.Pairs["ip"] = IPAddress.Loopback.GetAddressBytes();
            enr.Pairs["udp"] = new[] { (byte)((listener.Port >> 8) & 0xff), (byte)(listener.Port & 0xff) };
            EnrRecordSigner.Sign(enr, localKey);
            listener.LocalEnrEncoded = EnrRecordEncoder.EncodeRecord(enr);
            listener.LocalEnrSequence = enr.Sequence;

            var ourEnode = $"enode://{localKey.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{listener.Port}";
            _output.WriteLine($"Nethereum discv4 listener at {ourEnode}");

            var psi = new ProcessStartInfo(devp2pTool, $"discv4 test --remote {ourEnode}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            _output.WriteLine("=== devp2p stdout ===");
            _output.WriteLine(stdout);
            _output.WriteLine("=== devp2p stderr ===");
            _output.WriteLine(stderr);
            _output.WriteLine($"Exit code: {proc.ExitCode}");

            _output.WriteLine($"Counts — ping:{pingsRx} pong:{pongsRx} findnode:{findRx} enrreq:{enrRx}");
            foreach (var s in senders) _output.WriteLine($"  {s}");
            await listener.StopAsync();

            // Fail loudly if the tool reports any failures, so we can iterate on
            // the implementation gaps. Even a partial pass is informative.
            Assert.Equal(0, proc.ExitCode);
        }

        private static string FindDevp2pTool() => Helpers.GethToolLocator.FindDevp2pTool();
    }
}
