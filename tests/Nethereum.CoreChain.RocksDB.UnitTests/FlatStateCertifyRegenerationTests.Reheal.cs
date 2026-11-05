using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [Trait("Slice", "Flat")]
    public partial class FlatStateCertifyRegenerationTests
    {
        private static BlockHeader PivotHeader(long block, byte[] root) => new BlockHeader { BlockNumber = block, StateRoot = root };

        [Fact]
        public async Task Given_AnUnresolvableHoleDeepInsideAWhaleOwner_When_Repaired_Then_OnlyItsAncestorChainIsDeletedAndTheHealFetchesAboutDepthNodes()
        {
            var server = OpenBundle();
            var client = OpenBundle();
            var state = new OwnerState("whale-reheal", 20_000);
            state.Write(server);
            state.Write(client);
            var canonical = OwnerTrieRows(client.Rocks, state.Owner);
            var (_, divergedHole, slotUnder) = MixedHoles(state, client);
            DeleteRaw(client, state.Owner, divergedHole);
            client.Rocks.Put(RocksDbManager.CF_STATE_STORAGE, state.Owner.Concat(slotUnder).ToArray(), new byte[] { 0xEE, 0x01 });
            var scheduler = new ServingScheduler(server);
            var pivot = PivotHeader(900, state.Root);
            var rollingPivot = new SnapBootstrapper.RollingPivot(pivot, HashOf("pivot", 900));
            using var bound = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            var finalPivot = await SnapBootstrapper.ReconcileFlatStateAsync(
                client, scheduler, rollingPivot,
                (_, _) => Task.FromResult<(BlockHeader, byte[])?>((pivot, HashOf("pivot", 900))),
                null, NullLogger.Instance, bound.Token);

            Assert.Equal(state.Root, finalPivot.Header.StateRoot);
            Assert.True(scheduler.NodesServed > 0, "the hole was never fetched from a peer");
            Assert.True(scheduler.NodesServed <= 16, $"the repair fetched {scheduler.NodesServed} nodes; the ancestor chain of one hole is about a dozen");
            Assert.True(SameRows(canonical, OwnerTrieRows(client.Rocks, state.Owner)));
            Assert.Equal(state.Slots[slotUnder], client.Rocks.Get(RocksDbManager.CF_STATE_STORAGE, state.Owner.Concat(slotUnder).ToArray()));
            Assert.Equal(0, (await OracleVerify(client, state.Root)).TotalRepairs);
            Assert.Empty(client.GetPersistedDamage());
        }

        private static List<(byte[] Key, byte[] Value)> AccountTrieRows(RocksDbManager rocks)
        {
            var rows = new List<(byte[], byte[])>();
            using var it = rocks.CreateIterator(RocksDbManager.CF_STATE_TRIE_ACCOUNT);
            for (it.SeekToFirst(); it.Valid(); it.Next()) rows.Add((it.Key(), it.Value()));
            return rows;
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        public async Task Given_AnAccountTrieHoleUnderAProvenFlat_When_Finalizing_Then_OnlyItsAncestorChainIsRefetchedAndTheAccountTrieEndsCanonical(int depth)
        {
            var server = OpenBundle();
            var client = OpenBundle();
            var state = new OwnerState("account-hole-" + depth, 10);
            state.Write(server, eoas: 3_000);
            state.Write(client, eoas: 3_000);
            var canonical = AccountTrieRows(client.Rocks);
            var hole = canonical.First(r => r.Key.Length == depth).Key;
            client.Rocks.Delete(RocksDbManager.CF_STATE_TRIE_ACCOUNT, hole);
            var scheduler = new ServingScheduler(server);
            var pivot = PivotHeader(900, state.Root);
            using var bound = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            var finalPivot = await SnapBootstrapper.ReconcileFlatStateAsync(
                client, scheduler, new SnapBootstrapper.RollingPivot(pivot, HashOf("pivot", 900)),
                (_, _) => Task.FromResult<(BlockHeader, byte[])?>((pivot, HashOf("pivot", 900))),
                null, NullLogger.Instance, bound.Token);

            Assert.Equal(state.Root, finalPivot.Header.StateRoot);
            Assert.True(scheduler.NodesServed > 0, "the hole was never fetched from a peer");
            Assert.True(scheduler.NodesServed <= 16, $"the repair fetched {scheduler.NodesServed} nodes for one account-trie hole at depth {depth}");
            Assert.True(SameRows(canonical, AccountTrieRows(client.Rocks)), "the account trie is not byte-identical to canonical after the repair");
            Assert.Equal(0, (await OracleVerify(client, state.Root)).TotalRepairs);
            Assert.Empty(client.GetPersistedDamage());
        }

        [Fact]
        public async Task Given_StorageHolesUnderAProvenFlatAndAPivotOlderThan128Blocks_When_Finalizing_Then_TheHoleIsHealedAtTheCurrentPivotAndThatPivotIsPersisted()
        {
            var server = OpenBundle();
            var client = OpenBundle();
            var state = new OwnerState("proven-hole-finalize", 3_000);
            state.Write(server);
            state.Write(client);
            var canonical = OwnerTrieRows(client.Rocks, state.Owner);
            var (provenHole, _, _) = MixedHoles(state, client);
            DeleteRaw(client, state.Owner, provenHole);
            var scheduler = new ServingScheduler(server);
            var certified = PivotHeader(100, state.Root);
            using var bound = new CancellationTokenSource(TimeSpan.FromMinutes(2));

            var finalPivot = await SnapBootstrapper.ReconcileFlatStateAsync(
                client, scheduler, new SnapBootstrapper.RollingPivot(certified, HashOf("pivot", 100)),
                (_, _) => Task.FromResult<(BlockHeader, byte[])?>((PivotHeader(400, state.Root), HashOf("pivot", 400))),
                null, NullLogger.Instance, bound.Token);

            Assert.NotEmpty(scheduler.TrieNodeRoots);
            Assert.All(scheduler.TrieNodeRoots, root => Assert.Equal(state.Root, root));
            Assert.Equal(400, (long)finalPivot.Header.BlockNumber);
            Assert.True(SameRows(canonical, OwnerTrieRows(client.Rocks, state.Owner)));
            Assert.Equal(0, (await OracleVerify(client, state.Root)).TotalRepairs);
        }
    }
}
