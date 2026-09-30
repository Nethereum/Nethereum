using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Freezer.Codecs;
using Nethereum.CoreChain.Storage;
using Nethereum.Freezer;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using FreezerCore = Nethereum.Freezer.Freezer;

namespace Nethereum.CoreChain.Freezer.UnitTests
{
    public class FreezerHistoryStoreTests
    {

        private sealed class Harness : IDisposable
        {
            public FreezerHistoryStore Store { get; }
            public FreezerCore Freezer { get; }
            public FreezerCodecSet Codecs { get; }
            public InMemoryRandomKeyIndexStore Index { get; }
            public DecodedClusterCache Cache { get; }

            public Harness(FreezerHistoryStore store, FreezerCore freezer, FreezerCodecSet codecs,
                InMemoryRandomKeyIndexStore index, DecodedClusterCache cache)
            {
                Store = store;
                Freezer = freezer;
                Codecs = codecs;
                Index = index;
                Cache = cache;
            }

            public void Dispose() => Freezer.Dispose();
        }

        private static Harness OpenCorpusStore()
        {
            var freezer = FreezerCore.Open(new FreezerLayout(CorpusFixture.TxsSliceDirectory), FreezerOpenMode.ReadOnly);
            var codecs = new FreezerCodecSet();
            var signer = new TransactionVerificationAndRecoveryImp();
            var deriver = new ReceiptFieldDeriver(signer, new CancunBlobBaseFeeFractionResolver());
            var index = new InMemoryRandomKeyIndexStore();
            var cache = new DecodedClusterCache(64);
            var store = new FreezerHistoryStore(freezer, codecs, deriver, signer, index, cache);
            return new Harness(store, freezer, codecs, index, cache);
        }

        private static void PopulateIndexFully(Harness h)
        {
            for (var item = 0L; item < h.Freezer.Items; item++)
            {
                var cluster = h.Freezer.ReadCluster(item);
                h.Index.PutBlockHash(h.Codecs.Hashes.Decode(cluster.Hash), item);

                var body = h.Codecs.Bodies.Decode(cluster.Body);
                for (var t = 0; t < body.Txs.Count; t++)
                    h.Index.PutTxLocation(body.Txs[t].Hash, item, t);
            }
        }


        [Fact]
        public async Task Given_RealCorpus_When_GetByNumber_Then_HeaderMatches()
        {
            using var h = OpenCorpusStore();

            var header = await h.Store.GetByNumberAsync(0);

            Assert.Equal(1_500_000L, header.BlockNumber.ToLong());

            var oracle = new HeaderItemCodec().Decode(
                CorpusFixture.ReadRawItem(CorpusFixture.TxsSliceDirectory, "headers", compressed: true, itemNumber: 0));
            Assert.Equal(oracle.ParentHash, header.ParentHash);
            Assert.Equal(oracle.ReceiptHash, header.ReceiptHash);
        }

        [Fact]
        public async Task Given_GetHeightAsync_Then_ReturnsItemsMinusOne()
        {
            using var h = OpenCorpusStore();

            var height = await h.Store.GetHeightAsync();

            Assert.Equal((BigInteger)(h.Freezer.Items - 1), height);
        }


        [Fact]
        public async Task Given_RealCorpus_When_GetTxsByBlockNumber_Then_CountAndFirstTxHashMatch()
        {
            using var h = OpenCorpusStore();

            var storeTxs = await h.Store.GetByBlockNumberAsync(0);

            var oracleBody = new BodyClusterItemCodec().Decode(
                CorpusFixture.ReadRawItem(CorpusFixture.TxsSliceDirectory, "bodies", compressed: true, itemNumber: 0));

            Assert.NotEmpty(storeTxs);
            Assert.Equal(oracleBody.Txs.Count, storeTxs.Count);
            Assert.Equal(oracleBody.Txs[0].Hash, storeTxs[0].Hash);
        }


        [Fact]
        public async Task Given_RealCorpus_When_GetReceiptsByBlockNumber_Then_MatchDeriver()
        {
            using var h = OpenCorpusStore();

            var storeReceipts = await ((IReceiptStore)h.Store).GetByBlockNumberAsync(0);

            var header = new HeaderItemCodec().Decode(CorpusFixture.ReadRawItem(CorpusFixture.TxsSliceDirectory, "headers", true, 0));
            var body = new BodyClusterItemCodec().Decode(CorpusFixture.ReadRawItem(CorpusFixture.TxsSliceDirectory, "bodies", true, 0));
            var stored = new ReceiptsItemCodec().Decode(CorpusFixture.ReadRawItem(CorpusFixture.TxsSliceDirectory, "receipts", true, 0));
            var signer = new TransactionVerificationAndRecoveryImp();
            var deriver = new ReceiptFieldDeriver(signer, new CancunBlobBaseFeeFractionResolver());
            var expected = deriver.Derive(header, body, stored);

            Assert.Equal(body.Txs.Count, storeReceipts.Count);
            Assert.Equal(expected.Count, storeReceipts.Count);
            for (var i = 0; i < expected.Count; i++)
                Assert.Equal(expected[i].Bloom, storeReceipts[i].Bloom);
        }


        [Fact]
        public async Task Given_PopulatedIndex_When_GetByBlockHash_Then_ResolvesViaIndexToRightBlock()
        {
            using var h = OpenCorpusStore();
            PopulateIndexFully(h);

            var hash = h.Codecs.Hashes.Decode(h.Freezer.ReadCluster(5).Hash);

            var header = await h.Store.GetByHashAsync(hash);

            Assert.NotNull(header);
            Assert.Equal(1_500_005L, header.BlockNumber.ToLong());
        }

        [Fact]
        public async Task Given_PopulatedIndex_When_GetTxByHash_Then_ResolvesToRightTx()
        {
            using var h = OpenCorpusStore();
            PopulateIndexFully(h);

            var body = h.Codecs.Bodies.Decode(h.Freezer.ReadCluster(0).Body);
            Assert.NotEmpty(body.Txs);
            var expectedTx = body.Txs[0];

            var tx = await ((ITransactionStore)h.Store).GetByHashAsync(expectedTx.Hash);

            Assert.NotNull(tx);
            Assert.Equal(expectedTx.Hash, tx.Hash);
        }

        [Fact]
        public async Task Given_UnpopulatedIndex_When_GetByHashOrExists_Then_NullAndFalse()
        {
            using var h = OpenCorpusStore();

            var hash = h.Codecs.Hashes.Decode(h.Freezer.ReadCluster(0).Hash);

            Assert.Null(await h.Store.GetByHashAsync(hash));
            Assert.False(await h.Store.ExistsAsync(hash));
        }


        [Fact]
        public async Task Given_TxsAndReceiptsRequestedForSameBlock_Then_ClusterDecodedOnce()
        {
            using var h = OpenCorpusStore();

            await h.Store.GetByBlockNumberAsync(0);
            await ((IReceiptStore)h.Store).GetByBlockNumberAsync(0);

            Assert.Equal(1, h.Cache.Count);
        }


        [Fact]
        public async Task Given_FrozenBlock_When_DeleteByNumberAsync_Then_ThrowsFreezerImmutableException()
        {
            using var h = OpenCorpusStore();
            var frozenBlock = h.Freezer.Items - 1;

            await Assert.ThrowsAsync<FreezerImmutableException>(() => h.Store.DeleteByNumberAsync(frozenBlock));
        }

        [Fact]
        public async Task Given_BlockAboveFreezerItems_When_DeleteByNumberAsync_Then_NoOp()
        {
            using var h = OpenCorpusStore();
            var aboveFreezer = h.Freezer.Items;

            await h.Store.DeleteByNumberAsync(aboveFreezer);
        }

        [Fact]
        public async Task Given_DeleteAtItemsBoundary_When_DeleteByNumberAsync_Then_LastFrozenThrowsAndFirstUnfrozenNoOps()
        {
            using var h = OpenCorpusStore();
            var items = h.Freezer.Items;

            await Assert.ThrowsAsync<FreezerImmutableException>(() => h.Store.DeleteByNumberAsync(items - 1));
            await h.Store.DeleteByNumberAsync(items);
        }

        [Fact]
        public async Task Given_FrozenBlock_When_UpdateBlockHashAsync_Then_ThrowsFreezerImmutableException()
        {
            using var h = OpenCorpusStore();
            var frozenBlock = h.Freezer.Items - 1;

            await Assert.ThrowsAsync<FreezerImmutableException>(() => h.Store.UpdateBlockHashAsync(frozenBlock, new byte[32]));
        }

        [Fact]
        public async Task Given_UnresolvedHash_When_DeleteByBlockHashAsync_Then_NoOp()
        {
            using var h = OpenCorpusStore();

            await ((IBlockAccessListStore)h.Store).DeleteByBlockHashAsync(new byte[32]);
        }


        [Fact]
        public async Task Given_SaveAsync_Then_ThrowsInvalidOperationOnEveryInterface()
        {
            using var h = OpenCorpusStore();
            IBlockStore blockStore = h.Store;
            ITransactionStore txStore = h.Store;
            IReceiptStore receiptStore = h.Store;
            IBlockAccessListStore balStore = h.Store;

            await Assert.ThrowsAsync<InvalidOperationException>(() => blockStore.SaveAsync(null, null));
            await Assert.ThrowsAsync<InvalidOperationException>(() => txStore.SaveAsync(null, null, 0, 0));
            await Assert.ThrowsAsync<InvalidOperationException>(() => receiptStore.SaveAsync(null, null, null, 0, 0, 0, null, 0));
            await Assert.ThrowsAsync<InvalidOperationException>(() => balStore.SaveAsync(null, null));
        }


        [Fact]
        public void Given_SyntheticCluster_When_ReadBackThroughCoordinator_Then_WriteReadSymmetric()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "freezer-codecset-roundtrip-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var codecs = new FreezerCodecSet();
                var header = SyntheticHeader(1);
                var hash = new byte[32];
                hash[0] = 0xAB;
                var body = new BlockBodyCluster(new List<ISignedTransaction>(), new List<BlockHeader>(), null);
                var stored = new List<ReceiptForStorage>();
                var bal = Array.Empty<byte>();

                using (var freezer = FreezerCore.Open(new FreezerLayout(tempDir), FreezerOpenMode.Append))
                {
                    var cluster = new FrozenBlockCluster(
                        codecs.Headers.Encode(header),
                        hash,
                        codecs.Bodies.Encode(body),
                        codecs.Receipts.Encode(stored),
                        codecs.Bals.Encode(bal));

                    var batch = freezer.BeginBatch();
                    batch.AppendCluster(0, cluster);
                    batch.Commit();
                }

                using var reopened = FreezerCore.Open(new FreezerLayout(tempDir), FreezerOpenMode.ReadOnly);
                var readBack = reopened.ReadCluster(0);

                Assert.Equal(1L, codecs.Headers.Decode(readBack.Header).BlockNumber.ToLong());
                Assert.Equal(hash, codecs.Hashes.Decode(readBack.Hash));
                Assert.Empty(codecs.Bodies.Decode(readBack.Body).Txs);
                Assert.Empty(codecs.Receipts.Decode(readBack.Receipts));
                Assert.Empty(codecs.Bals.Decode(readBack.Bal));
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }


        private sealed class UncleWithdrawalHarness : IDisposable
        {
            public FreezerHistoryStore Store { get; }
            public byte[] PreShanghaiHash { get; }
            public byte[] PostShanghaiHash { get; }
            private readonly FreezerCore _freezer;
            private readonly string _tempDir;

            public UncleWithdrawalHarness(FreezerHistoryStore store, FreezerCore freezer, string tempDir,
                byte[] preShanghaiHash, byte[] postShanghaiHash)
            {
                Store = store;
                _freezer = freezer;
                _tempDir = tempDir;
                PreShanghaiHash = preShanghaiHash;
                PostShanghaiHash = postShanghaiHash;
            }

            public void Dispose()
            {
                _freezer.Dispose();
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        private static UncleWithdrawalHarness OpenSyntheticUncleWithdrawalStore()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "freezer-uncle-withdrawal-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var codecs = new FreezerCodecSet();

            var uncle = SyntheticHeader(50);
            var preShanghaiHeader = SyntheticHeader(100);
            var preShanghaiHash = new byte[32];
            preShanghaiHash[0] = 0xA0;
            var preShanghaiBody = new BlockBodyCluster(new List<ISignedTransaction>(), new List<BlockHeader> { uncle }, null);

            var postShanghaiHeader = SyntheticHeader(101);
            var postShanghaiHash = new byte[32];
            postShanghaiHash[0] = 0xB0;
            var withdrawals = new List<Withdrawal>
            {
                new() { Index = 1, ValidatorIndex = 11, Address = new byte[20], AmountInGwei = 1000 },
                new() { Index = 2, ValidatorIndex = 12, Address = new byte[20], AmountInGwei = 2000 },
            };
            var postShanghaiBody = new BlockBodyCluster(new List<ISignedTransaction>(), new List<BlockHeader>(), withdrawals);

            var stored = new List<ReceiptForStorage>();
            var bal = Array.Empty<byte>();

            using (var writer = FreezerCore.Open(new FreezerLayout(tempDir), FreezerOpenMode.Append))
            {
                var batch = writer.BeginBatch();
                batch.AppendCluster(0, new FrozenBlockCluster(
                    codecs.Headers.Encode(preShanghaiHeader), preShanghaiHash,
                    codecs.Bodies.Encode(preShanghaiBody), codecs.Receipts.Encode(stored), codecs.Bals.Encode(bal)));
                batch.AppendCluster(1, new FrozenBlockCluster(
                    codecs.Headers.Encode(postShanghaiHeader), postShanghaiHash,
                    codecs.Bodies.Encode(postShanghaiBody), codecs.Receipts.Encode(stored), codecs.Bals.Encode(bal)));
                batch.Commit();
            }

            var freezer = FreezerCore.Open(new FreezerLayout(tempDir), FreezerOpenMode.ReadOnly);
            var signer = new TransactionVerificationAndRecoveryImp();
            var deriver = new ReceiptFieldDeriver(signer, new CancunBlobBaseFeeFractionResolver());
            var index = new InMemoryRandomKeyIndexStore();
            index.PutBlockHash(preShanghaiHash, 0);
            index.PutBlockHash(postShanghaiHash, 1);
            var cache = new DecodedClusterCache(64);
            var store = new FreezerHistoryStore(freezer, codecs, deriver, signer, index, cache);

            return new UncleWithdrawalHarness(store, freezer, tempDir, preShanghaiHash, postShanghaiHash);
        }

        [Fact]
        public async Task Given_FrozenBlockWithUncles_When_GetUnclesByNumber_Then_ReturnsUncles()
        {
            using var h = OpenSyntheticUncleWithdrawalStore();
            IUncleStore uncleStore = h.Store;

            var uncles = await uncleStore.GetByBlockNumberAsync(0);

            Assert.NotNull(uncles);
            var uncle = Assert.Single(uncles);
            Assert.Equal(50L, uncle.BlockNumber.ToLong());
        }

        [Fact]
        public async Task Given_FrozenPostShanghaiBlock_When_GetWithdrawalsByNumber_Then_ReturnsWithdrawals()
        {
            using var h = OpenSyntheticUncleWithdrawalStore();
            IWithdrawalStore withdrawalStore = h.Store;

            var withdrawals = await withdrawalStore.GetByBlockNumberAsync(1);

            Assert.NotNull(withdrawals);
            Assert.Equal(2, withdrawals.Count);
            Assert.Equal(1UL, withdrawals[0].Index);
            Assert.Equal(2UL, withdrawals[1].Index);
        }

        [Fact]
        public async Task Given_FrozenPreShanghaiBlock_When_GetWithdrawals_Then_Null()
        {
            using var h = OpenSyntheticUncleWithdrawalStore();
            IWithdrawalStore withdrawalStore = h.Store;

            var withdrawals = await withdrawalStore.GetByBlockNumberAsync(0);

            Assert.Null(withdrawals);
        }

        [Fact]
        public async Task Given_FrozenBlockWithUncles_When_GetUnclesByBlockHash_Then_ResolvesSameAsByNumber()
        {
            using var h = OpenSyntheticUncleWithdrawalStore();
            IUncleStore uncleStore = h.Store;

            var byHash = await uncleStore.GetByBlockHashAsync(h.PreShanghaiHash);
            var byNumber = await uncleStore.GetByBlockNumberAsync(0);

            Assert.NotNull(byHash);
            Assert.Equal(byNumber.Count, byHash.Count);
            Assert.Equal(byNumber[0].BlockNumber.ToLong(), byHash[0].BlockNumber.ToLong());
        }

        private static BlockHeader SyntheticHeader(long blockNumber) => new()
        {
            ParentHash = new byte[32],
            UnclesHash = new byte[32],
            Coinbase = "0x0000000000000000000000000000000000000000",
            StateRoot = new byte[32],
            TransactionsHash = new byte[32],
            ReceiptHash = new byte[32],
            BlockNumber = new EvmUInt256((ulong)blockNumber),
            LogsBloom = new byte[256],
            Difficulty = EvmUInt256.Zero,
            Timestamp = 0,
            GasLimit = 30_000_000,
            GasUsed = 0,
            MixHash = new byte[32],
            ExtraData = Array.Empty<byte>(),
            Nonce = new byte[8],
        };
    }
}
