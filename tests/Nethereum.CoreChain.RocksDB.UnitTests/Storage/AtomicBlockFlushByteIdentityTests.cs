using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.RocksDB.UnitTests.Sync;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Storage
{
    public class AtomicBlockFlushByteIdentityTests : IDisposable
    {
        private readonly string _dir;

        public AtomicBlockFlushByteIdentityTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-atomicblockflush-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }

        private string SubDir(string name)
        {
            var path = Path.Combine(_dir, name);
            Directory.CreateDirectory(path);
            return path;
        }

        private static BlockImporter BuildImporter(IChainStoreBundle bundle, IAtomicBlockFlush atomicFlush)
        {
            var calculator = new IncrementalStateRootCalculator(bundle.State, bundle.TrieNodes);
            var engine = new BlockExecutor(
                bundle.State,
                bundle.Blocks,
                HiveTestdataFixture.ChainActivations,
                HiveTestdataFixture.ChainConfigFactory,
                HiveTestdataFixture.HardforkConfigFactory,
                calculator,
                EthereumProofOfWorkRewardPolicy.Instance,
                bundle.TrieNodes);
            return new BlockImporter(
                engine,
                bundle.Blocks,
                bundle.State,
                transactionStore: bundle.Transactions,
                receiptStore: bundle.Receipts,
                logStore: bundle.Logs,
                uncleStore: bundle.Uncles,
                atomicFlush: atomicFlush);
        }

        private static Dictionary<string, byte[]> DumpCf(RocksDbManager mgr, string cf)
        {
            var result = new Dictionary<string, byte[]>();
            using var it = mgr.CreateIterator(cf);
            it.SeekToFirst();
            while (it.Valid())
            {
                result[Convert.ToHexString(it.Key())] = it.Value();
                it.Next();
            }
            return result;
        }

        private static void AssertCfIdentical(RocksDbManager a, RocksDbManager b, string cf)
        {
            var dumpA = DumpCf(a, cf);
            var dumpB = DumpCf(b, cf);
            Assert.True(dumpA.Count == dumpB.Count,
                $"[{cf}] row count differs: no-atomic-flush={dumpA.Count} atomic-flush={dumpB.Count}");
            foreach (var kv in dumpA)
            {
                Assert.True(dumpB.TryGetValue(kv.Key, out var bVal),
                    $"[{cf}] key {kv.Key} present without atomicFlush, missing with it");
                Assert.Equal(kv.Value, bVal);
            }
        }

        [Fact]
        public async Task AtomicFlushWired_ProducesByteIdenticalFlatCfsAndCursors_VsAtomicFlushNull_IncludingAMismatchBlock()
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            var dirA = SubDir("no-atomic-flush");
            var dirB = SubDir("atomic-flush");

            using var mgrA = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirA });
            using var mgrB = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirB });

            var bundleA = RocksDbChainStoreBundle.FromManager(mgrA, dirA, journalOptions: HistoricalStateOptions.Default, ownsManager: false);
            var bundleB = RocksDbChainStoreBundle.FromManager(mgrB, dirB, journalOptions: HistoricalStateOptions.Default, ownsManager: false);

            await HiveTestdataFixture.PopulateGenesisAsync(bundleA.State);
            await HiveTestdataFixture.PopulateGenesisAsync(bundleB.State);

            var importerA = BuildImporter(bundleA, atomicFlush: null);
            var importerB = BuildImporter(bundleB, atomicFlush: (IAtomicBlockFlush)bundleB);

            const ulong mismatchBlock = 6;
            const ulong lastBlock = 8;

            Assert.Contains(HiveTestdataFixture.Chain, b => (ulong)b.Header.BlockNumber == mismatchBlock);
            Assert.Contains(HiveTestdataFixture.Chain, b => (ulong)b.Header.BlockNumber == lastBlock);

            var provider = RlpBlockEncodingProvider.Instance;

            foreach (var b in HiveTestdataFixture.Chain)
            {
                var blockNumber = (ulong)b.Header.BlockNumber;
                if (blockNumber > lastBlock) break;

                if (blockNumber == mismatchBlock)
                {
                    var tampered = provider.DecodeBlockHeader(provider.EncodeBlockHeader(b.Header));
                    tampered.StateRoot = new byte[32];

                    var badA = await importerA.ImportAsync(tampered, b.Transactions, b.Uncles, b.Withdrawals, CancellationToken.None);
                    var badB = await importerB.ImportAsync(tampered, b.Transactions, b.Uncles, b.Withdrawals, CancellationToken.None);

                    Assert.True(badA.StateRootMismatch);
                    Assert.False(badA.RootMatches);
                    Assert.Null(badA.BlockHash);
                    Assert.True(badB.StateRootMismatch);
                    Assert.False(badB.RootMatches);
                    Assert.Null(badB.BlockHash);

                    Assert.Equal(bundleA.Metadata.GetLastBlock(), bundleB.Metadata.GetLastBlock());
                    AssertCfIdentical(mgrA, mgrB, RocksDbManager.CF_STATE_ACCOUNTS);
                    AssertCfIdentical(mgrA, mgrB, RocksDbManager.CF_STATE_STORAGE);
                }

                var resA = await importerA.ImportAsync(b.Header, b.Transactions, b.Uncles, b.Withdrawals, CancellationToken.None);
                var resB = await importerB.ImportAsync(b.Header, b.Transactions, b.Uncles, b.Withdrawals, CancellationToken.None);

                Assert.True(resA.RootMatches, $"block {blockNumber} (no atomicFlush) should match its header");
                Assert.True(resB.RootMatches, $"block {blockNumber} (atomicFlush) should match its header");
                Assert.Equal(resA.ComputedStateRoot, resB.ComputedStateRoot);

                bundleA.Metadata.Commit(blockNumber, resA.BlockHash);
                bundleA.Metadata.CommitDurableState(blockNumber, resA.BlockHash);
                bundleB.Metadata.Commit(blockNumber, resB.BlockHash);
                bundleB.Metadata.CommitDurableState(blockNumber, resB.BlockHash);
            }

            await ((IAtomicBlockFlush)bundleB).DrainAsync();

            AssertCfIdentical(mgrA, mgrB, RocksDbManager.CF_STATE_ACCOUNTS);
            AssertCfIdentical(mgrA, mgrB, RocksDbManager.CF_STATE_STORAGE);
            AssertCfIdentical(mgrA, mgrB, RocksDbManager.CF_STATE_CODE);
            AssertCfIdentical(mgrA, mgrB, RocksDbManager.CF_METADATA);

            Assert.True(DumpCf(mgrA, RocksDbManager.CF_STATE_ACCOUNTS).Count > 0);

            Assert.Equal(lastBlock, bundleA.Metadata.GetDurableStateBlock());
            Assert.Equal(lastBlock, bundleB.Metadata.GetDurableStateBlock());
            Assert.Equal(lastBlock, bundleA.Metadata.GetLastBlock());
            Assert.Equal(lastBlock, bundleB.Metadata.GetLastBlock());
        }
    }
}
