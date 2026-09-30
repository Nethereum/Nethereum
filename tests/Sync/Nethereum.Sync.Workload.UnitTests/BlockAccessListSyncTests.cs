using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.Chain.TestData.Vectors;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class BlockAccessListSyncTests
    {
        private readonly ITestOutputHelper _out;

        public BlockAccessListSyncTests(ITestOutputHelper @out) => _out = @out;

        [Fact]
        public async Task Given_ASequencerProducingBlocksWithBlockAccessLists_When_AFollowerSyncsOverTheWire_Then_ItsStoredBlockAccessListsMatchTheProducersForEveryBlock()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 10, hardfork: "amsterdam");
            await new WorkloadV1().BuildAsync(sequencer);

            await using var server = await WireServerNode.StartAsync(sequencer);
            var (pool, _) = await SnapLoadTests.ConnectAsync(server);
            try
            {
                var follower = await FollowerNode.CreateAsync(sequencer);
                await follower.ConsumeAsync(sequencer);

                var height = (long)await sequencer.Blocks.GetHeightAsync();
                var nonEmptyBlocks = await CountBlocksWithNonEmptyBlockAccessListAsync(sequencer, height);
                _out.WriteLine($"{nonEmptyBlocks}/{height} blocks carried a non-empty block access list");
                Assert.True(nonEmptyBlocks > 0, "the workload produced no non-empty block access lists — the gate would be vacuous");

                await ChainEquivalence.AssertEquivalentAsync(sequencer.Stores, follower.Stores);
            }
            finally
            {
                await pool.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_ASnapSyncedFollower_When_ItForwardExecutesPastThePivot_Then_TheBlockAccessListItDerivesHashesToTheHeaderCommitment()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 10, hardfork: "amsterdam");
            await new WorkloadV1().BuildAsync(sequencer);
            await SnapLoadTests.ProduceEmptyTailAsync(sequencer, blocks: 90);

            await using var server = await WireServerNode.StartAsync(sequencer);
            var (pool, scheduler) = await SnapLoadTests.ConnectAsync(server);

            var head = (ulong)await server.Bundle.Blocks.GetHeightAsync();
            var headHeader = await server.Bundle.Blocks.GetByNumberAsync(head);
            var headHash = await server.Bundle.Blocks.GetHashByNumberAsync(head);
            var canonical = new TrustedTip(head, headHash, headHeader.StateRoot);

            var dbPath = Path.Combine(Path.GetTempPath(), "bal-snap-" + Guid.NewGuid().ToString("N"));
            try
            {
                var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dbPath });
                using var bundle = RocksDbChainStoreBundle.FromManager(manager, dbPath, journalOptions: HistoricalStateOptions.FullArchive);

                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                var activations = new FixedChainActivations(HardforkNames.Parse(sequencer.Hardfork));
                var snapResult = await SnapSyncOrchestrator.RunAsync(
                    bundle, pool, scheduler, canonical, activations, NullLogger.Instance,
                    new SnapSyncOrchestratorOptions { UseBackwardSkeleton = true }, cts.Token);
                Assert.True(snapResult.Ran, snapResult.SkipReason);
                await snapResult.HistoryBackfill;

                var pivot = (long)snapResult.PivotBlockNumber;
                var executed = await SnapLoadTests.CatchUpToTipAsync(bundle, sequencer);
                Assert.Equal((int)((long)head - pivot), executed);

                var nonEmptyCatchUpBlocks = 0;
                for (var n = pivot + 1; n <= (long)head; n++)
                {
                    var blockHash = await bundle.Blocks.GetHashByNumberAsync(n);
                    var header = await bundle.Blocks.GetByNumberAsync(n);
                    var appliedRlp = await bundle.BlockAccessLists.GetByBlockHashAsync(blockHash);

                    if (header.BlockAccessListHash == null)
                    {
                        Assert.Null(appliedRlp);
                        continue;
                    }

                    Assert.NotNull(appliedRlp);
                    var appliedHash = BlockAccessListRLPEncoder.Current.Hash(BlockAccessListRLPEncoder.Current.Decode(appliedRlp));
                    Assert.Equal(header.BlockAccessListHash.ToHex(), appliedHash.ToHex());

                    var producerRlp = await sequencer.BlockAccessLists.GetByBlockHashAsync(blockHash);
                    Assert.Equal(producerRlp, appliedRlp);

                    if (BlockAccessListRLPEncoder.Current.Decode(appliedRlp).Count > 0) nonEmptyCatchUpBlocks++;
                }

                _out.WriteLine($"{nonEmptyCatchUpBlocks}/{(long)head - pivot} catch-up blocks (pivot {pivot} to head {head}) carried a non-empty block access list");
                Assert.True(nonEmptyCatchUpBlocks > 0, "no catch-up block carried a non-empty block access list — the gate would be vacuous");
            }
            finally
            {
                await pool.DisposeAsync();
                if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true);
            }
        }

        private static async Task<long> CountBlocksWithNonEmptyBlockAccessListAsync(InProcessSequencerDriver sequencer, long height)
        {
            long count = 0;
            for (var n = 1L; n <= height; n++)
            {
                var hash = await sequencer.Blocks.GetHashByNumberAsync(n);
                var rlp = await sequencer.BlockAccessLists.GetByBlockHashAsync(hash);
                if (rlp != null && BlockAccessListRLPEncoder.Current.Decode(rlp).Any(a => HasAnyChange(a)))
                    count++;
            }
            return count;
        }

        private static bool HasAnyChange(AccountChanges account) =>
            account.StorageChanges.Count > 0 || account.StorageReads.Count > 0 ||
            account.BalanceChanges.Count > 0 || account.NonceChanges.Count > 0 || account.CodeChanges.Count > 0;

        private sealed class TrustedTip : ICanonicalStateRootSource
        {
            private readonly CanonicalTip _tip;
            public TrustedTip(ulong number, byte[] hash, byte[] stateRoot)
                => _tip = new CanonicalTip { BlockNumber = number, BlockHash = hash, StateRoot = stateRoot };
            public string Name => "BalGateTrustedTip";
            public Task<CanonicalTip> GetLatestAsync(CancellationToken ct) => Task.FromResult(_tip);
            public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(ulong blockNumber, CancellationToken ct)
                => Task.FromResult(blockNumber == _tip.BlockNumber ? (_tip.StateRoot, _tip.BlockHash) : ((byte[])null, (byte[])null));
        }
    }
}
