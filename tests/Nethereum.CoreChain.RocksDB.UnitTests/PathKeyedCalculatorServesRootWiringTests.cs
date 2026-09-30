using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class PathKeyedCalculatorServesRootWiringTests : IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbManager _mgr;
        private readonly RocksDbChainStoreBundle _bundle;

        public PathKeyedCalculatorServesRootWiringTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-pk-serve-wiring-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions
            {
                DatabasePath = _dir, PathKeyedState = true, TrieNodeHistoryBlocks = 128, TrieNodeHistoryIndex = true,
            });
            _bundle = RocksDbChainStoreBundle.FromManager(_mgr, _dir, HistoricalStateOptions.Default, ownsManager: false);
        }

        [Fact]
        public async Task CalcWiredToStateTrieNodes_MakesLatestProofStoreServeRoot()
        {
            var calc = new IncrementalStateRootCalculator(
                _bundle.State, _bundle.StateTrieNodes,
                emitTombstones: !ReferenceEquals(_bundle.StateTrieNodes, _bundle.TrieNodes));

            for (int i = 0; i < 20; i++)
                await _bundle.State.SaveAccountAsync(Addr(i), new Account { Balance = 1000 + i, Nonce = 1 });
            var root = await calc.ComputeStateRootAsync();
            _bundle.StateTrieNodes.Flush();

            var latest = ((ILatestProofServingBundle)_bundle).LatestProofNodeStore;
            Assert.True(latest.ContainsKey(root),
                "path-keyed calculator wired to StateTrieNodes must make the LATEST proof store serve the tip root");
        }

        [Fact]
        public async Task CalcWiredToHashTrieNodes_LeavesPathProofStoreUnableToServeRoot_BugSignature()
        {
            var calc = new IncrementalStateRootCalculator(_bundle.State, _bundle.TrieNodes);

            for (int i = 0; i < 20; i++)
                await _bundle.State.SaveAccountAsync(Addr(i), new Account { Balance = 1000 + i, Nonce = 1 });
            var root = await calc.ComputeStateRootAsync();
            _bundle.TrieNodes.Flush();

            var latest = ((ILatestProofServingBundle)_bundle).LatestProofNodeStore;
            Assert.False(latest.ContainsKey(root),
                "hash-wired calculator leaves the path CF empty -> the LATEST proof store must NOT serve the root");
            Assert.True(_bundle.TrieNodes.ContainsKey(root),
                "the hash store DOES hold the (correct) root -> the state is valid, just stored hash-keyed");
        }

        private static string Addr(int i) => "0x" + i.ToString("x").PadLeft(40, '0');

        public void Dispose()
        {
            _bundle?.Dispose();
            _mgr?.Dispose();
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { /* best-effort */ }
        }
    }
}
