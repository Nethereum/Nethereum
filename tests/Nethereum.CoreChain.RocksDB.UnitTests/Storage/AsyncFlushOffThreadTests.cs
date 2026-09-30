using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Storage
{
    public class AsyncFlushOffThreadTests : IDisposable
    {
        private readonly string _dir;

        public AsyncFlushOffThreadTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-asyncflush-" + Guid.NewGuid().ToString("N"));
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
            public volatile bool SkipRealWrite;

            public override void Write(RocksDbSharp.WriteBatch batch, RocksDbSharp.WriteOptions writeOptions = null)
            {
                OnBeforeWrite?.Invoke();
                if (SkipRealWrite) return;
                base.Write(batch, writeOptions);
            }
        }

        [Fact]
        public async Task CrossWindowColdRead_TrieNode_ServesInFlightWindowValue_NotStaleDisk()
        {
            using var mgr = new FaultInjectableRocksDbManager(PathKeyedOptions(_dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, _dir, ownsManager: false);
            var flush = (IAtomicBlockFlush)bundle;

            var writeEntered = new ManualResetEventSlim(false);
            var releaseWrite = new ManualResetEventSlim(false);
            mgr.OnBeforeWrite = () =>
            {
                writeEntered.Set();
                releaseWrite.Wait(TimeSpan.FromSeconds(10));
            };

            var owner = Array.Empty<byte>();
            var path = new byte[] { 0x01, 0x02 };
            var nodeV1 = new LeafNode { Owner = owner, Path = path, Nibbles = new byte[] { 3, 4 }, Value = new byte[] { 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA } };

            bundle.NodeCommitBlockSource.Arm(1);
            var set1 = new TrieNodeSet();
            set1.Add(nodeV1);
            bundle.StateTrieNodes.Commit(set1);
            bundle.NodeCommitBlockSource.Clear();

            var flushTask = flush.FlushBlockAsync(flat: null, block: 1, Hash32(1));
            var completedInTime = await Task.WhenAny(flushTask, Task.Delay(TimeSpan.FromSeconds(5))) == flushTask;
            Assert.True(completedInTime, "FlushBlockAsync should return once staged/handed-off, not wait for the write");

            Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(5)), "expected the flush task to have entered Write by now");

            var rawReader = (IRawNodeReader)bundle.StateTrieNodes;
            var blobWhileInFlight = rawReader.TryGetRawNode(owner, path);
            Assert.NotNull(blobWhileInFlight);
            Assert.Equal(nodeV1.GetEncodedData(), blobWhileInFlight);

            releaseWrite.Set();
            await ((IAtomicBlockFlush)bundle).DrainAsync();

            var blobAfterDurable = rawReader.TryGetRawNode(owner, path);
            Assert.NotNull(blobAfterDurable);
            Assert.Equal(nodeV1.GetEncodedData(), blobAfterDurable);
        }

        [Fact]
        public async Task CrossWindowColdRead_Account_ServesInFlightWindowValue_NotStaleDisk()
        {
            using var mgr = new FaultInjectableRocksDbManager(PathKeyedOptions(_dir));
            var journalOptions = new HistoricalStateOptions { MaxHistoryBlocks = 100_000, EnablePruning = false };
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, _dir, journalOptions: journalOptions, ownsManager: false);
            var flush = (IAtomicBlockFlush)bundle;

            var writeEntered = new ManualResetEventSlim(false);
            var releaseWrite = new ManualResetEventSlim(false);
            mgr.OnBeforeWrite = () =>
            {
                writeEntered.Set();
                releaseWrite.Wait(TimeSpan.FromSeconds(10));
            };

            const string address = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var account = new Account { Balance = 777, Nonce = 3 };
            var flat = new FlatStateBatch(
                deletedAccountAddresses: Array.Empty<string>(),
                clearedStorageAddresses: Array.Empty<string>(),
                nonZeroStorage: Array.Empty<(string, BigInteger, byte[])>(),
                deletedSlots: Array.Empty<(string, BigInteger)>(),
                accounts: new[] { (address, account) },
                code: Array.Empty<(byte[], byte[])>());

            var flushTask = flush.FlushBlockAsync(flat, block: 1, Hash32(1));
            var completedInTime = await Task.WhenAny(flushTask, Task.Delay(TimeSpan.FromSeconds(5))) == flushTask;
            Assert.True(completedInTime, "FlushBlockAsync should return once staged/handed-off, not wait for the write");
            Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(5)), "expected the flush task to have entered Write by now");

            var readWhileInFlight = await bundle.State.GetAccountAsync(address);
            Assert.NotNull(readWhileInFlight);
            Assert.Equal(777UL, (ulong)readWhileInFlight.Balance);
            Assert.Equal(3UL, (ulong)readWhileInFlight.Nonce);

            releaseWrite.Set();
            await ((IAtomicBlockFlush)bundle).DrainAsync();

            var readAfterDurable = await bundle.State.GetAccountAsync(address);
            Assert.NotNull(readAfterDurable);
            Assert.Equal(777UL, (ulong)readAfterDurable.Balance);
        }

        [Fact]
        public async Task NoDoubleWrite_StagedOwnership_ExactlyOneRocksWrite_LandsCursorAndWithdrawals()
        {
            using var mgr = new FaultInjectableRocksDbManager(PathKeyedOptions(_dir));
            var journalOptions = new HistoricalStateOptions { MaxHistoryBlocks = 100_000, EnablePruning = false };
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, _dir, journalOptions: journalOptions, ownsManager: false);
            var flush = (IAtomicBlockFlush)bundle;

            await bundle.Blocks.SaveAsync(new Nethereum.Model.BlockHeader { BlockNumber = 1 }, Hash32(1));

            var releaseWrite = new ManualResetEventSlim(false);
            var writeEntered = new ManualResetEventSlim(false);
            mgr.OnBeforeWrite = () => { writeEntered.Set(); releaseWrite.Wait(TimeSpan.FromSeconds(10)); };

            var withdrawals = new List<Withdrawal> { new Withdrawal { Index = 1, ValidatorIndex = 1, Address = new byte[20], AmountInGwei = 5 } };
            flush.ArmWithdrawals(1, withdrawals);

            var baselineWrites = mgr.WriteCallCount;

            var flushTask = flush.FlushBlockAsync(flat: null, block: 1, Hash32(1));
            var completedInTime = await Task.WhenAny(flushTask, Task.Delay(TimeSpan.FromSeconds(5))) == flushTask;
            Assert.True(completedInTime);
            Assert.True(writeEntered.Wait(TimeSpan.FromSeconds(5)));

            bool ownedByStagedFlush = flush.BlockOwnedByStagedFlush(1);
            Assert.True(ownedByStagedFlush, "staging should have armed ownership before FlushBlockAsync returned");
            if (!ownedByStagedFlush && bundle.Metadata.GetLastBlock() < 1)
                bundle.Metadata.Commit(1, Hash32(1));

            bool alreadyFolded = flush.WithdrawalsFoldedFor(1);
            Assert.True(alreadyFolded, "withdrawals should have been staged into the same batch");
            if (!alreadyFolded)
                await bundle.Withdrawals.SaveAsync(Hash32(1), withdrawals);

            Assert.Equal(baselineWrites, mgr.WriteCallCount);

            releaseWrite.Set();
            await flush.DrainAsync();

            Assert.Equal(baselineWrites + 1, mgr.WriteCallCount);

            Assert.Equal(1UL, bundle.Metadata.GetLastBlock());
            var storedWithdrawals = await bundle.Withdrawals.GetByBlockNumberAsync(1);
            Assert.NotNull(storedWithdrawals);
            Assert.Single(storedWithdrawals);
        }

        [Fact]
        public async Task FlushThreadException_FaultsAndHalts_DurableCursorUnchanged_LayersNotDropped()
        {
            using var mgr = new FaultInjectableRocksDbManager(PathKeyedOptions(_dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, _dir, ownsManager: false);
            var flush = (IAtomicBlockFlush)bundle;

            mgr.OnBeforeWrite = () => throw new InvalidOperationException("injected fatal write failure");

            var owner = Array.Empty<byte>();
            var path = new byte[] { 0x09 };
            var node = new LeafNode { Owner = owner, Path = path, Nibbles = new byte[] { 1 }, Value = new byte[] { 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB } };
            bundle.NodeCommitBlockSource.Arm(1);
            var set = new TrieNodeSet();
            set.Add(node);
            bundle.StateTrieNodes.Commit(set);
            bundle.NodeCommitBlockSource.Clear();

            var flushTask = flush.FlushBlockAsync(flat: null, block: 1, Hash32(1));
            await flushTask;

            ulong headBefore = bundle.Metadata.GetLastBlock();
            ulong durableBefore = bundle.Metadata.GetDurableStateBlock();

            await Assert.ThrowsAsync<InvalidOperationException>(() => flush.DrainAsync());

            Assert.Equal(headBefore, bundle.Metadata.GetLastBlock());
            Assert.Equal(durableBefore, bundle.Metadata.GetDurableStateBlock());

            var rawReader = (IRawNodeReader)bundle.StateTrieNodes;
            var blob = rawReader.TryGetRawNode(owner, path);
            Assert.NotNull(blob);
            Assert.Equal(node.GetEncodedData(), blob);

            await Assert.ThrowsAsync<InvalidOperationException>(() => flush.DrainAsync());
        }

        [Fact]
        public async Task Backpressure_SecondWindowHandoff_BlocksUntilFirstFlushLands_ThenRecovers()
        {
            using var mgr = new FaultInjectableRocksDbManager(PathKeyedOptions(_dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, _dir, ownsManager: false);
            var flush = (IAtomicBlockFlush)bundle;

            var releaseWrite = new ManualResetEventSlim(false);
            mgr.OnBeforeWrite = () => releaseWrite.Wait(TimeSpan.FromSeconds(10));

            void CommitOneNode(ulong block, byte tag)
            {
                bundle.NodeCommitBlockSource.Arm(block);
                var set = new TrieNodeSet();
                var val = new byte[32];
                for (int i = 0; i < val.Length; i++) val[i] = tag;
                set.Add(new LeafNode { Owner = Array.Empty<byte>(), Path = new byte[] { tag }, Nibbles = new byte[] { 1, 2 }, Value = val });
                bundle.StateTrieNodes.Commit(set);
                bundle.NodeCommitBlockSource.Clear();
            }

            CommitOneNode(1, 0x11);
            var flush1 = flush.FlushBlockAsync(flat: null, block: 1, Hash32(1));
            await flush1;

            CommitOneNode(2, 0x22);
            var flush2 = flush.FlushBlockAsync(flat: null, block: 2, Hash32(2));
            var raced = await Task.WhenAny(flush2, Task.Delay(TimeSpan.FromMilliseconds(500)));
            Assert.NotSame(flush2, raced);

            Assert.True(bundle.WindowLayers.LayerCount <= 1,
                "at most window 1's own layer should be retained while window 2 has not even staged yet");

            releaseWrite.Set();
            await flush2;
            await flush.DrainAsync();

            Assert.Equal(2UL, bundle.Metadata.GetDurableStateBlock());
        }

        [Fact]
        public async Task DrainAsync_OnlyCompletesAfterInFlightFlushIsDurable()
        {
            using var mgr = new FaultInjectableRocksDbManager(PathKeyedOptions(_dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, _dir, ownsManager: false);
            var flush = (IAtomicBlockFlush)bundle;

            var releaseWrite = new ManualResetEventSlim(false);
            mgr.OnBeforeWrite = () => releaseWrite.Wait(TimeSpan.FromSeconds(10));

            bundle.NodeCommitBlockSource.Arm(1);
            var set = new TrieNodeSet();
            set.Add(new LeafNode { Owner = Array.Empty<byte>(), Path = new byte[] { 0x77 }, Nibbles = new byte[] { 5 }, Value = new byte[32] });
            bundle.StateTrieNodes.Commit(set);
            bundle.NodeCommitBlockSource.Clear();
            await flush.FlushBlockAsync(flat: null, block: 1, Hash32(1));

            var drain = flush.DrainAsync();
            var raced = await Task.WhenAny(drain, Task.Delay(TimeSpan.FromMilliseconds(500)));
            Assert.NotSame(drain, raced);

            Assert.Equal(0UL, bundle.Metadata.GetDurableStateBlock());

            releaseWrite.Set();
            await drain;
            Assert.Equal(1UL, bundle.Metadata.GetDurableStateBlock());
        }

        [Fact]
        public async Task KillNineMidAsyncFlush_ResumesFromLastDurableWindow_NoTornState()
        {
            var dirCrash = Path.Combine(Path.GetTempPath(), "necc-asyncflush-kill-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dirCrash);
            try
            {
                ulong headAfterWindow1;
                using (var mgr = new FaultInjectableRocksDbManager(PathKeyedOptions(dirCrash)))
                {
                    var bundle = RocksDbChainStoreBundle.FromManager(mgr, dirCrash, ownsManager: false);
                    var flush = (IAtomicBlockFlush)bundle;

                    void CommitOneNode(ulong block, byte tag)
                    {
                        bundle.NodeCommitBlockSource.Arm(block);
                        var set = new TrieNodeSet();
                        var val = new byte[32];
                        for (int i = 0; i < val.Length; i++) val[i] = tag;
                        set.Add(new LeafNode { Owner = Array.Empty<byte>(), Path = new byte[] { tag }, Nibbles = new byte[] { 1, 2 }, Value = val });
                        bundle.StateTrieNodes.Commit(set);
                        bundle.NodeCommitBlockSource.Clear();
                    }

                    CommitOneNode(1, 0x11);
                    await flush.FlushBlockAsync(flat: null, block: 1, Hash32(1));
                    await flush.DrainAsync();
                    headAfterWindow1 = bundle.Metadata.GetDurableStateBlock();
                    Assert.Equal(1UL, headAfterWindow1);

                    mgr.SkipRealWrite = true;
                    CommitOneNode(2, 0x22);
                    await flush.FlushBlockAsync(flat: null, block: 2, Hash32(2));
                }

                using (var reopenMgr = new RocksDbManager(PathKeyedOptions(dirCrash)))
                {
                    var reopened = RocksDbChainStoreBundle.FromManager(reopenMgr, dirCrash, ownsManager: false);

                    Assert.Equal(headAfterWindow1, reopened.Metadata.GetLastBlock());
                    Assert.Equal(headAfterWindow1, reopened.Metadata.GetDurableStateBlock());

                    var owner = Array.Empty<byte>();
                    var rawReader = (IRawNodeReader)reopened.StateTrieNodes;
                    Assert.NotNull(rawReader.TryGetRawNode(owner, new byte[] { 0x11 }));
                    Assert.Null(rawReader.TryGetRawNode(owner, new byte[] { 0x22 }));
                }
            }
            finally
            {
                try { if (Directory.Exists(dirCrash)) Directory.Delete(dirCrash, recursive: true); } catch { }
            }
        }
    }
}
