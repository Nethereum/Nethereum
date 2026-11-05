using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs.Engine;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    public class DebugSetHeadTests : IDisposable
    {
        private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"debug_set_head_{Guid.NewGuid():N}");

        public void Dispose()
        {
            try { if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true); } catch { }
        }

        private static async Task<List<AuthoredBlock>> AuthorAsync(int count)
        {
            await using var author = await DevChainAuthor.CreateAsync(CustomGenesisFixture.Document());
            var blocks = new List<AuthoredBlock>();
            for (var i = 0; i < count; i++) blocks.Add(await author.ProduceAsync(transfers: 1 + i % 3));
            return blocks;
        }

        private static async Task ImportAsync(EngineOnlyServer server, AuthoredBlock block)
        {
            var imported = await server.Engine.NewPayloadV4Async(block.Payload, block.ExecutionRequests, DevChainAuthor.ZeroRoot);
            Assert.Equal(EnginePayloadStatus.Valid, imported.Status);
            var adopted = await server.Engine.ForkchoiceUpdatedAsync(DevChainAuthor.StateOf(block.Hash), null);
            Assert.Equal(EnginePayloadStatus.Valid, adopted.PayloadStatus.Status);
        }

        private static async Task<RpcResponseMessage> SetHeadAsync(EngineOnlyServer server, string hexNumber) =>
            await server.Services.GetRequiredService<RpcDispatcher>().DispatchAsync(
                new RpcRequestMessage(1, "debug_setHead", new object[] { hexNumber }));

        [Fact]
        public async Task Given_blocks_1_to_N_When_debug_setHead_0_Then_the_next_newPayload_for_block_1_imports_and_roots_match()
        {
            var blocks = await AuthorAsync(4);
            await using var server = await EngineOnlyServer.StartAsync(_workDir, CustomGenesisFixture.Json());
            var genesisHash = server.Gate.HeadHash;
            await server.Engine.ForkchoiceUpdatedAsync(DevChainAuthor.StateOf(genesisHash), null);
            foreach (var block in blocks) await ImportAsync(server, block);

            var response = await SetHeadAsync(server, "0x0");

            Assert.False(response.HasError, response.Error?.Message);
            Assert.Equal(0UL, server.Gate.HeadNumber);
            Assert.Equal(genesisHash, server.Gate.HeadHash);
            Assert.Equal(0UL, server.Bundle.Metadata.GetLastBlock());
            Assert.Null(await server.Bundle.Blocks.GetByNumberAsync(1));
            var genesisState = await server.Bundle.Blocks.GetByNumberAsync(0);
            Assert.True(server.Bundle.StateTrieNodes.ContainsKey(genesisState.StateRoot));
            var funded = await server.Bundle.State.GetAccountAsync(CustomGenesisFixture.FundedAddress);
            Assert.Equal(0, (int)funded.Nonce);

            foreach (var block in blocks) await ImportAsync(server, block);
            foreach (var block in blocks)
                Assert.Equal(block.StateRoot, (await server.Bundle.Blocks.GetByNumberAsync((long)block.Number)).StateRoot);
            Assert.Equal(blocks[^1].Hash, server.Gate.HeadHash);
        }

        [Fact]
        public async Task Given_a_rollback_deeper_than_the_retained_node_history_When_debug_setHead_runs_Then_the_chain_rewinds_and_replays_with_the_same_roots()
        {
            var blocks = await AuthorAsync(6);
            await using var server = await EngineOnlyServer.StartAsync(
                _workDir, CustomGenesisFixture.Json(), c => { c.TrieNodeHistoryBlocks = 2; c.JournalBlocks = 2; });
            await server.Engine.ForkchoiceUpdatedAsync(DevChainAuthor.StateOf(server.Gate.HeadHash), null);
            foreach (var block in blocks) await ImportAsync(server, block);

            var response = await SetHeadAsync(server, "0x0");
            Assert.False(response.HasError, response.Error?.Message);
            Assert.Equal(0UL, server.Gate.HeadNumber);
            foreach (var block in blocks) await ImportAsync(server, block);
            foreach (var block in blocks)
                Assert.Equal(block.StateRoot, (await server.Bundle.Blocks.GetByNumberAsync((long)block.Number)).StateRoot);
        }

        [Fact]
        public async Task Given_a_head_at_N_When_debug_setHead_names_a_future_block_Then_the_request_is_refused_and_the_head_stays()
        {
            var blocks = await AuthorAsync(1);
            await using var server = await EngineOnlyServer.StartAsync(_workDir, CustomGenesisFixture.Json());
            await server.Engine.ForkchoiceUpdatedAsync(DevChainAuthor.StateOf(server.Gate.HeadHash), null);
            await ImportAsync(server, blocks[0]);

            var response = await SetHeadAsync(server, "0x5");

            Assert.True(response.HasError);
            Assert.Contains("future block", response.Error.Message);
            Assert.Equal(1UL, server.Gate.HeadNumber);
        }

        [Fact]
        public async Task Given_a_chain_on_a_custom_genesis_When_it_is_rolled_back_with_debug_setHead_and_replayed_Then_the_roots_are_identical_both_times_and_survive_a_restart()
        {
            var blocks = await AuthorAsync(10);
            var firstRoots = new List<byte[]>();
            var replayRoots = new List<byte[]>();

            await using (var server = await EngineOnlyServer.StartAsync(_workDir, CustomGenesisFixture.Json()))
            {
                await server.Engine.ForkchoiceUpdatedAsync(DevChainAuthor.StateOf(server.Gate.HeadHash), null);
                foreach (var block in blocks)
                {
                    await ImportAsync(server, block);
                    firstRoots.Add((await server.Bundle.Blocks.GetByNumberAsync((long)block.Number)).StateRoot);
                }

                Assert.False((await SetHeadAsync(server, "0x4")).HasError);
                Assert.Equal(4UL, server.Gate.HeadNumber);
                Assert.Null(await server.Bundle.Blocks.GetByNumberAsync(5));
                foreach (var block in blocks.Skip(4)) await ImportAsync(server, block);

                Assert.False((await SetHeadAsync(server, "0x0")).HasError);
                foreach (var block in blocks)
                {
                    await ImportAsync(server, block);
                    replayRoots.Add((await server.Bundle.Blocks.GetByNumberAsync((long)block.Number)).StateRoot);
                }
            }

            Assert.Equal(blocks.Select(b => b.StateRoot), firstRoots);
            Assert.Equal(firstRoots, replayRoots);

            await using var restarted = await EngineOnlyServer.StartAsync(_workDir, CustomGenesisFixture.Json());
            Assert.Equal(10UL, restarted.Gate.HeadNumber);
            Assert.Equal(blocks[^1].Hash, restarted.Gate.HeadHash);
            Assert.True(restarted.Bundle.StateTrieNodes.ContainsKey(blocks[^1].StateRoot));
        }
    }
}
