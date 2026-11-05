using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain.Sync;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;
using Nethereum.MainnetChain.Observability;
using Nethereum.RPC.Eth.DTOs.Engine;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    public class EngineOnlyServerTests : IDisposable
    {
        private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"engine_only_{Guid.NewGuid():N}");

        public void Dispose()
        {
            try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true); } catch { }
        }

        [Fact]
        public async Task Given_a_custom_genesis_When_the_engine_server_starts_on_an_empty_data_dir_Then_the_genesis_root_resolves_and_the_first_payload_imports()
        {
            var genesis = CustomGenesisFixture.Document();
            await using var author = await DevChainAuthor.CreateAsync(genesis);
            var block = await author.ProduceAsync();

            await using var server = await EngineOnlyServer.StartAsync(_workDir, CustomGenesisFixture.Json());

            var genesisHeader = await server.Bundle.Blocks.GetByNumberAsync(0);
            Assert.Equal(author.GenesisStateRoot, genesisHeader.StateRoot);
            Assert.True(server.Bundle.StateTrieNodes.ContainsKey(genesisHeader.StateRoot));
            Assert.Equal(author.GenesisHash, server.Gate.HeadHash);

            var attached = await server.Engine.ForkchoiceUpdatedAsync(DevChainAuthor.StateOf(author.GenesisHash), null);
            var imported = await server.Engine.NewPayloadV4Async(block.Payload, block.ExecutionRequests, DevChainAuthor.ZeroRoot);
            var adopted = await server.Engine.ForkchoiceUpdatedAsync(DevChainAuthor.StateOf(block.Hash), null);

            Assert.Equal(EnginePayloadStatus.Valid, attached.PayloadStatus.Status);
            Assert.Equal(EnginePayloadStatus.Valid, imported.Status);
            Assert.Equal(EnginePayloadStatus.Valid, adopted.PayloadStatus.Status);
            Assert.Equal(block.StateRoot, (await server.Bundle.Blocks.GetByNumberAsync(1)).StateRoot);
            Assert.Equal(block.Hash, await server.Bundle.Blocks.GetHashByNumberAsync(BigInteger.One));
            Assert.Equal(block.Hash, server.Gate.HeadHash);
        }

        [Fact]
        public async Task Given_an_engine_only_genesis_When_started_Then_no_peer_or_light_client_service_runs()
        {
            await using var server = await EngineOnlyServer.StartAsync(_workDir, CustomGenesisFixture.Json());

            Assert.True(server.Config.EngineOnly);
            Assert.Null(server.Services.GetService<IPeerPool>());
            Assert.Null(server.Services.GetService<IFetchRequestScheduler>());
            Assert.Null(server.Services.GetService<HeaderFollowService>());
            Assert.Null(server.Services.GetService<Nethereum.Consensus.LightClient.LightClientService>());
            var hosted = server.Services.GetServices<IHostedService>().Select(s => s.GetType()).ToList();
            Assert.DoesNotContain(typeof(LightClientHostedService), hosted);
            Assert.DoesNotContain(typeof(SnapSyncProgressReporter), hosted);
            var nodeConfig = server.Services.GetRequiredService<ChainNodeConfig>();
            Assert.Equal(SyncMode.None, nodeConfig.Sync.Mode);
            Assert.False(nodeConfig.Network.Serve);
        }
    }
}
