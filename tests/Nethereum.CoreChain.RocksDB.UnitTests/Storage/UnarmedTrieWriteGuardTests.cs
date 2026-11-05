using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Storage
{
    public class UnarmedTrieWriteGuardTests : IDisposable
    {
        private readonly string _dir;

        public UnarmedTrieWriteGuardTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-unarmed-guard-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }

        private static RocksDbStorageOptions PathKeyedOptions(string dataDir) => new RocksDbStorageOptions
        {
            DatabasePath = dataDir,
            PathKeyedState = true,
            TrieNodeHistoryBlocks = 100_000,
            TrieNodeHistoryIndex = true,
        };

        private sealed class HoldableRocksDbManager : RocksDbManager
        {
            public HoldableRocksDbManager(RocksDbStorageOptions options) : base(options) { }
            public Action OnBeforeWrite;

            public override void Write(RocksDbSharp.WriteBatch batch, RocksDbSharp.WriteOptions writeOptions = null)
            {
                OnBeforeWrite?.Invoke();
                base.Write(batch, writeOptions);
            }
        }

        private static byte[] Hash32(byte b) { var h = new byte[32]; for (int i = 0; i < 32; i++) h[i] = (byte)(b + i); return h; }

        private static TrieNodeSet OneNode(byte tag)
        {
            var set = new TrieNodeSet();
            var val = new byte[32];
            for (int i = 0; i < val.Length; i++) val[i] = tag;
            set.Add(new LeafNode { Owner = Array.Empty<byte>(), Path = new byte[] { tag }, Nibbles = new byte[] { 1, 2 }, Value = val });
            return set;
        }

        private static void CommitArmed(RocksDbChainStoreBundle bundle, ulong block, byte tag)
        {
            bundle.NodeCommitBlockSource.Arm(block);
            bundle.StateTrieNodes.Commit(OneNode(tag));
            bundle.NodeCommitBlockSource.Clear();
        }

        private async Task<(RocksDbChainStoreBundle bundle, ManualResetEventSlim release, Task flush)> HoldAFlushInFlight(HoldableRocksDbManager mgr)
        {
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, _dir, ownsManager: false);
            var entered = new ManualResetEventSlim(false);
            var release = new ManualResetEventSlim(false);
            mgr.OnBeforeWrite = () => { entered.Set(); release.Wait(TimeSpan.FromSeconds(30)); };

            CommitArmed(bundle, 1, 0x11);
            var flush = ((IAtomicBlockFlush)bundle).FlushBlockAsync(flat: null, block: 1, Hash32(1));
            await flush.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)), "the flush should be holding its write");
            return (bundle, release, flush);
        }

        [Fact]
        public async Task Given_ABackgroundFlushInFlight_When_AnUnarmedTrieCommitArrives_Then_ItThrows()
        {
            using var mgr = new HoldableRocksDbManager(PathKeyedOptions(_dir));
            var (bundle, release, _) = await HoldAFlushInFlight(mgr);
            try
            {
                var ex = Assert.Throws<InvalidOperationException>(() => bundle.StateTrieNodes.Commit(OneNode(0x33)));
                Assert.Contains("Commit", ex.Message);
                Assert.Contains("background flush is in flight", ex.Message);
            }
            finally
            {
                release.Set();
                await ((IAtomicBlockFlush)bundle).DrainAsync().WaitAsync(TimeSpan.FromSeconds(30));
            }
        }

        [Fact]
        public async Task Given_ABackgroundFlushInFlight_When_AnUnarmedTrieDeleteRangeArrives_Then_ItThrows()
        {
            using var mgr = new HoldableRocksDbManager(PathKeyedOptions(_dir));
            var (bundle, release, _) = await HoldAFlushInFlight(mgr);
            try
            {
                var ex = Assert.Throws<InvalidOperationException>(() => ((IContractStorageWipeable)bundle.StateTrieNodes).DeleteRange(new byte[] { 0x44 }));
                Assert.Contains("DeleteRange", ex.Message);
                Assert.Contains("background flush is in flight", ex.Message);
            }
            finally
            {
                release.Set();
                await ((IAtomicBlockFlush)bundle).DrainAsync().WaitAsync(TimeSpan.FromSeconds(30));
            }
        }

        [Fact]
        public async Task Given_NoFlushInFlight_When_AnUnarmedTrieCommitArrives_Then_ItReachesTheStoreAsToday()
        {
            using var mgr = new HoldableRocksDbManager(PathKeyedOptions(_dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, _dir, ownsManager: false);

            CommitArmed(bundle, 1, 0x11);
            var flush = (IAtomicBlockFlush)bundle;
            await flush.FlushBlockAsync(flat: null, block: 1, Hash32(1)).WaitAsync(TimeSpan.FromSeconds(30));
            await flush.DrainAsync().WaitAsync(TimeSpan.FromSeconds(30));

            bundle.StateTrieNodes.Commit(OneNode(0x33));
            ((IContractStorageWipeable)bundle.StateTrieNodes).DeleteRange(new byte[] { 0x44 });

            Assert.NotNull(new RocksDbPathTrieNodeStore(mgr).TryGetRawNode(Array.Empty<byte>(), new byte[] { 0x33 }));
        }

        [Fact]
        public async Task Given_ABackgroundFlushInFlight_When_AnArmedCommitArrives_Then_ItIsCaptured()
        {
            using var mgr = new HoldableRocksDbManager(PathKeyedOptions(_dir));
            var (bundle, release, _) = await HoldAFlushInFlight(mgr);
            try
            {
                CommitArmed(bundle, 2, 0x22);

                var reader = (IRawNodeReader)bundle.StateTrieNodes;
                Assert.NotNull(reader.TryGetRawNode(Array.Empty<byte>(), new byte[] { 0x22 }));
                Assert.Null(new RocksDbPathTrieNodeStore(mgr).TryGetRawNode(Array.Empty<byte>(), new byte[] { 0x22 }));
            }
            finally
            {
                release.Set();
                await ((IAtomicBlockFlush)bundle).DrainAsync().WaitAsync(TimeSpan.FromSeconds(30));
            }
        }
    }
}
