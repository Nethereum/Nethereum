using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Storage
{
    public class AsyncFlushRewindRecoveryRaceTests : IDisposable
    {
        private readonly string _dir;

        public AsyncFlushRewindRecoveryRaceTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-asyncflush-race-" + Guid.NewGuid().ToString("N"));
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

        private static byte[] Hash32(byte b) { var h = new byte[32]; for (int i = 0; i < 32; i++) h[i] = (byte)(b + i); return h; }

        private sealed class FaultInjectableRocksDbManager : RocksDbManager
        {
            public FaultInjectableRocksDbManager(RocksDbStorageOptions options) : base(options) { }
            public Action OnBeforeWrite;

            public override void Write(RocksDbSharp.WriteBatch batch, RocksDbSharp.WriteOptions writeOptions = null)
            {
                OnBeforeWrite?.Invoke();
                base.Write(batch, writeOptions);
            }
        }

        private static void CommitOneNode(RocksDbChainStoreBundle bundle, ulong block, byte tag)
        {
            bundle.NodeCommitBlockSource.Arm(block);
            var set = new TrieNodeSet();
            var val = new byte[32];
            for (int i = 0; i < val.Length; i++) val[i] = tag;
            set.Add(new LeafNode { Owner = Array.Empty<byte>(), Path = new byte[] { tag }, Nibbles = new byte[] { 1, 2 }, Value = val });
            bundle.StateTrieNodes.Commit(set);
            bundle.NodeCommitBlockSource.Clear();
        }

        [Fact]
        public async Task RewindToAsync_DrainsBeforeReadingCursor_NeverObservesStalePreFlushHead()
        {
            using var mgr = new FaultInjectableRocksDbManager(PathKeyedOptions(_dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, _dir, ownsManager: false);
            var flush = (IAtomicBlockFlush)bundle;

            var writeEntered = new ManualResetEventSlim(false);
            var releaseWrite = new ManualResetEventSlim(false);
            mgr.OnBeforeWrite = () => { writeEntered.Set(); releaseWrite.Wait(TimeSpan.FromSeconds(10)); };

            CommitOneNode(bundle, 1, 0x11);
            var flushTask = flush.FlushBlockAsync(flat: null, block: 1, Hash32(1));
            var handedOffInTime = await Task.WhenAny(flushTask, Task.Delay(TimeSpan.FromSeconds(5))) == flushTask;
            Assert.True(handedOffInTime, "FlushBlockAsync should return once staged/handed-off");
            Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(5)), "expected the flush task to have entered Write by now");

            Assert.Equal(0UL, bundle.Metadata.GetDurableStateBlock());

            var coordinator = new RewindCoordinator(bundle);
            var rewindTask = coordinator.RewindToAsync(targetBlock: 0, RewindPolicy.SnapshotOnly);

            var completedWhileWriteHeld = await Task.WhenAny(rewindTask, Task.Delay(TimeSpan.FromMilliseconds(500))) == rewindTask;
            Assert.False(completedWhileWriteHeld, "RewindToAsync must drain the outstanding flush before reading the cursor, not race past it");

            releaseWrite.Set();
            var result = await rewindTask;

            Assert.NotEqual(RewindOutcome.NoOp, result.Outcome);
            Assert.Equal(1UL, bundle.Metadata.GetDurableStateBlock());
        }

        [Fact]
        public async Task DiscardCapturedBlock_DoesNotWipeAnEarlierStillInFlightWindowsLayer()
        {
            using var mgr = new FaultInjectableRocksDbManager(PathKeyedOptions(_dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, _dir, ownsManager: false);
            var flush = (IAtomicBlockFlush)bundle;

            var writeEntered = new ManualResetEventSlim(false);
            var releaseWrite = new ManualResetEventSlim(false);
            mgr.OnBeforeWrite = () => { writeEntered.Set(); releaseWrite.Wait(TimeSpan.FromSeconds(10)); };

            var owner = Array.Empty<byte>();
            var path1 = new byte[] { 0x11 };
            CommitOneNode(bundle, 1, 0x11);
            var flushTask = flush.FlushBlockAsync(flat: null, block: 1, Hash32(1));
            var handedOffInTime = await Task.WhenAny(flushTask, Task.Delay(TimeSpan.FromSeconds(5))) == flushTask;
            Assert.True(handedOffInTime);
            Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(5)));

            Assert.True(bundle.WindowLayers.LayerCount >= 1, "window 1's layer must be retained while its flush is in flight");
            var rawReader = (IRawNodeReader)bundle.StateTrieNodes;
            Assert.NotNull(rawReader.TryGetRawNode(owner, path1));

            bundle.NodeCommitBlockSource.Arm(2);
            var set2 = new TrieNodeSet();
            set2.Add(new LeafNode { Owner = owner, Path = new byte[] { 0x22 }, Nibbles = new byte[] { 3 }, Value = new byte[32] });
            bundle.StateTrieNodes.Commit(set2);
            bundle.NodeCommitBlockSource.Clear();

            flush.DiscardCapturedBlock();

            Assert.True(bundle.WindowLayers.LayerCount >= 1, "an unrelated LATER block's discard must not wipe window 1's still-in-flight layer");
            var blobAfterDiscard = rawReader.TryGetRawNode(owner, path1);
            Assert.NotNull(blobAfterDiscard);

            releaseWrite.Set();
            await flush.DrainAsync();

            Assert.Equal(1UL, bundle.Metadata.GetDurableStateBlock());
            Assert.NotNull(rawReader.TryGetRawNode(owner, path1));
        }

        [Fact]
        public async Task RecoverToAsync_DrainsOutstandingFlush_BeforeForwardingToRecoveryService()
        {
            using var mgr = new FaultInjectableRocksDbManager(PathKeyedOptions(_dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, _dir, ownsManager: false);
            var flush = (IAtomicBlockFlush)bundle;

            var writeEntered = new ManualResetEventSlim(false);
            var releaseWrite = new ManualResetEventSlim(false);
            mgr.OnBeforeWrite = () => { writeEntered.Set(); releaseWrite.Wait(TimeSpan.FromSeconds(10)); };

            CommitOneNode(bundle, 1, 0x11);
            var flushTask = flush.FlushBlockAsync(flat: null, block: 1, Hash32(1));
            var handedOffInTime = await Task.WhenAny(flushTask, Task.Delay(TimeSpan.FromSeconds(5))) == flushTask;
            Assert.True(handedOffInTime);
            Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(5)));

            var recoverTask = bundle.RecoverToAsync(
                targetBlock: 999, FlatRecoverySource.ReplayJournal, progress: null, ct: default);

            var finishedWhileWriteHeld = await Task.WhenAny(recoverTask, Task.Delay(TimeSpan.FromMilliseconds(500))) == recoverTask;
            Assert.False(finishedWhileWriteHeld, "RecoverToAsync must drain the outstanding flush before doing any recovery work, not race past it");

            releaseWrite.Set();

            await Assert.ThrowsAsync<InvalidOperationException>(() => recoverTask);

            await flush.DrainAsync();
            Assert.Equal(1UL, bundle.Metadata.GetDurableStateBlock());
        }

        [Fact]
        public async Task ReconcileFlatStateAsync_DrainsOutstandingFlush_BeforeForwardingToRecoveryService()
        {
            using var mgr = new FaultInjectableRocksDbManager(PathKeyedOptions(_dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, _dir, ownsManager: false);
            var flush = (IAtomicBlockFlush)bundle;

            var writeEntered = new ManualResetEventSlim(false);
            var releaseWrite = new ManualResetEventSlim(false);
            mgr.OnBeforeWrite = () => { writeEntered.Set(); releaseWrite.Wait(TimeSpan.FromSeconds(10)); };

            CommitOneNode(bundle, 1, 0x11);
            var flushTask = flush.FlushBlockAsync(flat: null, block: 1, Hash32(1));
            var handedOffInTime = await Task.WhenAny(flushTask, Task.Delay(TimeSpan.FromSeconds(5))) == flushTask;
            Assert.True(handedOffInTime);
            Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(5)));

            var reconcileTask = bundle.ReconcileFlatStateAsync(
                stateRoot: new byte[4], progress: null, ct: default);

            var finishedWhileWriteHeld = await Task.WhenAny(reconcileTask, Task.Delay(TimeSpan.FromMilliseconds(500))) == reconcileTask;
            Assert.False(finishedWhileWriteHeld, "ReconcileFlatStateAsync must drain the outstanding flush before forwarding to the recovery service, not race past it");

            releaseWrite.Set();

            await Assert.ThrowsAsync<ArgumentException>(() => reconcileTask);

            await flush.DrainAsync();
            Assert.Equal(1UL, bundle.Metadata.GetDurableStateBlock());
        }

        private const string ClearedAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        private sealed class StagingProbeList : System.Collections.Generic.IReadOnlyList<string>
        {
            private readonly Action _onStagingEnumeration;
            private int _enumerations;

            public StagingProbeList(Action onStagingEnumeration) => _onStagingEnumeration = onStagingEnumeration;

            public int Count => 1;
            public string this[int index] => ClearedAddress;

            public System.Collections.Generic.IEnumerator<string> GetEnumerator()
            {
                if (Interlocked.Increment(ref _enumerations) > 1) _onStagingEnumeration();
                return ((System.Collections.Generic.IEnumerable<string>)new[] { ClearedAddress }).GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private static FlatStateBatch BatchWhoseStagingRuns(Action onStagingEnumeration)
            => new FlatStateBatch(null, new StagingProbeList(onStagingEnumeration), null, null, null, null);

        [Fact]
        public async Task Given_StagingThrowsInTheBackground_When_TheNextBoundaryFlushes_Then_TheFailureSurfacesAndNothingIsWritten()
        {
            using var mgr = new FaultInjectableRocksDbManager(PathKeyedOptions(_dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, _dir, ownsManager: false);
            var flush = (IAtomicBlockFlush)bundle;

            var writes = 0;
            mgr.OnBeforeWrite = () => Interlocked.Increment(ref writes);

            CommitOneNode(bundle, 1, 0x11);
            var failing = BatchWhoseStagingRuns(() => throw new InvalidOperationException("staging failed"));
            await flush.FlushBlockAsync(failing, block: 1, Hash32(1)).WaitAsync(TimeSpan.FromSeconds(30));

            CommitOneNode(bundle, 2, 0x22);
            var next = await Assert.ThrowsAsync<InvalidOperationException>(
                () => flush.FlushBlockAsync(flat: null, block: 2, Hash32(2)).WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.Equal("staging failed", next.Message);

            var drain = await Assert.ThrowsAsync<InvalidOperationException>(
                () => flush.DrainAsync().WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.Equal("staging failed", drain.Message);

            Assert.Equal(0, Volatile.Read(ref writes));
            Assert.Equal(0UL, bundle.Metadata.GetDurableStateBlock());
            Assert.Null(new RocksDbPathTrieNodeStore(mgr).TryGetRawNode(Array.Empty<byte>(), new byte[] { 0x11 }));
        }

        [Fact]
        public async Task Given_AStagingInProgress_When_TheNextWindowReadsANodeOfTheFlushingWindow_Then_ItReadsTheWindowValue()
        {
            using var mgr = new FaultInjectableRocksDbManager(PathKeyedOptions(_dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, _dir, ownsManager: false);
            var flush = (IAtomicBlockFlush)bundle;

            var stagingEntered = new ManualResetEventSlim(false);
            var releaseStaging = new ManualResetEventSlim(false);
            var holding = BatchWhoseStagingRuns(() => { stagingEntered.Set(); releaseStaging.Wait(TimeSpan.FromSeconds(30)); });

            var owner = Array.Empty<byte>();
            var path = new byte[] { 0x11 };
            CommitOneNode(bundle, 1, 0x11);
            await flush.FlushBlockAsync(holding, block: 1, Hash32(1)).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(stagingEntered.Wait(TimeSpan.FromSeconds(10)), "staging should be running in the background");

            Assert.Equal(0UL, bundle.Metadata.GetDurableStateBlock());
            var rawReader = (IRawNodeReader)bundle.StateTrieNodes;
            var duringStaging = rawReader.TryGetRawNode(owner, path);
            Assert.NotNull(duringStaging);

            releaseStaging.Set();
            await flush.DrainAsync().WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(1UL, bundle.Metadata.GetDurableStateBlock());
            Assert.Equal(duringStaging, rawReader.TryGetRawNode(owner, path));
        }

        [Fact]
        public async Task Given_AFlushStagingOrWritingInTheBackground_When_GetExecutedHashIsAskedForABlockOfThatWindow_Then_ItReturnsTheExecutedHash()
        {
            using var mgr = new FaultInjectableRocksDbManager(PathKeyedOptions(_dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, _dir, ownsManager: false);
            var flush = (IAtomicBlockFlush)bundle;

            var writeEntered = new ManualResetEventSlim(false);
            var releaseWrite = new ManualResetEventSlim(false);
            mgr.OnBeforeWrite = () => { writeEntered.Set(); releaseWrite.Wait(TimeSpan.FromSeconds(10)); };

            bundle.Metadata.SaveExecutedHash(1, Hash32(1));
            bundle.Metadata.SaveExecutedHash(2, Hash32(2));
            CommitOneNode(bundle, 2, 0x22);
            await flush.FlushBlockAsync(flat: null, block: 2, Hash32(2)).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(10)));

            Assert.Equal(Hash32(1), bundle.Metadata.GetExecutedHash(1));

            releaseWrite.Set();
            await flush.DrainAsync().WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(Hash32(1), bundle.Metadata.GetExecutedHash(1));
            Assert.Equal(Hash32(2), bundle.Metadata.GetExecutedHash(2));
        }

        [Fact]
        public async Task Given_ACommitWriteInProgress_When_GetExecutedHashIsAsked_Then_ItReturnsTheExecutedHash()
        {
            using var mgr = new FaultInjectableRocksDbManager(PathKeyedOptions(_dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, _dir, ownsManager: false);

            var writeEntered = new ManualResetEventSlim(false);
            var releaseWrite = new ManualResetEventSlim(false);
            mgr.OnBeforeWrite = () => { writeEntered.Set(); releaseWrite.Wait(TimeSpan.FromSeconds(10)); };

            bundle.Metadata.SaveExecutedHash(1, Hash32(1));
            var commit = Task.Run(() => bundle.Metadata.Commit(2, Hash32(2)));
            Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(10)));

            Assert.Equal(Hash32(1), bundle.Metadata.GetExecutedHash(1));

            releaseWrite.Set();
            await commit.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(Hash32(1), bundle.Metadata.GetExecutedHash(1));
            Assert.Equal(Hash32(2), bundle.Metadata.GetExecutedHash(2));
        }
    }
}
