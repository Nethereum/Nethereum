using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class PathKeyedColdCacheResumeTests : IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbManager _mgr;
        private readonly RocksDbChainStoreBundle _bundle;

        private const string C = "0x00000000000000000000000000000000000000ca";

        public PathKeyedColdCacheResumeTests()
        {
            _dir = NewDir("necc-coldcache-path");
            _mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir, PathKeyedState = true });
            _bundle = RocksDbChainStoreBundle.FromManager(_mgr, _dir, HistoricalStateOptions.Default, ownsManager: false);
        }

        [Theory]
        [InlineData("modify")]
        [InlineData("add")]
        [InlineData("delete")]
        public async Task ColdCacheResume_IncrementalStorageMutation_MatchesFullRebuild(string op)
        {
            IncrementalStateRootCalculator Warm(RocksDbChainStoreBundle b)
                => new IncrementalStateRootCalculator(b.State, b.StateTrieNodes,
                    emitTombstones: !ReferenceEquals(b.StateTrieNodes, b.TrieNodes));

            for (int i = 1; i <= 6; i++)
                await _bundle.State.SaveStorageAsync(C, i, new byte[] { (byte)(0x10 + i) });
            await _bundle.State.SaveAccountAsync(C, new Account { Nonce = 1, Balance = 0, CodeHash = new byte[] { 9 } });

            var calc1 = Warm(_bundle);
            var r1 = await calc1.ComputeStateRootAsync();
            _bundle.StateTrieNodes.Flush();

            var calc2 = Warm(_bundle);
            Assert.Equal(r1, await calc2.ComputeStateRootAsync(r1));

            switch (op)
            {
                case "modify": await _bundle.State.SaveStorageAsync(C, 3, new byte[] { 0x99 }); break;
                case "add": await _bundle.State.SaveStorageAsync(C, 42, new byte[] { 0x99 }); break;
                case "delete": await _bundle.State.SaveStorageAsync(C, 3, new byte[0]); break;
            }
            var rCold = await calc2.ComputeStateRootAsync();

            var rFull = await new IncrementalStateRootCalculator(_bundle.State, new InMemoryContentNodeStore())
                .ComputeFullStateRootAsync();

            Assert.Equal(ToHex(rFull), ToHex(rCold));
        }

        [Theory]
        [InlineData("modify")]
        [InlineData("add")]
        [InlineData("delete")]
        public async Task ColdCacheResume_LargeStorageTrie_IncrementalMutation_MatchesFullRebuild(string op)
        {
            IncrementalStateRootCalculator Warm(RocksDbChainStoreBundle b)
                => new IncrementalStateRootCalculator(b.State, b.StateTrieNodes,
                    emitTombstones: !ReferenceEquals(b.StateTrieNodes, b.TrieNodes));

            for (int i = 1; i <= 128; i++)
                await _bundle.State.SaveStorageAsync(C, i, new byte[] { (byte)(i & 0xff), (byte)((i * 7) & 0xff), 0x01 });
            await _bundle.State.SaveAccountAsync(C, new Account { Nonce = 1, Balance = 0, CodeHash = new byte[] { 9 } });

            var r1 = await Warm(_bundle).ComputeStateRootAsync();
            _bundle.StateTrieNodes.Flush();

            var calc2 = Warm(_bundle);
            Assert.Equal(r1, await calc2.ComputeStateRootAsync(r1));

            switch (op)
            {
                case "modify": await _bundle.State.SaveStorageAsync(C, 77, new byte[] { 0xab, 0xcd }); break;
                case "add": await _bundle.State.SaveStorageAsync(C, 500, new byte[] { 0xab, 0xcd }); break;
                case "delete": await _bundle.State.SaveStorageAsync(C, 77, new byte[0]); break;
            }
            var rCold = await calc2.ComputeStateRootAsync();

            var rFull = await new IncrementalStateRootCalculator(_bundle.State, new InMemoryContentNodeStore())
                .ComputeFullStateRootAsync();

            Assert.Equal(ToHex(rFull), ToHex(rCold));
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
            _bundle?.Dispose();
            _mgr?.Dispose();
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
        }
    }
}
