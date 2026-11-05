using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;
using static Nethereum.CoreChain.RocksDB.UnitTests.GenerationTestState;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [Trait("Slice", "Flat")]
    public partial class FlatStateGenerationJournalTests
    {
        private static int UnitDoneKeys(RocksDbChainStoreBundle bundle)
            => Rows(bundle, RocksDbManager.CF_FLAT_GENERATION, new byte[] { 0x02 }).Count;

        [Fact]
        public async Task Given_AnUncommittedStore_When_GenerationCompletes_Then_TheJournalHoldsTheRootAndEveryUnitIsDone()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new GenerationTestState().WithEoas("journal-start", 500).WithContract("journal-start-contract", 50);
            state.Write(bundle);

            await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(state.Root, bundle.Rocks.Get(RocksDbManager.CF_FLAT_GENERATION, JournalRootKey));
            Assert.Equal(256, UnitDoneKeys(bundle));
        }

        [Fact]
        public async Task Given_ACompletedGeneration_When_ReconcileRunsAgainAtTheSameRoot_Then_ItReturnsWithoutHashingAUnit()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new GenerationTestState().WithEoas("journal-restart", 500);
            state.Write(bundle);
            await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);
            var lines = new ConcurrentQueue<string>();

            var again = await bundle.ReconcileFlatStateAsync(state.Root, lines.Enqueue, CancellationToken.None);

            Assert.Equal(256, UnitsDoneAtStart(lines));
            Assert.Equal(0, UnitsQueued(lines));
            Assert.Equal(0, again.AccountsScanned);
        }

        [Fact]
        public async Task Given_ACommittedNode_When_ReconcileRuns_Then_NoGenerationJournalIsReadOrWritten()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new GenerationTestState().WithEoas("committed", 500);
            state.Write(bundle);
            bundle.Metadata.Commit(5, RootOf("block5"));
            bundle.Rocks.Put(RocksDbManager.CF_FLAT_GENERATION, JournalRootKey, state.Root);
            for (int unit = 0; unit < 256; unit++)
                bundle.Rocks.Put(RocksDbManager.CF_FLAT_GENERATION, new byte[] { 0x02, (byte)unit }, Array.Empty<byte>());
            var journal = Rows(bundle, RocksDbManager.CF_FLAT_GENERATION);
            var diverged = HashOf("committed", 3);
            await new RocksDbStateStore(bundle.Rocks).SaveAccountByHashAsync(diverged, NewAccount(9, 9));

            var result = await bundle.ReconcileFlatStateAsync(state.Root, null, CancellationToken.None);

            Assert.Equal(1, result.AccountsPatched);
            Assert.Equal(500, result.AccountsScanned);
            Assert.True(SameRows(journal, Rows(bundle, RocksDbManager.CF_FLAT_GENERATION)));
        }

        private static async Task SaveHeaderAsync(RocksDbChainStoreBundle bundle, ulong number, byte[] stateRoot)
            => await bundle.Blocks.SaveAsync(new BlockHeader
            {
                BlockNumber = number,
                ParentHash = RootOf("parent" + number),
                StateRoot = stateRoot, TransactionsHash = new byte[32], ReceiptHash = new byte[32],
                UnclesHash = new byte[32], ExtraData = Array.Empty<byte>(), LogsBloom = new byte[256],
                Coinbase = "0x0000000000000000000000000000000000000000", Difficulty = 0, GasLimit = 0,
                GasUsed = 0, Timestamp = 0, MixHash = new byte[32], Nonce = new byte[8],
            }, RootOf("block" + number));

        [Fact]
        public async Task Given_ACommittedNodeWithAStorageHole_When_RecoverRuns_Then_ItThrowsWithTheHoleAndTheCursorIsUnchanged()
        {
            var bundle = OpenPathKeyedBundle();
            var state = new GenerationTestState().WithEoas("recover", 100).WithContract("recover-contract", 300);
            state.Write(bundle);
            await SaveHeaderAsync(bundle, 3, state.Root);
            bundle.Metadata.Commit(5, RootOf("block5"));
            var owner = HashOf("recover-contract", 0);
            var hole = Rows(bundle, RocksDbManager.CF_STATE_TRIE_STORAGE, owner).First(r => r.Key.Length == 33).Key;
            bundle.Rocks.Delete(RocksDbManager.CF_STATE_TRIE_STORAGE, hole);

            var thrown = await Assert.ThrowsAsync<FlatReconcileUnresolvableNodeException>(
                () => bundle.RecoverToAsync(3, FlatRecoverySource.ReconcileFromTrie, null, CancellationToken.None));

            Assert.Contains(thrown.MissingNodes, m => m.Owner.SequenceEqual(owner) && owner.Concat(m.Path).SequenceEqual(hole));
            Assert.Equal(5UL, bundle.Metadata.GetLastBlock());
            Assert.Null(bundle.Rocks.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, hole));
            Assert.Empty(Rows(bundle, RocksDbManager.CF_FLAT_GENERATION));
        }
    }
}
