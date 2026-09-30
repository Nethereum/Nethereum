using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv5;
using Nethereum.Model.Enr;
using Nethereum.Signer;
using Nethereum.Signer.Enr;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    public class Devp2pDiscv5ConformanceTests
    {
        private readonly ITestOutputHelper _output;
        public Devp2pDiscv5ConformanceTests(ITestOutputHelper output) { _output = output; }

        [Theory]
        [InlineData("Ping")]
        [InlineData("PingLargeRequestID")]
        [InlineData("PingMultiIP")]
        [InlineData("PingHandshakeInterrupted")]
        [InlineData("TalkRequest")]
        [InlineData("FindnodeZeroDistance")]
        [InlineData("FindnodeResults")]
        public Task Discv5_PerSubtest_AgainstNethereumListener(string subtest)
            => RunDiscv5Subtest(subtest);

        private async Task RunDiscv5Subtest(string subtest)
        {
            var devp2pTool = FindDevp2pTool();
            _output.WriteLine($"Using devp2p tool: {devp2pTool}");

            var localKey = EthECKey.GenerateKey();
            using var listener = new Discv5Listener(localKey);
            listener.Start(IPAddress.Loopback, port: 0);

            var enr = new EnrRecord { Sequence = 1 };
            enr.Pairs["id"] = Encoding.ASCII.GetBytes("v4");
            enr.Pairs["ip"] = IPAddress.Loopback.GetAddressBytes();
            enr.Pairs["udp"] = new[] { (byte)((listener.Port >> 8) & 0xff), (byte)(listener.Port & 0xff) };
            EnrRecordSigner.Sign(enr, localKey);
            listener.LocalEnrEncoded = EnrRecordEncoder.EncodeRecord(enr);
            listener.LocalEnrSequence = enr.Sequence;

            var enrUrl = EnrRecordEncoder.ToUrl(enr);
            _output.WriteLine($"Nethereum discv5 listener at {enrUrl}");
            _output.WriteLine($"  Port: {listener.Port}");

            var psi = new ProcessStartInfo(devp2pTool,
                $"discv5 test --listen1 127.0.0.1 --listen2 127.0.0.2 --run ^{subtest}$ {enrUrl}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            _output.WriteLine($"Invoking: {psi.FileName} {psi.Arguments}");

            using var proc = Process.Start(psi);
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();
            var exited = proc.WaitForExit(90_000);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            _output.WriteLine("=== devp2p stdout ===");
            _output.WriteLine(stdout);
            _output.WriteLine("=== devp2p stderr ===");
            _output.WriteLine(stderr);
            _output.WriteLine($"Exit code: {proc.ExitCode}");

            await listener.StopAsync();
            Assert.True(exited, "devp2p tool did not exit within 60s");
            Assert.Equal(0, proc.ExitCode);
        }

        private static string FindDevp2pTool() => Helpers.GethToolLocator.FindDevp2pTool();
    }
}
