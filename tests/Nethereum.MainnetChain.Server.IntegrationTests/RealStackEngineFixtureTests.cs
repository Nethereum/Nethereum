using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs.Engine;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    public class RealStackEngineFixtureTests : IDisposable
    {
        private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"real_stack_{Guid.NewGuid():N}");

        public void Dispose()
        {
            try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true); } catch { }
        }

        private static string DataFile(string name) => Path.Combine(AppContext.BaseDirectory, "TestData", "RealStack", name);

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

        private static async Task RunFixtureOnceAsync(EngineOnlyServer server, JObject fixture, List<NewPayload> payloads)
        {
            var lastHash = fixture["lastblockhash"]!.Value<string>()!.HexToByteArray();
            var postStateHash = fixture["postStateHash"]!.Value<string>()!.HexToByteArray();

            foreach (var entry in payloads)
            {
                var status = await server.Engine.NewPayloadV4Async(entry.Payload, entry.Requests, entry.ParentBeaconBlockRoot);
                Assert.Equal(EnginePayloadStatus.Valid, status.Status);
                Assert.Equal(entry.Payload.BlockHash, status.LatestValidHash);
            }

            var forkchoice = await server.Engine.ForkchoiceUpdatedAsync(DevChainAuthor.StateOf(lastHash), null);

            Assert.Equal(EnginePayloadStatus.Valid, forkchoice.PayloadStatus.Status);
            Assert.Equal(lastHash, server.Gate.HeadHash);
            var head = await server.Bundle.Blocks.GetByNumberAsync((long)server.Gate.HeadNumber);
            Assert.Equal(postStateHash, head.StateRoot);
        }

        [Fact]
        public async Task Given_the_real_stack_CREATE2_collisions_fixture_on_an_empty_data_dir_When_payloads_then_forkchoice_then_debug_setHead_0_run_twice_Then_every_payload_is_VALID_and_the_post_state_matches()
        {
            var (fixture, payloads) = Load("create2-collisions-fixture.json");
            await using var server = await EngineOnlyServer.StartAsync(
                _workDir, File.ReadAllText(DataFile("genesis-0x5f42b57d81ee579a.json")));
            var genesisHash = server.Gate.HeadHash;

            await RunFixtureOnceAsync(server, fixture, payloads);
            var rollback = await SetHeadAsync(server, "0x0");
            Assert.False(rollback.HasError, rollback.Error?.Message);
            Assert.Equal(genesisHash, server.Gate.HeadHash);
            Assert.Equal(0UL, server.Gate.HeadNumber);

            await RunFixtureOnceAsync(server, fixture, payloads);
        }
    }
}
