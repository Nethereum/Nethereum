using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs.Engine;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Engine
{
    public class FollowerEngineApiServiceTests
    {
        private static readonly BigInteger Gwei = new BigInteger(1_000_000_000);

        [Fact]
        public async Task Given_an_engine_driven_follower_When_a_payload_extends_the_head_Then_it_is_imported_once_and_VALID()
        {
            var chain = await EngineFollowerHarness.AuthorChainAsync(Import.SideChainHarness.RecipientA, 2);
            using var follower = await EngineFollowerHarness.CreateAsync();
            await follower.ForkchoiceAsync(follower.GenesisHash);

            var first = await follower.NewPayloadAsync(chain[0]);
            var again = await follower.NewPayloadAsync(chain[0]);

            Assert.Equal(EnginePayloadStatus.Valid, first.Status);
            Assert.Equal(chain[0].Hash.ToHex(true), first.LatestValidHash);
            Assert.Equal(EnginePayloadStatus.Valid, again.Status);
            Assert.Equal(new ulong[] { 1 }, follower.Executor.Executed.ToArray());
            Assert.Equal(1UL, follower.Gate.HeadNumber);
            Assert.Equal(chain[0].Hash, follower.Gate.HeadHash);
        }

        [Fact]
        public async Task Given_a_payload_whose_parent_is_known_but_not_the_head_When_newPayload_Then_it_is_ACCEPTED_and_stashed_without_execution()
        {
            var chain = await EngineFollowerHarness.AuthorChainAsync(Import.SideChainHarness.RecipientA, 3);
            using var follower = await EngineFollowerHarness.CreateAsync();
            await follower.ForkchoiceAsync(follower.GenesisHash);
            await follower.NewPayloadAsync(chain[0]);
            await follower.NewPayloadAsync(chain[1]);

            var sibling = (await EngineFollowerHarness.AuthorSiblingsAsync(1)).BranchB;
            var status = await follower.NewPayloadAsync(sibling);

            Assert.Equal(EnginePayloadStatus.Accepted, status.Status);
            Assert.True(follower.Stash.Contains(sibling.Hash));
            Assert.Equal(new ulong[] { 1, 2 }, follower.Executor.Executed.ToArray());
            Assert.Equal(2UL, follower.Gate.HeadNumber);
        }

        [Fact]
        public async Task Given_a_payload_with_an_unknown_parent_When_newPayload_Then_SYNCING_and_the_devp2p_sync_targets_the_forkchoice_head()
        {
            var chain = await EngineFollowerHarness.AuthorChainAsync(Import.SideChainHarness.RecipientA, 3);
            using var follower = await EngineFollowerHarness.CreateAsync();
            await follower.ForkchoiceAsync(follower.GenesisHash);

            var status = await follower.NewPayloadAsync(chain[2]);
            var forkchoice = await follower.ForkchoiceAsync(chain[2].Hash);

            Assert.Equal(EnginePayloadStatus.Syncing, status.Status);
            Assert.Equal(EnginePayloadStatus.Syncing, forkchoice.PayloadStatus.Status);
            Assert.Equal(0, follower.Executor.ExecutedCount);
            var target = follower.Sink.Published[^1];
            Assert.Equal(chain[2].Hash, target.Head);
            Assert.Equal(3UL, target.HeadNumber);
        }

        [Fact]
        public async Task Given_a_forkchoice_head_known_to_no_one_When_forkchoiceUpdated_Then_SYNCING_and_no_sync_target_is_set()
        {
            var chain = await EngineFollowerHarness.AuthorChainAsync(Import.SideChainHarness.RecipientA, 1);
            using var follower = await EngineFollowerHarness.CreateAsync();

            var forkchoice = await follower.ForkchoiceAsync(chain[0].Hash);

            Assert.Equal(EnginePayloadStatus.Syncing, forkchoice.PayloadStatus.Status);
            Assert.Empty(follower.Sink.Published);
        }

        [Fact]
        public async Task Given_a_forkchoice_head_only_the_peers_have_When_forkchoiceUpdated_Then_the_header_comes_from_the_peers_and_becomes_the_sync_target()
        {
            var chain = await EngineFollowerHarness.AuthorChainAsync(Import.SideChainHarness.RecipientA, 1);
            using var follower = await EngineFollowerHarness.CreateAsync();
            follower.PeerHeaders[chain[0].Hash.ToHex(true)] = chain[0].Header;

            var forkchoice = await follower.ForkchoiceAsync(chain[0].Hash);

            Assert.Equal(EnginePayloadStatus.Syncing, forkchoice.PayloadStatus.Status);
            Assert.Equal(chain[0].Hash, Assert.Single(follower.Sink.Published).Head);
        }

        [Fact]
        public async Task Given_a_fresh_engine_node_at_genesis_When_newPayload_extends_genesis_before_any_forkchoice_Then_it_executes_and_returns_VALID()
        {
            var chain = await EngineFollowerHarness.AuthorChainAsync(Import.SideChainHarness.RecipientA, 1);
            using var follower = await EngineFollowerHarness.CreateAsync();

            var status = await follower.NewPayloadAsync(chain[0]);

            Assert.Equal(EnginePayloadStatus.Valid, status.Status);
            Assert.Equal(chain[0].Hash.ToHex(true), status.LatestValidHash);
            Assert.Equal(new ulong[] { 1 }, follower.Executor.Executed.ToArray());
            Assert.Equal(chain[0].Hash, follower.Gate.HeadHash);
        }

        [Fact]
        public async Task Given_a_fresh_engine_node_at_genesis_When_newPayload_with_an_unknown_parent_arrives_before_any_forkchoice_Then_it_is_SYNCING_and_nothing_executes()
        {
            var chain = await EngineFollowerHarness.AuthorChainAsync(Import.SideChainHarness.RecipientA, 2);
            using var follower = await EngineFollowerHarness.CreateAsync();

            var status = await follower.NewPayloadAsync(chain[1]);

            Assert.Equal(EnginePayloadStatus.Syncing, status.Status);
            Assert.Equal(0, follower.Executor.ExecutedCount);
            Assert.True(follower.Stash.Contains(chain[1].Hash));
        }

        [Fact]
        public async Task Given_forkchoiceUpdated_to_a_sibling_head_When_applied_Then_the_existing_reorg_path_runs()
        {
            var (common, branchA, branchB) = await EngineFollowerHarness.AuthorSiblingsAsync(2);
            using var follower = await EngineFollowerHarness.CreateAsync(recordExecutions: false);
            await follower.ForkchoiceAsync(follower.GenesisHash);
            foreach (var block in common) await follower.NewPayloadAsync(block);
            await follower.NewPayloadAsync(branchA);
            await follower.ForkchoiceAsync(branchA.Hash);
            Assert.Equal(branchA.Hash, follower.Gate.HeadHash);
            Assert.Equal(3 * Gwei, await follower.BalanceOfAsync(Import.SideChainHarness.RecipientA));
            Assert.Equal(BigInteger.Zero, await follower.BalanceOfAsync(Import.SideChainHarness.RecipientB));

            var stashed = await follower.NewPayloadAsync(branchB);
            var reorg = await follower.ForkchoiceAsync(branchB.Hash);

            Assert.Equal(EnginePayloadStatus.Accepted, stashed.Status);
            Assert.Equal(EnginePayloadStatus.Valid, reorg.PayloadStatus.Status);
            Assert.Equal(2 * Gwei, await follower.BalanceOfAsync(Import.SideChainHarness.RecipientA));
            Assert.Equal(Gwei, await follower.BalanceOfAsync(Import.SideChainHarness.RecipientB));
            Assert.Equal(branchB.Hash, follower.Gate.HeadHash);
            Assert.Equal(branchB.Hash, follower.Store.Bundle.Metadata.GetLastBlockHash());
            Assert.Equal(branchB.Header.StateRoot, (await follower.Store.Bundle.Blocks.GetByNumberAsync(3)).StateRoot);
            Assert.Equal(branchB.Hash, await follower.Store.Bundle.Blocks.GetHashByNumberAsync(new BigInteger(3)));
        }

        [Fact]
        public async Task Given_forkchoiceUpdated_to_the_current_head_When_applied_Then_it_is_a_no_op_that_rewinds_and_executes_nothing()
        {
            var chain = await EngineFollowerHarness.AuthorChainAsync(Import.SideChainHarness.RecipientA, 2);
            using var follower = await EngineFollowerHarness.CreateAsync();
            await follower.ForkchoiceAsync(follower.GenesisHash);
            foreach (var block in chain) await follower.NewPayloadAsync(block);

            var forkchoice = await follower.ForkchoiceAsync(chain[1].Hash);
            var toAncestor = await follower.ForkchoiceAsync(chain[0].Hash);

            Assert.Equal(EnginePayloadStatus.Valid, forkchoice.PayloadStatus.Status);
            Assert.Equal(EnginePayloadStatus.Valid, toAncestor.PayloadStatus.Status);
            Assert.Equal(new ulong[] { 1, 2 }, follower.Executor.Executed.ToArray());
            Assert.Equal(chain[1].Hash, follower.Gate.HeadHash);
            Assert.Equal(chain[1].Hash, follower.Store.Bundle.Metadata.GetLastBlockHash());
        }

        [Fact]
        public async Task Given_a_payload_whose_state_root_is_wrong_When_newPayload_Then_INVALID_with_the_parent_as_latestValidHash_and_its_descendants_stay_INVALID()
        {
            var chain = await EngineFollowerHarness.AuthorChainAsync(Import.SideChainHarness.RecipientA, 2);
            using var follower = await EngineFollowerHarness.CreateAsync();
            await follower.ForkchoiceAsync(follower.GenesisHash);

            var tampered = EngineFollowerHarness.PayloadOf(chain[0]);
            tampered.StateRoot = new string('1', 64).Insert(0, "0x");
            follower.Resealed(tampered);
            var status = await follower.Service.NewPayloadAsync(tampered, EngineFollowerHarness.ZeroRoot);

            var child = EngineFollowerHarness.PayloadOf(chain[1]);
            child.ParentHash = tampered.BlockHash;
            follower.Resealed(child);
            var childStatus = await follower.Service.NewPayloadAsync(child, EngineFollowerHarness.ZeroRoot);
            var headStatus = await follower.Service.ForkchoiceUpdatedAsync(
                new ForkchoiceStateV1 { HeadBlockHash = child.BlockHash, SafeBlockHash = child.BlockHash, FinalizedBlockHash = child.BlockHash },
                attributes: null);

            Assert.Equal(EnginePayloadStatus.Invalid, status.Status);
            Assert.Equal(follower.GenesisHash.ToHex(true), status.LatestValidHash);
            Assert.Equal(EnginePayloadStatus.Invalid, childStatus.Status);
            Assert.Equal(follower.GenesisHash.ToHex(true), childStatus.LatestValidHash);
            Assert.Equal(1, follower.Executor.ExecutedCount);
            Assert.Equal(EnginePayloadStatus.Invalid, headStatus.PayloadStatus.Status);
            Assert.Equal(0UL, follower.Gate.HeadNumber);
        }

        [Fact]
        public async Task Given_a_payload_whose_timestamp_is_not_after_its_parent_When_newPayload_Then_INVALID_without_execution()
        {
            var chain = await EngineFollowerHarness.AuthorChainAsync(Import.SideChainHarness.RecipientA, 2);
            using var follower = await EngineFollowerHarness.CreateAsync();
            await follower.ForkchoiceAsync(follower.GenesisHash);
            await follower.NewPayloadAsync(chain[0]);

            var stale = EngineFollowerHarness.PayloadOf(chain[1]);
            stale.Timestamp = new HexBigInteger(chain[0].Header.Timestamp);
            follower.Resealed(stale);
            var status = await follower.Service.NewPayloadAsync(stale, EngineFollowerHarness.ZeroRoot);

            Assert.Equal(EnginePayloadStatus.Invalid, status.Status);
            Assert.Equal(chain[0].Hash.ToHex(true), status.LatestValidHash);
            Assert.Equal(1, follower.Executor.ExecutedCount);
        }

        [Fact]
        public async Task Given_a_payload_whose_declared_hash_is_wrong_When_newPayload_Then_INVALID_BLOCK_HASH_without_execution()
        {
            var chain = await EngineFollowerHarness.AuthorChainAsync(Import.SideChainHarness.RecipientA, 1);
            using var follower = await EngineFollowerHarness.CreateAsync();
            await follower.ForkchoiceAsync(follower.GenesisHash);

            var payload = EngineFollowerHarness.PayloadOf(chain[0]);
            payload.BlockHash = new byte[32].ToHex(true);
            var status = await follower.Service.NewPayloadAsync(payload, EngineFollowerHarness.ZeroRoot);

            Assert.Equal(EnginePayloadStatus.InvalidBlockHash, status.Status);
            Assert.Equal(0, follower.Executor.ExecutedCount);
        }

        [Fact]
        public async Task Given_two_engine_calls_and_the_sync_loop_When_they_race_Then_each_block_is_imported_exactly_once()
        {
            var chain = await EngineFollowerHarness.AuthorChainAsync(Import.SideChainHarness.RecipientA, 2);
            using var follower = await EngineFollowerHarness.CreateAsync();
            await follower.ForkchoiceAsync(follower.GenesisHash);
            await follower.Gate.WaitAsync();

            var first = follower.NewPayloadAsync(chain[0]);
            var duplicate = follower.NewPayloadAsync(chain[0]);
            await Task.Delay(400);

            Assert.Equal(0, follower.Executor.ExecutedCount);
            Assert.False(first.IsCompleted);
            Assert.False(duplicate.IsCompleted);

            follower.Gate.Release();
            var statuses = await Task.WhenAll(first, duplicate);

            Assert.All(statuses, s => Assert.Equal(EnginePayloadStatus.Valid, s.Status));
            Assert.Equal(new ulong[] { 1 }, follower.Executor.Executed.ToArray());
        }

        [Fact]
        public async Task Given_concurrent_newPayload_calls_for_a_chain_When_they_run_against_the_gate_Then_blocks_execute_in_order_one_at_a_time()
        {
            var chain = await EngineFollowerHarness.AuthorChainAsync(Import.SideChainHarness.RecipientA, 4);
            using var follower = await EngineFollowerHarness.CreateAsync();
            await follower.ForkchoiceAsync(follower.GenesisHash);
            var inFlight = 0;
            var maxInFlight = 0;
            follower.Executor.BeforeProcess = async () =>
            {
                var now = System.Threading.Interlocked.Increment(ref inFlight);
                int seen;
                while (now > (seen = maxInFlight)) System.Threading.Interlocked.CompareExchange(ref maxInFlight, now, seen);
                await Task.Delay(30);
                System.Threading.Interlocked.Decrement(ref inFlight);
            };

            var calls = new List<Task<PayloadStatusV1>>();
            foreach (var block in chain) calls.Add(Task.Run(() => follower.NewPayloadAsync(block)));
            var statuses = await Task.WhenAll(calls);

            Assert.Equal(1, maxInFlight);
            Assert.Contains(statuses, s => s.Status == EnginePayloadStatus.Valid);
            Assert.Equal(statuses.Count(s => s.Status == EnginePayloadStatus.Valid), follower.Executor.ExecutedCount);
        }
    }
}
