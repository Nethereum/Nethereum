using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.RocksDB.UnitTests.Sync;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [Trait("Requires", "Externals")]
    public class RocksDbRecoveryDurableKeyTests : IDisposable
    {
        private const int Cadence = 4;
        private const int ExecutedBlocks = 8;
        private const ulong RewindTarget = 4;

        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"recovery_durable_k4_{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(_dir)) { try { Directory.Delete(_dir, true); } catch { } }
        }

        private RocksDbStorageOptions StorageOptions() => new RocksDbStorageOptions
        {
            DatabasePath = _dir,
            PathKeyedState = true,
            TrieNodeHistoryBlocks = 128,
            TrieNodeHistoryIndex = true,
        };

        private static HistoricalStateOptions JournalOptions()
            => new HistoricalStateOptions { MaxHistoryBlocks = 128, EnablePruning = false };

        private RocksDbChainStoreBundle OpenBundle()
            => RocksDbChainStoreBundle.Open(_dir, journalOptions: JournalOptions(), storageOptions: StorageOptions());

        private static async Task<byte[][]> ExecuteBlocksAsync(RocksDbChainStoreBundle bundle)
        {
            Assert.True(HiveTestdataFixture.Chain.Count >= ExecutedBlocks);
            await HiveTestdataFixture.PopulateGenesisAsync(bundle);
            var executor = FollowerStackBuilder.BuildPathKeyed(
                bundle, HiveTestdataFixture.ChainActivations, HiveTestdataFixture.HardforkConfigFactory,
                HiveTestdataFixture.ChainConfigFactory, flushCadence: new FixedIntervalFlushCadence(Cadence));

            var hashes = new byte[ExecutedBlocks + 1][];
            for (var i = 0; i < ExecutedBlocks; i++)
            {
                var b = HiveTestdataFixture.Chain[i];
                var number = (ulong)b.Header.BlockNumber;
                ((IAtomicBlockFlush)bundle).ArmWithdrawals(number, b.Withdrawals);
                var res = await executor.ProcessBlockAsync(b.Header, b.Transactions, b.Uncles, b.Withdrawals, CancellationToken.None)
                    .WaitAsync(TimeSpan.FromMinutes(2));
                Assert.True(res.RootMatches, $"block {number} should match its header");
                var owned = ((IAtomicBlockFlush)bundle).BlockOwnedByStagedFlush(number);
                if (!owned)
                {
                    await ((IAtomicBlockFlush)bundle).DrainAsync();
                    if (bundle.Metadata.GetLastBlock() < number)
                        bundle.Metadata.Commit(number, res.BlockHash);
                }
                var folded = ((IAtomicBlockFlush)bundle).WithdrawalsFoldedFor(number);
                if (!folded && b.Withdrawals != null && b.Withdrawals.Count > 0)
                    await bundle.Withdrawals.SaveAsync(res.BlockHash, b.Withdrawals);
                hashes[number] = res.BlockHash;
            }
            await ((IAtomicBlockFlush)bundle).DrainAsync();
            return hashes;
        }

        [Fact]
        public async Task Given_APathKeyedStoreAtCadenceFour_When_RecoverToAsyncRewindsBelowTheDurableBlock_Then_TheDurableBlockAndHashAreTheTarget()
        {
            Directory.CreateDirectory(_dir);
            RocksDbChainStoreBundle bundle;
            byte[][] hashes;
            using (bundle = OpenBundle())
            {
                hashes = await ExecuteBlocksAsync(bundle);
                Assert.Equal((ulong)ExecutedBlocks, bundle.Metadata.GetDurableStateBlock());

                var committed = await bundle.RecoverToAsync(RewindTarget, FlatRecoverySource.ReplayJournal, _ => { }, CancellationToken.None)
                    .WaitAsync(TimeSpan.FromMinutes(2));

                Assert.Equal(RewindTarget, committed);
                Assert.Equal(RewindTarget, bundle.Metadata.GetLastBlock());
                Assert.Equal(RewindTarget, bundle.Metadata.GetDurableStateBlock());
                Assert.Equal(hashes[RewindTarget],
                    bundle.Rocks.Get(RocksDbManager.CF_METADATA, RocksDbChainMetadataStore.MetaKeys.DurableStateHash));

                var root4 = HiveTestdataFixture.Chain[(int)RewindTarget - 1].Header.StateRoot;
                await bundle.SaveCheckpointAsync(RewindTarget, root4, hashes[RewindTarget]);
            }

            await bundle.RestoreCheckpointAsync(RewindTarget);

            using var reopened = OpenBundle();
            Assert.Equal(RewindTarget, reopened.Metadata.GetLastBlock());
            Assert.Equal(RewindTarget, reopened.Metadata.GetDurableStateBlock());
        }

        [Fact]
        public async Task Given_APathKeyedStoreAtCadenceFour_When_RecoverToAsyncTargetsTheDurableBlockItself_Then_TheDurableBlockIsUnchanged()
        {
            Directory.CreateDirectory(_dir);
            using var bundle = OpenBundle();
            var hashes = await ExecuteBlocksAsync(bundle);
            Assert.Equal((ulong)ExecutedBlocks, bundle.Metadata.GetDurableStateBlock());

            var committed = await bundle.RecoverToAsync(ExecutedBlocks, FlatRecoverySource.ReplayJournal, _ => { }, CancellationToken.None)
                .WaitAsync(TimeSpan.FromMinutes(2));

            Assert.Equal((ulong)ExecutedBlocks, committed);
            Assert.Equal((ulong)ExecutedBlocks, bundle.Metadata.GetDurableStateBlock());
            Assert.Equal(hashes[ExecutedBlocks],
                bundle.Rocks.Get(RocksDbManager.CF_METADATA, RocksDbChainMetadataStore.MetaKeys.DurableStateHash));
        }
    }
}
