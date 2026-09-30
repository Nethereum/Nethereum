using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class StateRootIndexOffByOneReproTests : IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbManager _mgr;
        private readonly RocksDbChainStoreBundle _bundle;

        public StateRootIndexOffByOneReproTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-rootidx-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions
            {
                DatabasePath = _dir,
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 64,
                TrieNodeHistoryIndex = true,
            });
            _bundle = RocksDbChainStoreBundle.FromManager(_mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);
        }

        [Fact]
        public async Task FindBlockByStateRoot_MapsEachBlockRootToItsOwnBlock()
        {
            var seed = new IncrementalStateRootCalculator(_bundle.State, _bundle.StateTrieNodes, emitTombstones: false);
            var genesisRoot = await seed.ComputeStateRootAsync();
            var roots = new List<byte[]> { genesisRoot };
            var prev = genesisRoot;

            for (ulong n = 1; n <= 6; n++)
            {
                var calc = new IncrementalStateRootCalculator(
                    _bundle.State, _bundle.StateTrieNodes,
                    emitTombstones: !ReferenceEquals(_bundle.StateTrieNodes, _bundle.TrieNodes));

                _bundle.NodeCommitBlockSource.Arm(n);

                await calc.ComputeStateRootAsync(prev);

                await _bundle.State.SaveAccountAsync(Addr((int)n), new Account { Balance = 1000 + n, Nonce = n });
                await _bundle.State.SaveAccountAsync(Addr(999), new Account { Balance = 5000 + n, Nonce = n });
                await _bundle.State.SaveAccountAsync(CA, new Account { Balance = 0, Nonce = 1 });
                await _bundle.State.SaveStorageAsync(CA, (int)n, ValueBytes((int)n + 1));

                var root = await calc.ComputeStateRootAsync();
                _bundle.NodeCommitBlockSource.Clear();
                await ((IAtomicBlockFlush)_bundle).FlushBlockAsync(null, n, new byte[32]);
                await ((IAtomicBlockFlush)_bundle).DrainAsync();
                roots.Add(root);
                prev = root;
            }

            var index = new RocksDbNodeReverseDiffStore(_mgr, buildKeyMajorIndex: true);

            for (ulong n = 1; n <= 6; n++)
            {
                var got = index.FindBlockByStateRoot(roots[(int)n]);
                Assert.Equal(n, got);
            }
        }

        private const string CA = "0x00000000000000000000000000000000000000ca";

        private static string Addr(int i) => "0x" + i.ToString("x").PadLeft(40, '0');

        private static byte[] ValueBytes(int v)
        {
            var b = new System.Numerics.BigInteger(v).ToByteArray(isUnsigned: true, isBigEndian: true);
            return b.Length == 0 ? new byte[] { 0 } : b;
        }

        public void Dispose()
        {
            _bundle?.Dispose();
            _mgr?.Dispose();
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }
    }
}
