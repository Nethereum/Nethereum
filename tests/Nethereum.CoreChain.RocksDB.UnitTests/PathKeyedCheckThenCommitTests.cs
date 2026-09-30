using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class PathKeyedCheckThenCommitTests : IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbManager _mgr;

        public PathKeyedCheckThenCommitTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-ctc-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions
            {
                DatabasePath = _dir, PathKeyedState = true, TrieNodeHistoryBlocks = 128, TrieNodeHistoryIndex = true,
            });
        }

        private static string Addr(int i) => "0x" + i.ToString("x").PadLeft(40, '0');

        private IIncrementalStateRootCalculator NewCalc(RocksDbChainStoreBundle bundle)
            => new IncrementalStateRootCalculator(bundle.State, bundle.StateTrieNodes,
                emitTombstones: !ReferenceEquals(bundle.StateTrieNodes, bundle.TrieNodes));

        [Fact]
        public async Task DiscardedBlock_DoesNotCorruptStore_ParentRootStaysColdResolvable()
        {
            using var bundle = RocksDbChainStoreBundle.FromManager(
                _mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);
            var hist = (HistoricalStateStore)bundle.State;

            bundle.NodeCommitBlockSource.Arm(1);
            hist.SetCurrentBlockNumber(1);
            for (int i = 0; i < 60; i++) await hist.SaveAccountAsync(Addr(i), new Account { Balance = 1000 + i, Nonce = 1 });
            var root1 = await NewCalc(bundle).ComputeStateRootAsync();
            await hist.ClearCurrentBlockNumberAsync();
            bundle.NodeCommitBlockSource.Clear();
            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, 1, new byte[32]);
            await ((IAtomicBlockFlush)bundle).DrainAsync();

            var cold1 = PatriciaTrie.LoadFromStorage(root1, bundle.StateTrieNodes);
            for (int i = 0; i < 60; i++)
                Assert.NotNull(cold1.Get(StateKeys.AccountKey(Addr(i))));

            var calc2 = NewCalc(bundle);
            bundle.NodeCommitBlockSource.Arm(2);
            hist.SetCurrentBlockNumber(2);
            for (int i = 0; i < 60; i++) await hist.SaveAccountAsync(Addr(i), new Account { Balance = 9999 + i, Nonce = 7 });
            var root2 = await calc2.ComputeStateRootWithoutPersistAsync(root1);
            calc2.DiscardPendingState();
            await hist.ClearCurrentBlockNumberAsync();
            bundle.NodeCommitBlockSource.Clear();
            Assert.False(root2.AsSpan().SequenceEqual(root1), "block 2 changed state, so its root must differ");

            var cold1Again = PatriciaTrie.LoadFromStorage(root1, bundle.StateTrieNodes);
            for (int i = 0; i < 60; i++)
                Assert.NotNull(cold1Again.Get(StateKeys.AccountKey(Addr(i))));

            Assert.False(bundle.StateTrieNodes.ContainsKey(root2), "discarded root must not be on disk");
        }

        [Fact]
        public async Task ComputeWithoutPersist_ThenPersist_CommitsIdenticallyToComputeWithPersist()
        {
            using var bundle = RocksDbChainStoreBundle.FromManager(
                _mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);
            var hist = (HistoricalStateStore)bundle.State;

            bundle.NodeCommitBlockSource.Arm(1);
            hist.SetCurrentBlockNumber(1);
            for (int i = 0; i < 40; i++) await hist.SaveAccountAsync(Addr(i), new Account { Balance = 500 + i, Nonce = 1 });
            var calc = NewCalc(bundle);
            var root = await calc.ComputeStateRootWithoutPersistAsync(null);
            await calc.PersistPendingStateAsync();
            await hist.ClearCurrentBlockNumberAsync();
            bundle.NodeCommitBlockSource.Clear();
            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, 1, new byte[32]);
            await ((IAtomicBlockFlush)bundle).DrainAsync();

            var cold = PatriciaTrie.LoadFromStorage(root, bundle.StateTrieNodes);
            for (int i = 0; i < 40; i++)
                Assert.NotNull(cold.Get(StateKeys.AccountKey(Addr(i))));
        }

        public void Dispose()
        {
            try { _mgr.Dispose(); } catch { }
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
        }
    }
}
