using System;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.EVM.ForkId;
using Nethereum.DevP2P.Sync.Serving;

namespace Nethereum.DevP2P.IntegrationTests
{
    public class Devp2pSnapTestConformanceTests
    {
        private readonly ITestOutputHelper _output;
        public Devp2pSnapTestConformanceTests(ITestOutputHelper output) { _output = output; }

        [Theory]
        [InlineData("Status")]
        [InlineData("AccountRange")]
        [InlineData("GetByteCodes")]
        [InlineData("GetTrieNodes")]
        [InlineData("GetStorageRanges")]
        public Task SnapTest_PerSubtest_AgainstNethereumServer_Passes(string subtest)
            => RunSnapSubtest(subtest);

        [Fact]
        public async Task SnapTest_StatusSubtest_AgainstNethereumServer_Passes()
        {
            await RunSnapSubtest("Status");
        }

        private async Task RunSnapSubtest(string subtest)
        {
            var testdata = FindTestdata();
            var devp2pTool = FindDevp2pTool();
            _output.WriteLine($"Testdata: {testdata}");
            _output.WriteLine($"Tool: {devp2pTool}");

            var (stateResult, networkId, genesisHash, headHash, headNumber, headTime, forkBlocks, forkTimes) =
                LoadFixture(testdata);

            var historical = GethTestdataHistoricalStateBuilder.Build(testdata);
            _output.WriteLine($"Historical replay: {historical.BlocksMatched}/{historical.BlocksProcessed} blocks byte-exact, head root {historical.HeadStateRoot.ToHex()}");

            _output.WriteLine($"State accounts: {stateResult.AccountCount}, root match: {stateResult.RootMatches}");
            _output.WriteLine($"Genesis: {genesisHash.ToHex()}");
            _output.WriteLine($"Head block #{headNumber}: {headHash.ToHex()}");
            _output.WriteLine($"Fork blocks: {string.Join(",", forkBlocks)}");
            _output.WriteLine($"Fork times: {string.Join(",", forkTimes)}");

            Assert.True(stateResult.RootMatches, "state root divergence — testdata isn't fully decoded by our loader");

            var forkHash = Eip2124ForkIdCalculator.ComputeForkHash(genesisHash, forkBlocks, forkTimes);
            _output.WriteLine($"ForkID hash: 0x{forkHash:x8}");

            var serverKey = EthECKey.GenerateKey();
            var serverConfig = new DevP2PConfig
            {
                ClientId = "Nethereum/snap-conformance",
                ConnectTimeoutMs = 5000,
                HandshakeTimeoutMs = 5000,
                RequestTimeoutMs = 10000,
                PingIntervalMs = 30000,
                ReadTimeoutMs = 30000
            };

            using var sessionCts = new CancellationTokenSource();
            var listener = new RlpxListener(serverKey, serverConfig);
            listener.PeerFailed += (_, e) => _output.WriteLine($"server PeerFailed: {e.Exception.GetType().Name}: {e.Exception.Message}");
            listener.PeerAccepted += async (_, conn) =>
            {
                try
                {
                    var ethOffset = conn.GetCapabilityOffset("eth");
                    var ethCap = conn.SharedCapabilities.Find(c => c.Name == "eth");
                    int negotiatedEthVersion = ethCap.Version;

                    var status = new Eth69StatusMessage
                    {
                        ProtocolVersion = negotiatedEthVersion,
                        NetworkId = networkId,
                        GenesisHash = genesisHash,
                        ForkHash = forkHash,
                        ForkNext = 0,
                        EarliestBlock = 0,
                        LatestBlock = headNumber,
                        LatestBlockHash = headHash
                    };
                    await conn.SendMessageAsync(ethOffset + Eth68MessageIds.Status, Eth69StatusMessageEncoder.Encode(status));
                    await conn.ReceiveMessageAsync();

                    var snap = new Snap1Handler(conn, new PatriciaSnapRequestHandler(historical.TrieStorage, historical.Bytecodes));
                    int snapOffset = conn.GetCapabilityOffset("snap");
                    var snapCap = conn.SharedCapabilities.Find(c => c.Name == "snap");

                    while (!sessionCts.Token.IsCancellationRequested && conn.IsConnected)
                    {
                        var (msgId, payload) = await conn.ReceiveMessageAsync(sessionCts.Token);
                        if (msgId >= snapOffset && msgId < snapOffset + snapCap.Length)
                            await snap.HandleSnapMessageAsync(msgId - snapOffset, payload, sessionCts.Token);
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"server: {ex.GetType().Name}: {ex.Message}");
                }
            };

            try
            {
                listener.Start(port: 0, bindAddress: IPAddress.Loopback);
                var enode = $"enode://{serverKey.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{listener.Port}";
                _output.WriteLine($"Listening at {enode}");

                var args = string.Join(" ",
                    "rlpx snap-test",
                    "--chain", $"\"{testdata}\"",
                    "--engineapi", "http://127.0.0.1:1",
                    "--jwtsecret", "0x7365637265747365637265747365637265747365637265747365637265747365",
                    "--node", enode,
                    "--run", subtest);
                _output.WriteLine($"Invoking: devp2p {args}");

                var psi = new ProcessStartInfo(devp2pTool, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(psi);
                var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                var stderrTask = proc.StandardError.ReadToEndAsync();
                var exited = proc.WaitForExit(60_000);

                var stdout = await stdoutTask;
                var stderr = await stderrTask;
                _output.WriteLine("=== devp2p stdout ===");
                _output.WriteLine(stdout);
                _output.WriteLine("=== devp2p stderr ===");
                _output.WriteLine(stderr);

                Assert.True(exited, "devp2p tool did not exit within 60s");
                _output.WriteLine($"Exit: {proc.ExitCode}");
                Assert.Equal(0, proc.ExitCode);
            }
            finally
            {
                sessionCts.Cancel();
                await listener.StopAsync();
            }
        }

        private static (HeadStateLoader.LoadResult state,
                        ulong networkId,
                        byte[] genesisHash,
                        byte[] headHash,
                        ulong headNumber,
                        ulong headTime,
                        ulong[] forkBlocks,
                        ulong[] forkTimes) LoadFixture(string testdata)
        {
            var state = HeadStateLoader.Load(Path.Combine(testdata, "headstate.json"));
            var genesis = GethChainRlpFixtureReader.ReadGenesisHash(Path.Combine(testdata, "chain.rlp"));
            var headBlock = JObject.Parse(File.ReadAllText(Path.Combine(testdata, "headblock.json")));
            var forkenv = JObject.Parse(File.ReadAllText(Path.Combine(testdata, "forkenv.json")));

            var headHash = ParseHex(headBlock["hash"].ToString());
            var headNumber = ParseUlong(headBlock["number"].ToString());
            var headTime = ParseUlong(headBlock["timestamp"].ToString());
            var networkId = ulong.Parse(forkenv["HIVE_NETWORK_ID"].ToString());

            var forkBlocks = new ulong[]
            {
                ulong.Parse(forkenv["HIVE_FORK_TANGERINE"].ToString()),
                ulong.Parse(forkenv["HIVE_FORK_SPURIOUS"].ToString()),
                ulong.Parse(forkenv["HIVE_FORK_BYZANTIUM"].ToString()),
                ulong.Parse(forkenv["HIVE_FORK_CONSTANTINOPLE"].ToString()),
                ulong.Parse(forkenv["HIVE_FORK_PETERSBURG"].ToString()),
                ulong.Parse(forkenv["HIVE_FORK_ISTANBUL"].ToString()),
                ulong.Parse(forkenv["HIVE_FORK_MUIR_GLACIER"].ToString()),
                ulong.Parse(forkenv["HIVE_FORK_BERLIN"].ToString()),
                ulong.Parse(forkenv["HIVE_FORK_LONDON"].ToString()),
                ulong.Parse(forkenv["HIVE_FORK_ARROW_GLACIER"].ToString()),
                ulong.Parse(forkenv["HIVE_FORK_GRAY_GLACIER"].ToString()),
                ulong.Parse(forkenv["HIVE_MERGE_BLOCK_ID"].ToString())
            };
            var timestampKeys = new[]
            {
                "HIVE_SHANGHAI_TIMESTAMP",
                "HIVE_CANCUN_TIMESTAMP",
                "HIVE_PRAGUE_TIMESTAMP"
            };
            var unknownTimestamps = forkenv.Properties()
                .Select(prop => prop.Name)
                .Where(name => name.EndsWith("_TIMESTAMP") && !timestampKeys.Contains(name))
                .ToList();
            Assert.True(unknownTimestamps.Count == 0,
                "the geth testdata declares fork timestamps this test does not fold into the fork id: "
                + string.Join(", ", unknownTimestamps));

            var forkTimes = timestampKeys
                .Where(key => forkenv[key] != null)
                .Select(key => ulong.Parse(forkenv[key].ToString()))
                .ToArray();

            return (state, networkId, genesis, headHash, headNumber, headTime, forkBlocks, forkTimes);
        }

        private static byte[] ParseHex(string s)
        {
            if (s.StartsWith("0x") || s.StartsWith("0X")) s = s.Substring(2);
            if (s.Length % 2 != 0) s = "0" + s;
            return s.HexToByteArray();
        }

        private static ulong ParseUlong(string s)
        {
            if (s.StartsWith("0x") || s.StartsWith("0X"))
                return Convert.ToUInt64(s.Substring(2), 16);
            return ulong.Parse(s);
        }

        private static string FindTestdata() => Helpers.GethToolLocator.FindEthTestTestdata();
        private static string FindDevp2pTool() => Helpers.GethToolLocator.FindDevp2pTool();
    }
}
