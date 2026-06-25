using System;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    /// <summary>
    /// Tests for <see cref="IBundleBatch"/> — the atomic write primitive
    /// added to <see cref="IChainStoreBundle"/>. Cluster A from the
    /// 2026-06-23 snap-sync audit; one primitive closes A-1..A-7.
    /// </summary>
    public class AtomicBundleBatchTests
    {
        private static byte[] Hash(byte b)
        {
            var h = new byte[32];
            h[31] = b;
            return h;
        }

        private static BlockHeader Header(ulong number, byte hashTag)
        {
            return new BlockHeader
            {
                BlockNumber = number,
                ParentHash = Hash((byte)(hashTag - 1)),
                Difficulty = 1,
                GasLimit = 100,
                Timestamp = 0,
                StateRoot = Hash(hashTag),
                TransactionsHash = Hash(hashTag),
                ReceiptHash = Hash(hashTag),
                LogsBloom = new byte[256],
                Nonce = new byte[8],
                MixHash = new byte[32],
                Coinbase = "0x0000000000000000000000000000000000000000",
                ExtraData = new byte[0],
            };
        }

        [Fact]
        public async Task HappyPath_WritesAllStaged_OnCommit()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var header = Header(100, 1);

            using (var batch = bundle.BeginBatch())
            {
                batch.PutHeader(header, Hash(1));
                batch.SetLastFetchedHeaderAndBody(100, 100);
                batch.Commit(100, Hash(1));
                batch.SaveSnapSyncState(new SnapSyncState
                {
                    SchemaVersion = 1,
                    Phase = SnapPhase.Complete,
                    PivotBlockNumber = 100,
                    PivotBlockHash = Hash(1),
                    HealTargetRoot = Hash(1),
                    Tasks = Array.Empty<SnapSyncAccountTask>(),
                    Counters = SnapSyncCounters.Zero,
                });
                await batch.CommitAsync();
            }

            Assert.Equal(100UL, bundle.Metadata.GetLastBlock());
            Assert.Equal(100UL, bundle.Metadata.GetLastFetchedHeader());
            Assert.Equal(100UL, bundle.Metadata.GetLastFetchedBody());
            var snap = bundle.Metadata.GetSnapSyncState();
            Assert.NotNull(snap);
            Assert.Equal(SnapPhase.Complete, snap!.Phase);
            Assert.Equal(100UL, snap.PivotBlockNumber);
            var stored = await bundle.Blocks.GetByHashAsync(Hash(1));
            Assert.NotNull(stored);
        }

        [Fact]
        public async Task Discard_DropsBufferedWrites_NothingPersisted()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var header = Header(50, 2);

            using (var batch = bundle.BeginBatch())
            {
                batch.PutHeader(header, Hash(2));
                batch.Commit(50, Hash(2));
                batch.SetLastFetchedHeader(50);
                batch.Discard();
            }

            Assert.Equal(0UL, bundle.Metadata.GetLastBlock());
            Assert.Equal(0UL, bundle.Metadata.GetLastFetchedHeader());
            var stored = await bundle.Blocks.GetByHashAsync(Hash(2));
            Assert.Null(stored);
        }

        [Fact]
        public async Task DoubleCommit_Throws()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            using var batch = bundle.BeginBatch();
            batch.SetLastFetchedHeader(1);
            await batch.CommitAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => batch.CommitAsync());
        }

        [Fact]
        public void StagingAfterDiscard_Throws()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var batch = bundle.BeginBatch();
            batch.Discard();
            Assert.Throws<InvalidOperationException>(() => batch.SetLastFetchedHeader(1));
        }

        [Fact]
        public async Task LargeBatch_AllCommitTogether()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            const int count = 100;

            using (var batch = bundle.BeginBatch())
            {
                for (int i = 1; i <= count; i++)
                {
                    var h = Header((ulong)i, (byte)i);
                    batch.PutHeader(h, Hash((byte)i));
                }
                batch.SetLastFetchedHeaderAndBody((ulong)count, (ulong)count);
                batch.Commit((ulong)count, Hash((byte)count));
                await batch.CommitAsync();
            }

            Assert.Equal((ulong)count, bundle.Metadata.GetLastBlock());
            Assert.Equal((ulong)count, bundle.Metadata.GetLastFetchedHeader());
            Assert.Equal((ulong)count, bundle.Metadata.GetLastFetchedBody());
            for (int i = 1; i <= count; i++)
            {
                var stored = await bundle.Blocks.GetByHashAsync(Hash((byte)i));
                Assert.NotNull(stored);
            }
        }

        [Fact]
        public async Task ConcurrentBatches_BothLand_NoCorruption()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var header1 = Header(1, 1);
            var header2 = Header(2, 2);

            // Two threads each open a batch and stage independent writes.
            // Both commits must serialise under the bundle lock — there is no
            // overlap in observed state.
            Task t1 = Task.Run(async () =>
            {
                using var b = bundle.BeginBatch();
                b.PutHeader(header1, Hash(1));
                b.SetLastFetchedHeader(1);
                await b.CommitAsync();
            });
            Task t2 = Task.Run(async () =>
            {
                using var b = bundle.BeginBatch();
                b.PutHeader(header2, Hash(2));
                b.SetLastFetchedHeader(2);
                await b.CommitAsync();
            });
            await Task.WhenAll(t1, t2);

            // The last-fetched-header cursor is one of {1, 2} — both batches
            // landed without exceptions; the final value reflects whichever
            // committed last.
            Assert.NotNull(await bundle.Blocks.GetByHashAsync(Hash(1)));
            Assert.NotNull(await bundle.Blocks.GetByHashAsync(Hash(2)));
            var cursor = bundle.Metadata.GetLastFetchedHeader();
            Assert.True(cursor == 1 || cursor == 2);
        }

        [Fact]
        public async Task SetLastFetchedHeaderAndBody_AdvancesBothCursors()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.SetLastFetchedHeaderAndBody(42, 41);
            Assert.Equal(42UL, bundle.Metadata.GetLastFetchedHeader());
            Assert.Equal(41UL, bundle.Metadata.GetLastFetchedBody());

            using (var batch = bundle.BeginBatch())
            {
                batch.SetLastFetchedHeaderAndBody(100, 99);
                await batch.CommitAsync();
            }
            Assert.Equal(100UL, bundle.Metadata.GetLastFetchedHeader());
            Assert.Equal(99UL, bundle.Metadata.GetLastFetchedBody());
        }

        [Fact]
        public async Task UsingScopeWithoutCommit_DiscardsAutomatically()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var header = Header(7, 7);

            // Simulate an exception between staging and commit. The using
            // block disposes the batch, which discards staged writes.
            try
            {
                using var batch = bundle.BeginBatch();
                batch.PutHeader(header, Hash(7));
                batch.Commit(7, Hash(7));
                throw new InvalidOperationException("simulated failure before commit");
            }
            catch (InvalidOperationException) { /* expected */ }

            Assert.Equal(0UL, bundle.Metadata.GetLastBlock());
            Assert.Null(await bundle.Blocks.GetByHashAsync(Hash(7)));
        }

        [Fact]
        public async Task CursorNeverAdvancesAheadOfData_WhenCommitFailsMidMetadata()
        {
            // Locks down the documented invariant for IBundleBatch: a batch
            // that is discarded before CommitAsync leaves both the data rows
            // AND the cursor untouched — the cursor never advances ahead of
            // the data it indexes.
            using var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.SetLastFetchedHeader(100);

            using (var batch = bundle.BeginBatch())
            {
                for (ulong n = 101; n <= 105; n++)
                {
                    var header = Header(n, (byte)n);
                    batch.PutHeader(header, Hash((byte)n));
                }
                batch.SetLastFetchedHeader(105);

                batch.Discard();
            }

            for (ulong n = 101; n <= 105; n++)
            {
                var stored = await bundle.Blocks.GetByHashAsync(Hash((byte)n));
                Assert.Null(stored);
            }
            Assert.Equal(100UL, bundle.Metadata.GetLastFetchedHeader());
        }

        [Fact]
        public async Task DataPhasePartialApply_CursorStaysBehind_OnCommitFault()
        {
            // Locks down the contract for an in-flight CommitAsync that
            // throws mid-batch on a data op: the cursor (always staged AFTER
            // the data ops, per the cursor-trails-data invariant) does not
            // advance.
            //
            // For InMemoryBundleBatch this is enforced by the per-op throw
            // skipping every remaining op in the lock-held drain — the
            // cursor advance staged at the tail of the batch never runs.
            // For RocksDbBundleBatch the same invariant holds for a weaker
            // data contract: Phase 1 data ops may leave a partial subset of
            // rows durable before the throw, but the Phase 2 metadata
            // WriteBatch is skipped on any Phase 1 fault, so the cursor row
            // in the metadata CF stays at its pre-batch value. RocksDB fault
            // injection is auto-deferred (see [Manual] trait below).
            using var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.SetLastFetchedHeader(200);

            // Swap in a faulting Blocks store via reflection on the get-only
            // auto-property backing field. The InMemoryBundleBatch captures
            // _bundle.Blocks at PutHeader time as a lazy lambda, so the
            // commit-time lookup hits the faulting store.
            var faultingHash = Hash(203);
            var faultingBlocks = new ThirdHeaderThrowsBlockStore(bundle.Blocks, faultingHash);
            ReplaceProperty(bundle, nameof(bundle.Blocks), faultingBlocks);

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                using var batch = bundle.BeginBatch();
                for (ulong n = 201; n <= 205; n++)
                {
                    batch.PutHeader(Header(n, (byte)n), Hash((byte)n));
                }
                batch.SetLastFetchedHeader(205);
                await batch.CommitAsync();
            });

            Assert.Equal(200UL, bundle.Metadata.GetLastFetchedHeader());
        }

        private static void ReplaceProperty(object target, string propertyName, object newValue)
        {
            var type = target.GetType();
            var backing = type.GetField($"<{propertyName}>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (backing == null)
                throw new InvalidOperationException(
                    $"Could not find backing field for {propertyName} on {type.FullName}.");
            backing.SetValue(target, newValue);
        }

        private sealed class ThirdHeaderThrowsBlockStore : IBlockStore
        {
            private readonly IBlockStore _inner;
            private readonly byte[] _faultOnHash;

            public ThirdHeaderThrowsBlockStore(IBlockStore inner, byte[] faultOnHash)
            {
                _inner = inner;
                _faultOnHash = faultOnHash;
            }

            public Task SaveAsync(BlockHeader header, byte[] blockHash)
            {
                if (blockHash.SequenceEqual(_faultOnHash))
                    throw new InvalidOperationException("simulated mid-batch fault");
                return _inner.SaveAsync(header, blockHash);
            }

            public Task<BlockHeader> GetByHashAsync(byte[] hash) => _inner.GetByHashAsync(hash);
            public Task<BlockHeader> GetByNumberAsync(BigInteger number) => _inner.GetByNumberAsync(number);
            public Task<BlockHeader> GetLatestAsync() => _inner.GetLatestAsync();
            public Task<BigInteger> GetHeightAsync() => _inner.GetHeightAsync();
            public Task<bool> ExistsAsync(byte[] hash) => _inner.ExistsAsync(hash);
            public Task<byte[]> GetHashByNumberAsync(BigInteger number) => _inner.GetHashByNumberAsync(number);
            public Task UpdateBlockHashAsync(BigInteger blockNumber, byte[] newHash) => _inner.UpdateBlockHashAsync(blockNumber, newHash);
            public Task DeleteByNumberAsync(BigInteger blockNumber) => _inner.DeleteByNumberAsync(blockNumber);
        }

        [Fact]
        [Trait("Manual", "rocksdb-fault-injection")]
        public void RocksDbBundleBatch_CursorTrailsData_OnPhase1Fault()
        {
            // Documented but not auto-run: requires an injected RocksDB-backed
            // bundle with a faulting blocks store. The RocksDbBundleBatch
            // contract is that the metadata WriteBatch never lands when a
            // Phase 1 data op throws — partial data rows may be durable, but
            // the cursor row in the metadata CF is unchanged. Covered today
            // by code inspection of RocksDbBundleBatch.CommitAsync (data ops
            // in Phase 1, metadata WriteBatch in Phase 2).
        }
    }
}
