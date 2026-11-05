using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Engine;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.CoreChain.UnitTests.Import;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.MainnetChain.Bootstrap;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs.Engine;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Engine
{
    public class EngineDrivenFollowerEndToEndTests
    {
        private sealed class AcceptEverythingPolicy : IValidationPolicy
        {
            public bool ShouldAnchorAt(ulong blockNumber) => false;
            public ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber) => ValidationAction.Continue;
        }

        private sealed class EngineDrivenNode : IAsyncDisposable
        {
            private readonly CancellationTokenSource _cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            private Task<FollowerRunResult> _run;

            public EngineFollowerHarness Harness { get; private set; }
            public EngineForkchoiceHeaders Tips { get; private set; }

            public static async Task<EngineDrivenNode> StartAsync()
            {
                var node = new EngineDrivenNode();
                node.Harness = await EngineFollowerHarness.CreateAsync(recordExecutions: false, forwardTo: node.Tips = new EngineForkchoiceHeaders());
                var canonical = new TrustedHeaderCanonicalSource(node.Tips, useOptimistic: true);
                BackwardWalkerDelegate neverWalks = (f, h, t, b, c, n) =>
                    throw new InvalidOperationException("the engine-driven follower must not walk");
                var harness = node.Harness;
                node._run = new FollowerService(neverWalks, null, null).RunAsync(
                    new LocalReplayBlockSource(new List<BlockBundle>()),
                    () => harness.Store.Bundle,
                    _ => harness.Gate.Executor,
                    new AcceptEverythingPolicy(),
                    canonical,
                    new FollowerOptions(StartBlock: 0, CheckpointEvery: 0, AnchorEvery: 0,
                        MaxConsecutiveSourceFailures: 100_000, TipPollInterval: TimeSpan.FromMilliseconds(20))
                    { ExternalHeaderFollow = true, ChainWriteGate = harness.Gate },
                    node._cts.Token);
                return node;
            }

            public async Task<FollowerRunResult> StopAsync()
            {
                _cts.Cancel();
                return await _run;
            }

            public async Task WaitForHeadAsync(byte[] hash)
            {
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (DateTime.UtcNow < deadline)
                {
                    if (Harness.Gate.HasHead && System.Linq.Enumerable.SequenceEqual(Harness.Gate.HeadHash, hash)) return;
                    await Task.Delay(20);
                }
                _cts.Cancel();
                var result = await _run;
                throw new TimeoutException($"the follower never reached the expected head: {result.ExitReason} {result.Detail} executed={result.BlocksExecuted} mismatches={result.RootMismatches} head={Harness.Gate.HeadNumber}");
            }

            public async ValueTask DisposeAsync()
            {
                _cts.Cancel();
                try { await _run; } catch { }
                Harness.Dispose();
                _cts.Dispose();
            }
        }

        private static async Task SaveHeaderAndBodyAsync(IChainStoreBundle bundle, ProducedBlock block)
        {
            await bundle.Blocks.SaveAsync(block.Header, block.Hash);
            await bundle.Uncles.SaveAsync(block.Hash, new List<BlockHeader>());
            await bundle.Withdrawals.SaveAsync(block.Hash, new List<Withdrawal>(block.Withdrawals));
            await bundle.Transactions.SaveManyAsync(block.Hash, block.Header.BlockNumber, new List<ISignedTransaction>(block.Transactions));
        }

        [Fact]
        public async Task Given_a_devchain_acting_as_the_consensus_layer_When_it_sends_newPayload_and_forkchoiceUpdated_Then_the_engine_driven_follower_reaches_the_same_head_and_roots()
        {
            var (common, branchA, branchB) = await EngineFollowerHarness.AuthorSiblingsAsync(3);
            await using var node = await EngineDrivenNode.StartAsync();
            var follower = node.Harness;

            var genesis = await follower.ForkchoiceAsync(follower.GenesisHash);
            Assert.Equal(EnginePayloadStatus.Valid, genesis.PayloadStatus.Status);

            foreach (var block in common)
            {
                var imported = await follower.NewPayloadAsync(block);
                var adopted = await follower.ForkchoiceAsync(block.Hash, finalized: common[0].Hash);
                Assert.Equal(EnginePayloadStatus.Valid, imported.Status);
                Assert.Equal(block.Hash.ToHex(true), imported.LatestValidHash);
                Assert.Equal(EnginePayloadStatus.Valid, adopted.PayloadStatus.Status);
            }

            await node.WaitForHeadAsync(common[^1].Hash);
            for (var number = 1; number <= common.Count; number++)
            {
                var stored = await follower.Store.Bundle.Blocks.GetByNumberAsync(number);
                Assert.Equal(common[number - 1].Header.StateRoot, stored.StateRoot);
                Assert.Equal(common[number - 1].Hash, await follower.Store.Bundle.Blocks.GetHashByNumberAsync(new BigInteger(number)));
            }
            Assert.Equal(common[^1].Hash, follower.Store.Bundle.Metadata.GetLastBlockHash());
            Assert.Equal(common[0].Hash, follower.Sink.Published[^1].Finalized);

            var result = await node.StopAsync();
            Assert.Equal(0UL, result.RootMismatches);
        }

        [Fact]
        public async Task Given_a_forkchoice_head_ahead_of_the_follower_When_the_sync_has_the_blocks_Then_the_tip_loop_executes_them_under_the_gate_and_the_roots_match()
        {
            var chain = await EngineFollowerHarness.AuthorChainAsync(SideChainHarness.RecipientA, 4);
            await using var node = await EngineDrivenNode.StartAsync();
            var follower = node.Harness;
            await follower.ForkchoiceAsync(follower.GenesisHash);
            await follower.NewPayloadAsync(chain[0]);
            await follower.ForkchoiceAsync(chain[0].Hash);

            for (var i = 1; i < chain.Count; i++) await SaveHeaderAndBodyAsync(follower.Store.Bundle, chain[i]);
            var syncing = await follower.ForkchoiceAsync(chain[3].Hash);

            Assert.Equal(EnginePayloadStatus.Syncing, syncing.PayloadStatus.Status);
            await node.WaitForHeadAsync(chain[3].Hash);
            for (var number = 1; number <= 4; number++)
            {
                var stored = await follower.Store.Bundle.Blocks.GetByNumberAsync(number);
                Assert.Equal(chain[number - 1].Header.StateRoot, stored.StateRoot);
            }
            Assert.Equal(4UL, follower.Gate.HeadNumber);
            var result = await node.StopAsync();
            Assert.Equal(0UL, result.RootMismatches);
            Assert.Equal(4UL, result.BlocksExecuted);
            Assert.Equal(4UL, result.LastExecutedBlock);
        }

        [Fact]
        public async Task Given_the_consensus_layer_reorgs_to_a_sibling_When_newPayload_then_forkchoiceUpdated_arrive_Then_the_follower_follows_the_sibling_and_its_state_matches()
        {
            var (common, branchA, branchB) = await EngineFollowerHarness.AuthorSiblingsAsync(2);
            await using var node = await EngineDrivenNode.StartAsync();
            var follower = node.Harness;
            await follower.ForkchoiceAsync(follower.GenesisHash);
            foreach (var block in common)
            {
                await follower.NewPayloadAsync(block);
                await follower.ForkchoiceAsync(block.Hash);
            }
            await follower.NewPayloadAsync(branchA);
            await follower.ForkchoiceAsync(branchA.Hash);
            await node.WaitForHeadAsync(branchA.Hash);

            var stashed = await follower.NewPayloadAsync(branchB);
            var reorg = await follower.ForkchoiceAsync(branchB.Hash);

            Assert.Equal(EnginePayloadStatus.Accepted, stashed.Status);
            Assert.Equal(EnginePayloadStatus.Valid, reorg.PayloadStatus.Status);
            await node.WaitForHeadAsync(branchB.Hash);
            Assert.Equal(branchB.Hash, await follower.Store.Bundle.Blocks.GetHashByNumberAsync(new BigInteger(3)));
            Assert.Equal(branchB.Header.StateRoot, (await follower.Store.Bundle.Blocks.GetByNumberAsync(3)).StateRoot);
            Assert.Equal(2 * 1_000_000_000L + 0, (long)await follower.BalanceOfAsync(SideChainHarness.RecipientA));
            Assert.Equal(1_000_000_000L, (long)await follower.BalanceOfAsync(SideChainHarness.RecipientB));
            var result = await node.StopAsync();
            Assert.Equal(0UL, result.RootMismatches);
        }

        [Fact]
        public async Task Given_no_forkchoiceUpdated_When_a_payload_extends_genesis_Then_the_engine_executes_it_and_the_follower_result_counts_it()
        {
            var chain = await EngineFollowerHarness.AuthorChainAsync(SideChainHarness.RecipientA, 1);
            await using var node = await EngineDrivenNode.StartAsync();
            var follower = node.Harness;

            var status = await follower.NewPayloadAsync(chain[0]);
            await Task.Delay(300);
            var result = await node.StopAsync();

            Assert.Equal(EnginePayloadStatus.Valid, status.Status);
            Assert.Equal(1UL, follower.Gate.HeadNumber);
            Assert.Equal(1UL, result.BlocksExecuted);
            Assert.Equal(1UL, result.LastExecutedBlock);
        }
    }
}
