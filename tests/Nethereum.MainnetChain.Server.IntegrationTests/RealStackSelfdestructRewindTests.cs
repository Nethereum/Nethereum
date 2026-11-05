using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs.Engine;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    public class RealStackSelfdestructRewindTests : IDisposable
    {
        private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"real_stack_sd_{Guid.NewGuid():N}");
        private readonly ITestOutputHelper _output;

        public RealStackSelfdestructRewindTests(ITestOutputHelper output) => _output = output;

        public void Dispose()
        {
            try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true); } catch { }
        }

        private static string DataFile(string name) => Path.Combine(AppContext.BaseDirectory, "TestData", "RealStack", name);

        private static string Genesis()
        {
            using var file = File.OpenRead(DataFile("genesis-0xa9149b4b29ac0473.json.gz"));
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            return reader.ReadToEnd();
        }

        private sealed record NewPayload(ExecutionPayloadV3 Payload, string[] Requests, string ParentBeaconBlockRoot);

        private static (JObject Fixture, List<NewPayload> Payloads) Load(string fixtureFile)
        {
            var fixture = (JObject)((JProperty)JObject.Parse(File.ReadAllText(DataFile(fixtureFile))).First!).Value;
            var payloads = fixture["engineNewPayloads"]!.Select(entry =>
            {
                var parameters = (JArray)entry["params"]!;
                return new NewPayload(
                    parameters[0].ToObject<ExecutionPayloadV3>()!,
                    parameters[3].ToObject<string[]>()!,
                    parameters[2].Value<string>()!);
            }).ToList();
            return (fixture, payloads);
        }

        private static async Task<RpcResponseMessage> SetHeadAsync(EngineOnlyServer server, string hexNumber) =>
            await server.Services.GetRequiredService<RpcDispatcher>().DispatchAsync(
                new RpcRequestMessage(1, "debug_setHead", new object[] { hexNumber }));

        private async Task RunFixtureAsync(EngineOnlyServer server, string fixtureFile)
        {
            var (fixture, payloads) = Load(fixtureFile);
            var lastHash = fixture["lastblockhash"]!.Value<string>()!.HexToByteArray();
            var postStateHash = fixture["postStateHash"]!.Value<string>()!.HexToByteArray();
            var watch = Stopwatch.StartNew();

            foreach (var entry in payloads)
            {
                var status = await server.Engine.NewPayloadV4Async(entry.Payload, entry.Requests, entry.ParentBeaconBlockRoot);
                Assert.Equal(EnginePayloadStatus.Valid, status.Status);
            }
            _output.WriteLine($"{fixtureFile} newPayload {watch.ElapsedMilliseconds} ms");

            var forkchoice = await server.Engine.ForkchoiceUpdatedAsync(DevChainAuthor.StateOf(lastHash), null);
            Assert.Equal(EnginePayloadStatus.Valid, forkchoice.PayloadStatus.Status);
            Assert.Equal(lastHash, server.Gate.HeadHash);
            var head = await server.Bundle.Blocks.GetByNumberAsync((long)server.Gate.HeadNumber);
            Assert.Equal(postStateHash, head.StateRoot);
        }

        private async Task SetHeadAndExpectAsync(EngineOnlyServer server, string hexNumber, ulong expected)
        {
            var watch = Stopwatch.StartNew();
            var response = await SetHeadAsync(server, hexNumber);
            _output.WriteLine($"debug_setHead {hexNumber} {watch.ElapsedMilliseconds} ms");
            Assert.False(response.HasError, response.Error?.Message);
            Assert.Equal(expected, server.Gate.HeadNumber);
        }

        [Fact]
        public async Task Given_the_selfdestruct_of_contracts_created_in_the_block_When_the_chain_is_rewound_straight_after_the_forkchoice_and_the_next_fixture_runs_Then_every_rewind_succeeds_and_the_roots_match()
        {
            await using var server = await EngineOnlyServer.StartAsync(_workDir, Genesis());
            var genesisHash = server.Gate.HeadHash;

            await RunFixtureAsync(server, "selfdestruct-created-value-false-fixture.json");
            await SetHeadAndExpectAsync(server, "0x0", 0);
            Assert.Equal(genesisHash, server.Gate.HeadHash);

            await RunFixtureAsync(server, "selfdestruct-created-value-true-fixture.json");
            await SetHeadAndExpectAsync(server, "0x1", 1);
            await RunFixtureAsync(server, "selfdestruct-created-value-true-fixture.json");
        }

        [Fact]
        public async Task Given_a_two_block_chain_rolled_back_to_genesis_When_a_different_two_block_chain_is_imported_and_debug_setHead_names_its_first_block_Then_the_rewind_succeeds()
        {
            var abandoned = new List<AuthoredBlock>();
            var replacement = new List<AuthoredBlock>();
            await using (var authorX = await DevChainAuthor.CreateAsync(CustomGenesisFixture.Document()))
            {
                abandoned.Add(await authorX.ProduceAsync(transfers: 2));
                abandoned.Add(await authorX.ProduceAsync(transfers: 2));
            }
            await using (var authorY = await DevChainAuthor.CreateAsync(CustomGenesisFixture.Document()))
            {
                replacement.Add(await authorY.ProduceAsync(transfers: 5, distinctRecipients: true));
                replacement.Add(await authorY.ProduceAsync(transfers: 3, distinctRecipients: true));
            }

            await using var server = await EngineOnlyServer.StartAsync(_workDir, CustomGenesisFixture.Json());
            var genesisState = DevChainAuthor.StateOf(server.Gate.HeadHash);
            await server.Engine.ForkchoiceUpdatedAsync(genesisState, null);

            foreach (var block in abandoned)
                Assert.Equal(EnginePayloadStatus.Valid, (await server.Engine.NewPayloadV4Async(block.Payload, block.ExecutionRequests, DevChainAuthor.ZeroRoot)).Status);
            await server.Engine.ForkchoiceUpdatedAsync(DevChainAuthor.StateOf(abandoned[^1].Hash), null);
            await SetHeadAndExpectAsync(server, "0x0", 0);

            foreach (var block in replacement)
                Assert.Equal(EnginePayloadStatus.Valid, (await server.Engine.NewPayloadV4Async(block.Payload, block.ExecutionRequests, DevChainAuthor.ZeroRoot)).Status);
            await server.Engine.ForkchoiceUpdatedAsync(DevChainAuthor.StateOf(replacement[^1].Hash), null);
            Assert.Equal(2UL, server.Gate.HeadNumber);

            await SetHeadAndExpectAsync(server, "0x1", 1);
            Assert.Equal(replacement[0].Hash, server.Gate.HeadHash);
            Assert.Equal(replacement[0].StateRoot, (await server.Bundle.Blocks.GetByNumberAsync(1)).StateRoot);
            Assert.True(server.Bundle.StateTrieNodes.ContainsKey(replacement[0].StateRoot));
        }
    }
}
