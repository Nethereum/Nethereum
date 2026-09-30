using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class PathKeyedFollowNoOrphanTests : IDisposable
    {
        private readonly string _pathDir;
        private readonly string _hashDir;
        private readonly string _oracleDir;
        private readonly RocksDbManager _pathMgr;
        private readonly RocksDbManager _hashMgr;
        private readonly RocksDbManager _oracleMgr;
        private readonly RocksDbChainStoreBundle _pathBundle;
        private readonly RocksDbChainStoreBundle _hashBundle;
        private readonly RocksDbStateStore _oracleState;
        private readonly RocksDbPathTrieNodeStore _oracleTrie;

        public PathKeyedFollowNoOrphanTests()
        {
            _pathDir = NewDir("necc-follow-path");
            _hashDir = NewDir("necc-follow-hash");
            _oracleDir = NewDir("necc-follow-oracle");

            _pathMgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _pathDir, PathKeyedState = true });
            _hashMgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _hashDir, PathKeyedState = false });
            _oracleMgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _oracleDir, PathKeyedState = true });

            _pathBundle = RocksDbChainStoreBundle.FromManager(_pathMgr, _pathDir, HistoricalStateOptions.Default, ownsManager: false);
            _hashBundle = RocksDbChainStoreBundle.FromManager(_hashMgr, _hashDir, HistoricalStateOptions.Default, ownsManager: false);

            _oracleState = new RocksDbStateStore(_oracleMgr);
            _oracleTrie = new RocksDbPathTrieNodeStore(_oracleMgr);
        }

        [Fact]
        public async Task PathKeyedFollow_InsertOverwriteDelete_LeavesNoOrphans_AndMatchesHashRoots()
        {
            var pathCalc = new IncrementalStateRootCalculator(
                _pathBundle.State, _pathBundle.StateTrieNodes,
                emitTombstones: !ReferenceEquals(_pathBundle.StateTrieNodes, _pathBundle.TrieNodes));
            var hashCalc = new IncrementalStateRootCalculator(
                _hashBundle.State, _hashBundle.StateTrieNodes,
                emitTombstones: !ReferenceEquals(_hashBundle.StateTrieNodes, _hashBundle.TrieNodes));

            Assert.True(!ReferenceEquals(_pathBundle.StateTrieNodes, _pathBundle.TrieNodes));
            Assert.False(!ReferenceEquals(_hashBundle.StateTrieNodes, _hashBundle.TrieNodes));

            const string CA = "0x00000000000000000000000000000000000000ca";
            const string CB = "0x00000000000000000000000000000000000000cb";

            await ApplyToAll(async s =>
            {
                for (int i = 0; i < 40; i++)
                    await s.SaveAccountAsync(Addr(i), new Account { Balance = 1000 + i, Nonce = 1 });
                await s.SaveAccountAsync(CA, new Account { Balance = 0, Nonce = 1 });
                await s.SaveAccountAsync(CB, new Account { Balance = 0, Nonce = 1 });
                for (int i = 0; i < 24; i++)
                {
                    await s.SaveStorageAsync(CA, i, ValueBytes(i + 1));
                    await s.SaveStorageAsync(CB, i, ValueBytes(i + 100));
                }
            });
            await AssertBlockInvariants(pathCalc, hashCalc);

            await ApplyToAll(async s =>
            {
                for (int i = 0; i < 40; i += 2)
                    await s.SaveAccountAsync(Addr(i), new Account { Balance = 5000 + i, Nonce = 2 });
                for (int i = 24; i < 40; i++)
                    await s.SaveStorageAsync(CA, i, ValueBytes(i + 1));
            });
            await AssertBlockInvariants(pathCalc, hashCalc);

            await ApplyToAll(async s =>
            {
                for (int i = 0; i < 40; i += 3)
                    await s.DeleteAccountAsync(Addr(i));
                for (int i = 0; i < 20; i++)
                    await s.SaveStorageAsync(CA, i, Array.Empty<byte>());
                for (int i = 0; i < 12; i++)
                    await s.SaveStorageAsync(CB, i, Array.Empty<byte>());
            });
            await AssertBlockInvariants(pathCalc, hashCalc);

            await ApplyToAll(async s =>
            {
                for (int i = 0; i < 40; i += 3)
                    await s.SaveAccountAsync(Addr(i), new Account { Balance = 9000 + i, Nonce = 5 });
                for (int i = 1; i < 40; i += 5)
                    await s.DeleteAccountAsync(Addr(i));
            });
            await AssertBlockInvariants(pathCalc, hashCalc);
        }

        [Fact]
        public async Task PathKeyedFollow_SelfDestruct_WipesContractStorageSubtree_NoOrphans_AndMatchesHashRoots()
        {
            var pathCalc = new IncrementalStateRootCalculator(
                _pathBundle.State, _pathBundle.StateTrieNodes,
                emitTombstones: !ReferenceEquals(_pathBundle.StateTrieNodes, _pathBundle.TrieNodes));
            var hashCalc = new IncrementalStateRootCalculator(
                _hashBundle.State, _hashBundle.StateTrieNodes,
                emitTombstones: !ReferenceEquals(_hashBundle.StateTrieNodes, _hashBundle.TrieNodes));

            const string CA = "0x00000000000000000000000000000000000000ca";
            var owner = Owner(CA);

            await ApplyToAll(async s =>
            {
                for (int i = 0; i < 30; i++)
                    await s.SaveAccountAsync(Addr(i), new Account { Balance = 100 + i, Nonce = 1 });
                await s.SaveAccountAsync(CA, new Account { Balance = 0, Nonce = 1 });
                for (int i = 0; i < 40; i++)
                    await s.SaveStorageAsync(CA, i, ValueBytes(i + 1));
            });
            await AssertBlockInvariants(pathCalc, hashCalc);
            Assert.True(CountOwnerKeys(_pathMgr, owner) > 0, "CA storage subtree must be present after block 1");

            await ApplyToAll(async s =>
            {
                await s.ClearStorageAsync(CA);
                await s.DeleteAccountAsync(CA);
            });
            await AssertBlockInvariants(pathCalc, hashCalc);

            Assert.Equal(0, CountOwnerKeys(_pathMgr, owner));
        }

        private static byte[] Owner(string address)
            => new Sha3Keccack().CalculateHash(AddressUtil.Current.ConvertToValid20ByteAddress(address).HexToByteArray());

        private static int CountOwnerKeys(RocksDbManager mgr, byte[] owner)
        {
            using var it = mgr.CreateIterator(RocksDbManager.CF_STATE_TRIE_STORAGE);
            it.Seek(owner);
            int n = 0;
            while (it.Valid())
            {
                if (!ByteUtil.StartsWith(it.Key(), owner)) break;
                n++;
                it.Next();
            }
            return n;
        }

        private async Task ApplyToAll(Func<IStateStore, Task> ops)
        {
            await ops(_pathBundle.State);
            await ops(_hashBundle.State);
            await ops(_oracleState);
        }

        private async Task AssertBlockInvariants(
            IIncrementalStateRootCalculator pathCalc, IIncrementalStateRootCalculator hashCalc)
        {
            var rootPath = await pathCalc.ComputeStateRootAsync();
            var rootHash = await hashCalc.ComputeStateRootAsync();

            Assert.Equal(rootHash, rootPath);

            var kept = CountKeys(_pathMgr, RocksDbManager.CF_STATE_TRIE_ACCOUNT)
                     + CountKeys(_pathMgr, RocksDbManager.CF_STATE_TRIE_STORAGE);
            var reachable = await ReachableKeyedCountAsync(rootPath);
            Assert.Equal(reachable, kept);
        }

        private async Task<int> ReachableKeyedCountAsync(byte[] expectedRoot)
        {
            _oracleTrie.Clear();
            var rebuild = new IncrementalStateRootCalculator(_oracleState, _oracleTrie);
            var oracleRoot = await rebuild.ComputeFullStateRootAsync();
            _oracleTrie.Flush();
            Assert.Equal(expectedRoot, oracleRoot);
            return CountKeys(_oracleMgr, RocksDbManager.CF_STATE_TRIE_ACCOUNT)
                 + CountKeys(_oracleMgr, RocksDbManager.CF_STATE_TRIE_STORAGE);
        }

        private static int CountKeys(RocksDbManager mgr, string cf)
        {
            using var it = mgr.CreateIterator(cf);
            it.SeekToFirst();
            int n = 0;
            while (it.Valid()) { n++; it.Next(); }
            return n;
        }

        private static string Addr(int i) => "0x" + i.ToString("x").PadLeft(40, '0');

        private static byte[] ValueBytes(int v)
        {
            var b = new BigInteger(v).ToByteArray(isUnsigned: true, isBigEndian: true);
            return b.Length == 0 ? new byte[] { 0 } : b;
        }

        private static string NewDir(string prefix)
        {
            var d = Path.Combine(Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }

        public void Dispose()
        {
            _pathBundle?.Dispose();
            _hashBundle?.Dispose();
            _pathMgr?.Dispose();
            _hashMgr?.Dispose();
            _oracleMgr?.Dispose();
            foreach (var d in new[] { _pathDir, _hashDir, _oracleDir })
            {
                try { if (Directory.Exists(d)) Directory.Delete(d, recursive: true); }
                catch { /* best-effort */ }
            }
        }
    }
}
