using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Storage
{
    public class AbandonedBlockDiscardTests : IDisposable
    {
        private readonly string _dir;
        private const string AddrParent = "0x1111111111111111111111111111111111111111";
        private const string AddrAbandoned = "0x2222222222222222222222222222222222222222";
        private const string AddrNext = "0x3333333333333333333333333333333333333333";

        public AbandonedBlockDiscardTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-abandon-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }

        private static RocksDbManager NewManager(string path)
        {
            Directory.CreateDirectory(path);
            return new RocksDbManager(new RocksDbStorageOptions
            {
                DatabasePath = path,
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 0,
                TrieNodeHistoryIndex = false,
            });
        }

        private static IIncrementalStateRootCalculator NewCalc(RocksDbChainStoreBundle bundle)
            => new IncrementalStateRootCalculator(bundle.State, bundle.StateTrieNodes,
                emitTombstones: !ReferenceEquals(bundle.StateTrieNodes, bundle.TrieNodes));

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

        private static void AssertCfIdentical(RocksDbManager faulted, RocksDbManager cleanReference, string cf)
        {
            var dumpFaulted = DumpCf(faulted, cf);
            var dumpClean = DumpCf(cleanReference, cf);
            Assert.True(dumpFaulted.Count == dumpClean.Count,
                $"[{cf}] row count differs: faulted={dumpFaulted.Count} clean-reference={dumpClean.Count}");
            foreach (var kv in dumpFaulted)
            {
                Assert.True(dumpClean.TryGetValue(kv.Key, out var cleanVal),
                    $"[{cf}] key {kv.Key} present in the faulted-and-continued run, missing in the clean reference");
                Assert.Equal(kv.Value, cleanVal);
            }
        }

        private static async Task RunCleanReferenceAsync(RocksDbChainStoreBundle bundle)
        {
            var hist = (HistoricalStateStore)bundle.State;
            var flush = (IAtomicBlockFlush)bundle;

            bundle.NodeCommitBlockSource.Arm(1);
            hist.SetCurrentBlockNumber(1);
            await hist.SaveAccountAsync(AddrParent, new Account { Balance = 1000, Nonce = 1 });
            var root1 = await NewCalc(bundle).ComputeStateRootAsync();
            await hist.ClearCurrentBlockNumberAsync();
            bundle.NodeCommitBlockSource.Clear();
            await flush.FlushBlockAsync(null, 1, new byte[32]);
            await flush.DrainAsync();

            bundle.NodeCommitBlockSource.Arm(3);
            hist.SetCurrentBlockNumber(3);
            await hist.SaveAccountAsync(AddrNext, new Account { Balance = 3000, Nonce = 1 });
            var calc3 = NewCalc(bundle);
            await calc3.ComputeStateRootWithoutPersistAsync(root1);
            await calc3.PersistPendingStateAsync();
            await hist.ClearCurrentBlockNumberAsync();
            bundle.NodeCommitBlockSource.Clear();
            await flush.FlushBlockAsync(null, 3, new byte[32]);
            await flush.DrainAsync();
        }

        private static async Task RunFaultedAndContinuedAsync(RocksDbChainStoreBundle bundle, bool applyDiscardFix)
        {
            var hist = (HistoricalStateStore)bundle.State;
            var flush = (IAtomicBlockFlush)bundle;

            bundle.NodeCommitBlockSource.Arm(1);
            hist.SetCurrentBlockNumber(1);
            await hist.SaveAccountAsync(AddrParent, new Account { Balance = 1000, Nonce = 1 });
            var root1 = await NewCalc(bundle).ComputeStateRootAsync();
            await hist.ClearCurrentBlockNumberAsync();
            bundle.NodeCommitBlockSource.Clear();
            await flush.FlushBlockAsync(null, 1, new byte[32]);
            await flush.DrainAsync();

            bundle.NodeCommitBlockSource.Arm(2);
            hist.SetCurrentBlockNumber(2);
            await hist.SaveAccountAsync(AddrAbandoned, new Account { Balance = 2000, Nonce = 1 });
            var calcN = NewCalc(bundle);
            await calcN.ComputeStateRootWithoutPersistAsync(root1);
            await calcN.PersistPendingStateAsync();
            if (applyDiscardFix) flush.DiscardCapturedBlock();
            await hist.RevertCurrentBlockAsync();
            bundle.NodeCommitBlockSource.Clear();

            bundle.NodeCommitBlockSource.Arm(3);
            hist.SetCurrentBlockNumber(3);
            await hist.SaveAccountAsync(AddrNext, new Account { Balance = 3000, Nonce = 1 });
            var calc3 = NewCalc(bundle);
            await calc3.ComputeStateRootWithoutPersistAsync(root1);
            await calc3.PersistPendingStateAsync();
            await hist.ClearCurrentBlockNumberAsync();
            bundle.NodeCommitBlockSource.Clear();
            await flush.FlushBlockAsync(null, 3, new byte[32]);
            await flush.DrainAsync();
        }

        [Fact]
        public async Task WithoutDiscardFix_AbandonedBlockResidueCorruptsTheNextBlocksRead()
        {
            var dirFaulted = Path.Combine(_dir, "faulted-nofix");
            Directory.CreateDirectory(dirFaulted);
            using var mgrFaulted = NewManager(dirFaulted);
            using var bundleFaulted = RocksDbChainStoreBundle.FromManager(mgrFaulted, dirFaulted, HistoricalStateOptions.FullArchive, ownsManager: false);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => RunFaultedAndContinuedAsync(bundleFaulted, applyDiscardFix: false));
            Assert.Contains("hash mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task WithDiscardFix_AbandonedBlockLeavesNoResidue_ByteIdenticalToCleanReference()
        {
            var dirFaulted = Path.Combine(_dir, "faulted-fixed");
            var dirClean = Path.Combine(_dir, "clean-fixed-compare");

            using var mgrFaulted = NewManager(dirFaulted);
            using var mgrClean = NewManager(dirClean);
            using var bundleFaulted = RocksDbChainStoreBundle.FromManager(mgrFaulted, dirFaulted, HistoricalStateOptions.FullArchive, ownsManager: false);
            using var bundleClean = RocksDbChainStoreBundle.FromManager(mgrClean, dirClean, HistoricalStateOptions.FullArchive, ownsManager: false);

            await RunFaultedAndContinuedAsync(bundleFaulted, applyDiscardFix: true);
            await RunCleanReferenceAsync(bundleClean);

            AssertCfIdentical(mgrFaulted, mgrClean, RocksDbManager.CF_NODE_HISTORY);
            AssertCfIdentical(mgrFaulted, mgrClean, RocksDbManager.CF_STATE_TRIE_ACCOUNT);
            AssertCfIdentical(mgrFaulted, mgrClean, RocksDbManager.CF_STATE_ACCOUNTS);

            Assert.True(DumpCf(mgrFaulted, RocksDbManager.CF_NODE_HISTORY).Count > 0);
            Assert.True(DumpCf(mgrFaulted, RocksDbManager.CF_STATE_TRIE_ACCOUNT).Count > 0);
        }
    }
}
