using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Snap
{
    public class PathKeyedSnapSinkWriteTests : IDisposable
    {
        private readonly string _pathDir;
        private readonly RocksDbManager _pathMgr;
        private readonly RocksDbChainStoreBundle _pathBundle;
        private readonly string _hashDir;
        private readonly RocksDbManager _hashMgr;
        private readonly RocksDbChainStoreBundle _hashBundle;

        public PathKeyedSnapSinkWriteTests()
        {
            _pathDir = NewDir("necc-snapsink-path");
            _hashDir = NewDir("necc-snapsink-hash");
            _pathMgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _pathDir, PathKeyedState = true });
            _hashMgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _hashDir, PathKeyedState = false });
            _pathBundle = RocksDbChainStoreBundle.FromManager(_pathMgr, _pathDir, HistoricalStateOptions.Default, ownsManager: false);
            _hashBundle = RocksDbChainStoreBundle.FromManager(_hashMgr, _hashDir, HistoricalStateOptions.Default, ownsManager: false);
        }

        [Fact]
        public async Task SnapSink_Over50kAccounts_PathStore_MatchesHashStoreRoot()
        {
            const int N = 60_000;
            var sha3 = new Sha3Keccack();
            var slim = SlimAccountEncoder.ToSlim(
                new AccountEncoder().Encode(new Account
                {
                    Nonce = 1,
                    Balance = 1000,
                    CodeHash = DefaultValues.EMPTY_DATA_HASH,
                    StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                }));

            var hashes = new List<byte[]>(N);
            for (int i = 0; i < N; i++)
                hashes.Add(sha3.CalculateHash(BitConverter.GetBytes(i)));
            hashes.Sort(CompareBytes);

            var pathRoot = await WriteAllAsync(_pathBundle, hashes, slim);
            var hashRoot = await WriteAllAsync(_hashBundle, hashes, slim);

            Assert.Equal(ToHex(hashRoot), ToHex(pathRoot));
        }

        private static async Task<byte[]> WriteAllAsync(RocksDbChainStoreBundle bundle, List<byte[]> hashes, byte[] slim)
        {
            var sink = new TrieSnapSyncSink(bundle.StateTrieNodes, bundle.State);
            await sink.BeginAsync(new byte[32], CancellationToken.None);
            foreach (var h in hashes)
                await sink.WriteAccountAsync(h, slim, CancellationToken.None);
            return await sink.FinaliseRootAsync(CancellationToken.None);
        }

        private static int CompareBytes(byte[] a, byte[] b)
        {
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++) { int c = a[i].CompareTo(b[i]); if (c != 0) return c; }
            return a.Length.CompareTo(b.Length);
        }

        private static string ToHex(byte[] b) => b == null ? "null" : BitConverter.ToString(b);

        private static string NewDir(string p)
        {
            var d = Path.Combine(Path.GetTempPath(), p + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }

        public void Dispose()
        {
            _pathBundle?.Dispose(); _hashBundle?.Dispose();
            _pathMgr?.Dispose(); _hashMgr?.Dispose();
            foreach (var d in new[] { _pathDir, _hashDir })
                try { if (Directory.Exists(d)) Directory.Delete(d, true); } catch { }
        }
    }
}
