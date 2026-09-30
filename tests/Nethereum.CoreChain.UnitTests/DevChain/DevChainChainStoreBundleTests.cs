using System;
using System.Numerics;
using Nethereum.CoreChain.Composition;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevChain.Composition;
using Nethereum.DevChain.Storage.Sqlite;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class DevChainChainStoreBundleTests
    {
        [Fact]
        public async System.Threading.Tasks.Task Given_ASqliteBackedBundle_When_EveryMemberIsExercised_Then_ItRoundTripsThroughTheSqliteStores()
        {
            using var manager = new SqliteStorageManager(dbPath: null, deleteOnDispose: true);
            using var bundle = DevChainChainStoreBundle.OpenSqlite(manager);

            Assert.IsType<SqliteBlockStore>(bundle.Blocks);
            Assert.IsType<SqliteTransactionStore>(bundle.Transactions);
            Assert.IsType<SqliteReceiptStore>(bundle.Receipts);
            Assert.IsType<SqliteLogStore>(bundle.Logs);
            Assert.IsType<SqliteTrieNodeStore>(bundle.TrieNodes);
            Assert.Same(bundle.TrieNodes, bundle.StateTrieNodes);
            Assert.IsType<SqliteBlockAccessListStore>(bundle.BlockAccessLists);
            Assert.IsType<HistoricalStateStore>(bundle.State);
            Assert.IsType<SqliteStateDiffStore>(bundle.Diffs);
            Assert.True(bundle.JournalEnabled);

            var header = new BlockHeader
            {
                ParentHash = new byte[32],
                UnclesHash = new byte[32],
                Coinbase = "0x0000000000000000000000000000000000000000",
                StateRoot = new byte[32],
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                LogsBloom = new byte[256],
                Difficulty = 0,
                BlockNumber = 0,
                GasLimit = 30_000_000,
                GasUsed = 0,
                Timestamp = 0,
                ExtraData = Array.Empty<byte>(),
                MixHash = new byte[32],
                Nonce = new byte[8],
            };
            var hash = new byte[32];
            hash[0] = 7;

            await bundle.Blocks.SaveAsync(header, hash);
            var readBack = await bundle.Blocks.GetHashByNumberAsync(BigInteger.Zero);
            Assert.Equal(hash, readBack);

            await bundle.Uncles.SaveAsync(hash, new System.Collections.Generic.List<BlockHeader>());
            Assert.Empty(await bundle.Uncles.GetByBlockHashAsync(hash));

            await bundle.Withdrawals.SaveAsync(hash, new System.Collections.Generic.List<Withdrawal>());

            var stateRoot = new byte[32];
            stateRoot[0] = 9;
            var checkpoint = await bundle.SaveCheckpointAsync(0, stateRoot, hash);
            Assert.Equal(0UL, checkpoint.BlockNumber);
            var checkpoints = await bundle.ListCheckpointsAsync();
            Assert.Single(checkpoints);
            await bundle.DeleteCheckpointAsync(0);
            Assert.Empty(await bundle.ListCheckpointsAsync());

            await Assert.ThrowsAsync<NotSupportedException>(() => bundle.RestoreCheckpointAsync(0));
            await Assert.ThrowsAsync<NotSupportedException>(() => bundle.ExportDatabaseAsync("ignored.db"));
            await bundle.ResetStateOnlyAsync();
            await bundle.ResetSnapBootstrapStateAsync();

            Assert.Equal(0, bundle.FreezerHead);
            Assert.Equal(0, bundle.ByHashIndexedHead);
            Assert.Equal(0, bundle.LogIndexRenderedHead);
            Assert.Equal(0, bundle.LogRenderProgressBlock);

            using (var batch = bundle.BeginBatch())
            {
                Assert.IsType<InMemoryBundleBatch>(batch);
                batch.Discard();
            }
        }

        [Fact]
        public void Given_AnInMemoryBundle_When_EveryMemberIsRead_Then_TheyAreAllPopulated()
        {
            using var bundle = DevChainChainStoreBundle.OpenInMemory();

            Assert.NotNull(bundle.State);
            Assert.NotNull(bundle.TrieNodes);
            Assert.NotNull(bundle.StateTrieNodes);
            Assert.NotNull(bundle.Blocks);
            Assert.NotNull(bundle.Transactions);
            Assert.NotNull(bundle.Uncles);
            Assert.NotNull(bundle.Withdrawals);
            Assert.NotNull(bundle.BlockAccessLists);
            Assert.NotNull(bundle.Receipts);
            Assert.NotNull(bundle.Logs);
            Assert.NotNull(bundle.Metadata);
            Assert.NotNull(bundle.Diffs);
            Assert.Null(bundle.NodeCommitBlockSource);
            Assert.Equal(string.Empty, bundle.ResolveCheckpointSnapshotPath(0));
        }

        [Fact]
        public void Given_ANullState_When_TheBundleIsConstructed_Then_ItThrows()
        {
            using var manager = new SqliteStorageManager(dbPath: null, deleteOnDispose: true);
            var blockStore = new SqliteBlockStore(manager);

            Assert.Throws<ArgumentNullException>(() => new DevChainChainStoreBundle(
                state: null,
                trieNodes: new SqliteTrieNodeStore(manager),
                blocks: blockStore,
                transactions: new SqliteTransactionStore(manager),
                blockAccessLists: new SqliteBlockAccessListStore(manager, blockStore),
                receipts: new SqliteReceiptStore(manager),
                logs: new SqliteLogStore(manager)));
        }

        [Fact]
        public void Given_ANullBlockStore_When_TheBundleIsConstructed_Then_ItThrows()
        {
            using var manager = new SqliteStorageManager(dbPath: null, deleteOnDispose: true);

            Assert.Throws<ArgumentNullException>(() => new DevChainChainStoreBundle(
                state: new StateLayer().Stores.DevChainSqlite(manager),
                trieNodes: new SqliteTrieNodeStore(manager),
                blocks: null,
                transactions: new SqliteTransactionStore(manager),
                blockAccessLists: null,
                receipts: new SqliteReceiptStore(manager),
                logs: new SqliteLogStore(manager)));
        }

        [Fact]
        public void Given_AStateStoreThatIsNotHistorical_When_TheBundleIsConstructed_Then_ItThrowsForDiffs()
        {
            using var manager = new SqliteStorageManager(dbPath: null, deleteOnDispose: true);
            var blockStore = new SqliteBlockStore(manager);

            Assert.Throws<ArgumentException>(() => new DevChainChainStoreBundle(
                state: new SqliteStateStore(manager),
                trieNodes: new SqliteTrieNodeStore(manager),
                blocks: blockStore,
                transactions: new SqliteTransactionStore(manager),
                blockAccessLists: new SqliteBlockAccessListStore(manager, blockStore),
                receipts: new SqliteReceiptStore(manager),
                logs: new SqliteLogStore(manager)));
        }
    }
}
