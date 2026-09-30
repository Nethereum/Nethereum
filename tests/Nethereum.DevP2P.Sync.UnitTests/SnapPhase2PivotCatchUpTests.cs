using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapPhase2PivotCatchUpTests
    {
        private static readonly byte[] ZeroHash = new byte[32];

        private static byte[] Hash(byte first, byte last)
        {
            var h = new byte[32];
            h[0] = first;
            h[31] = last;
            return h;
        }

        private sealed class NoBytecodes : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private static byte[] PutAccount(InMemoryContentNodeStore store, PatriciaTrie stateTrie, byte[] accountHash, ulong balance, int storageSlots)
        {
            var storageRoot = DefaultValues.EMPTY_TRIE_HASH;
            if (storageSlots > 0)
            {
                var keccak = new Sha3Keccack();
                var storageTrie = new PatriciaTrie(store, accountHash);
                for (int i = 0; i < storageSlots; i++)
                    storageTrie.Put(
                        keccak.CalculateHash(new[] { accountHash[0], (byte)i }),
                        Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)(i + 1), 0xCD }));
                storageTrie.SaveDirtyNodesToStorage();
                storageRoot = storageTrie.Root.GetHash();
            }
            stateTrie.Put(accountHash, new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)balance,
                StateRoot = storageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            }));
            return storageRoot;
        }

        private static byte[] BuildState(InMemoryContentNodeStore store, params (byte[] Hash, ulong Balance, int Slots)[] accounts)
        {
            var stateTrie = new PatriciaTrie(store);
            foreach (var a in accounts) PutAccount(store, stateTrie, a.Hash, a.Balance, a.Slots);
            stateTrie.SaveDirtyNodesToStorage();
            return stateTrie.Root.GetHash();
        }

        private static GetAccountRangeMessage WithLimit(GetAccountRangeMessage r, byte[] limit) => new GetAccountRangeMessage
        {
            RequestId = r.RequestId,
            RootHash = r.RootHash,
            StartingHash = r.StartingHash,
            LimitHash = limit,
            ResponseBytes = r.ResponseBytes,
        };

        private static GetStorageRangesMessage WithResponseBytes(GetStorageRangesMessage r, ulong bytes) => new GetStorageRangesMessage
        {
            RequestId = r.RequestId,
            RootHash = r.RootHash,
            AccountHashes = r.AccountHashes,
            StartingHash = r.StartingHash,
            LimitHash = r.LimitHash,
            ResponseBytes = bytes,
        };

        private sealed class RecordingPeer : ISnapPeer
        {
            private readonly ISnapPeer _inner;
            private readonly List<string> _events;
            private readonly Func<GetAccountRangeMessage, CancellationToken, Task> _beforeAccountRange;
            private int _accountRangeRequests;

            public RecordingPeer(ISnapPeer inner, List<string> events, Func<GetAccountRangeMessage, CancellationToken, Task> beforeAccountRange)
            {
                _inner = inner;
                _events = events;
                _beforeAccountRange = beforeAccountRange;
            }

            public int AccountRangeRequests => Volatile.Read(ref _accountRangeRequests);

            public async Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
            {
                Interlocked.Increment(ref _accountRangeRequests);
                lock (_events) _events.Add("range@" + r.RootHash.ToHex());
                await _beforeAccountRange(r, ct).ConfigureAwait(false);
                return await _inner.GetAccountRangeAsync(r, ct).ConfigureAwait(false);
            }

            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
                => _inner.GetStorageRangesAsync(r, ct);
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => _inner.GetByteCodesAsync(r, ct);
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => _inner.GetTrieNodesAsync(r, ct);
        }

        [Fact]
        public async Task Given_PivotCatchUpIsSet_When_ThePivotMovesMidAttempt_Then_TheAttemptIsDrainedACheckpointIsEmittedAndCatchUpReceivesTheDurableSnapshotBeforeReEntry()
        {
            var store = new InMemoryContentNodeStore();
            var owner = Hash(0x41, 0x01);
            var rootA = BuildState(store, (owner, 1, 1));
            var rootB = BuildState(store, (owner, 2, 1));

            var events = new List<string>();
            var moveGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var drained = false;
            var firstRequest = 0;
            var peer = new RecordingPeer(new InProcessSnapPeer(new PatriciaSnapRequestHandler(store, new NoBytecodes())), events,
                async (r, ct) =>
                {
                    if (Interlocked.Exchange(ref firstRequest, 1) != 0) return;
                    moveGate.TrySetResult(true);
                    try { await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false); }
                    finally
                    {
                        await Task.Delay(250, CancellationToken.None).ConfigureAwait(false);
                        Volatile.Write(ref drained, true);
                    }
                });

            var checkpoints = new List<SnapSyncClient.SnapSyncCheckpoint>();
            (bool Drained, int CheckpointsBefore, IReadOnlyList<SnapSyncAccountTask> Tasks)? observed = null;
            var client = new SnapSyncClient(peer)
            {
                AccountConcurrency = 1,
                RootRefreshIntervalMs = 15,
                PivotRefresher = _ => Task.FromResult(moveGate.Task.IsCompleted ? rootB : rootA),
            };
            client.PivotCatchUp = (tasks, ct) =>
            {
                lock (checkpoints) observed = (Volatile.Read(ref drained), checkpoints.Count, tasks);
                lock (events) events.Add("catchup");
                return Task.FromResult(rootB);
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await client.SyncStateWithCheckpointAsync(
                rootA, resumeFrom: null, checkpointSink: cp => { lock (checkpoints) checkpoints.Add(cp); }, ct: cts.Token);

            Assert.NotNull(observed);
            Assert.True(observed.Value.Drained, "the catch-up ran before the moved attempt drained");
            Assert.True(observed.Value.CheckpointsBefore >= 1, "no checkpoint was emitted before the catch-up");
            var persisted = checkpoints[observed.Value.CheckpointsBefore - 1].State.Tasks;
            Assert.Equal(persisted.Count, observed.Value.Tasks.Count);
            for (int i = 0; i < persisted.Count; i++)
            {
                Assert.Equal(persisted[i].Next.ToHex(), observed.Value.Tasks[i].Next.ToHex());
                Assert.Equal(persisted[i].Last.ToHex(), observed.Value.Tasks[i].Last.ToHex());
            }

            var catchUpAt = events.IndexOf("catchup");
            Assert.True(catchUpAt > 0);
            Assert.DoesNotContain("range@" + rootB.ToHex(), events.Take(catchUpAt));
            Assert.Contains("range@" + rootB.ToHex(), events.Skip(catchUpAt));
            Assert.Equal(rootB.ToHex(), result.FinalTargetRoot.ToHex());
            Assert.Null(result.ComputedRoot);
        }

        [Fact]
        public async Task Given_PivotCatchUpReturnsARootNewerThanTheMoveSignal_When_ReEntering_Then_TheNextAttemptTargetsTheReturnedRoot()
        {
            var store = new InMemoryContentNodeStore();
            var owner = Hash(0x42, 0x02);
            var rootA = BuildState(store, (owner, 1, 1));
            var rootB = BuildState(store, (owner, 2, 1));
            var rootC = BuildState(store, (owner, 3, 1));

            var events = new List<string>();
            var moveGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var caughtUp = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstRequest = 0;
            var peer = new RecordingPeer(new InProcessSnapPeer(new PatriciaSnapRequestHandler(store, new NoBytecodes())), events,
                async (r, ct) =>
                {
                    if (Interlocked.Exchange(ref firstRequest, 1) != 0) return;
                    moveGate.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                });

            var frozenRoots = new List<string>();
            var client = new SnapSyncClient(peer)
            {
                AccountConcurrency = 1,
                RootRefreshIntervalMs = 15,
                PivotRefresher = _ => Task.FromResult(caughtUp.Task.IsCompleted ? rootC : moveGate.Task.IsCompleted ? rootB : rootA),
            };
            client.OnPhase2CycleFrozen = root => { lock (frozenRoots) frozenRoots.Add(root.ToHex()); };
            client.PivotCatchUp = (tasks, ct) =>
            {
                caughtUp.TrySetResult(true);
                return Task.FromResult(rootC);
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await client.SyncStateWithCheckpointAsync(
                rootA, resumeFrom: null, checkpointSink: _ => { }, ct: cts.Token);

            Assert.True(frozenRoots.Count >= 2);
            Assert.Equal(rootA.ToHex(), frozenRoots[0]);
            Assert.All(frozenRoots.Skip(1), root => Assert.Equal(rootC.ToHex(), root));
            Assert.DoesNotContain("range@" + rootB.ToHex(), events);
            Assert.Contains("range@" + rootC.ToHex(), events);
            Assert.Equal(rootC.ToHex(), result.FinalTargetRoot.ToHex());
        }

        [Fact]
        public async Task Given_PivotCatchUpIsSetAndNoCheckpointSink_When_Phase2Starts_Then_ItThrows()
        {
            var store = new InMemoryContentNodeStore();
            var rootA = BuildState(store, (Hash(0x43, 0x03), 1, 1));
            var peer = new RecordingPeer(new InProcessSnapPeer(new PatriciaSnapRequestHandler(store, new NoBytecodes())),
                new List<string>(), (r, ct) => Task.CompletedTask);
            var catchUps = 0;
            var client = new SnapSyncClient(peer)
            {
                AccountConcurrency = 1,
                PivotCatchUp = (tasks, ct) => { Interlocked.Increment(ref catchUps); return Task.FromResult(rootA); },
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => client.SyncStateWithCheckpointAsync(
                rootA, resumeFrom: null, checkpointSink: null, ct: CancellationToken.None));

            Assert.Equal(0, peer.AccountRangeRequests);
            Assert.Equal(0, catchUps);
        }

        private sealed class ParkedWhalePeer : ISnapPeer
        {
            private const ulong StoragePageBytes = 150;

            private readonly ISnapPeer _inner;
            private readonly byte[] _rootA;
            private readonly byte[] _rootB;
            private readonly byte[] _rootC;
            private readonly byte[] _whale;
            private readonly byte[] _whaleLastSlot;
            private readonly byte[] _refetchLimit;
            private int _whaleRequestsAtA;

            public ParkedWhalePeer(
                ISnapPeer inner, byte[] rootA, byte[] rootB, byte[] rootC, byte[] whale, byte[] whaleLastSlot, byte[] refetchLimit)
            {
                _inner = inner;
                _rootA = rootA;
                _rootB = rootB;
                _rootC = rootC;
                _whale = whale;
                _whaleLastSlot = whaleLastSlot;
                _refetchLimit = refetchLimit;
            }

            public TaskCompletionSource<bool> Move { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource<bool> WhaleServedAtB { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public volatile bool RefetchServed;

            public byte[] RefetchedPageLast { get; private set; }

            public async Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
            {
                var fromStart = ByteUtil.AreEqual(r.StartingHash, ZeroHash);
                if (ByteUtil.AreEqual(r.RootHash, _rootA) && fromStart)
                    return await _inner.GetAccountRangeAsync(WithLimit(r, _whale), ct).ConfigureAwait(false);
                if (ByteUtil.AreEqual(r.RootHash, _rootA) && r.StartingHash[0] >= 0x80)
                    await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                if (ByteUtil.AreEqual(r.RootHash, _rootB) && fromStart)
                    await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                if (ByteUtil.AreEqual(r.RootHash, _rootC) && fromStart)
                {
                    var page = await _inner.GetAccountRangeAsync(WithLimit(r, _refetchLimit), ct).ConfigureAwait(false);
                    RefetchedPageLast = page.Accounts[^1].Hash;
                    RefetchServed = true;
                    return page;
                }
                return await _inner.GetAccountRangeAsync(r, ct).ConfigureAwait(false);
            }

            public async Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
            {
                var onlyWhale = r.AccountHashes.Count == 1 && ByteUtil.AreEqual(r.AccountHashes[0], _whale);
                if (onlyWhale && ByteUtil.AreEqual(r.RootHash, _rootA) && Interlocked.Increment(ref _whaleRequestsAtA) >= 2)
                {
                    Move.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                }
                var resp = await _inner.GetStorageRangesAsync(WithResponseBytes(r, StoragePageBytes), ct).ConfigureAwait(false);
                if (onlyWhale && ByteUtil.AreEqual(r.RootHash, _rootB)
                    && resp.Slots.Count > 0 && resp.Slots[0].Any(s => ByteUtil.AreEqual(s.Hash, _whaleLastSlot)))
                    WhaleServedAtB.TrySetResult(true);
                return resp;
            }

            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => _inner.GetByteCodesAsync(r, ct);
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => _inner.GetTrieNodesAsync(r, ct);
        }

        [Fact]
        public async Task Given_ASnap2PageParkedBehindAWhale_When_ThePivotMovesAndTheWhaleCompletesBeforeTheShorterRefetchedPage_Then_NoCheckpointsDurableNextPassesTheRefetchedPagesEnd()
        {
            var store = new InMemoryContentNodeStore();
            var a1 = Hash(0x10, 0x01);
            var whale = Hash(0x20, 0x02);
            var a2 = Hash(0x30, 0x03);
            var a3 = Hash(0x40, 0x04);
            var b1 = Hash(0x90, 0x05);
            const int whaleSlots = 30;
            var rootA = BuildState(store, (a1, 1, 0), (whale, 1, whaleSlots), (a2, 1, 0), (a3, 1, 0), (b1, 1, 0));
            var rootB = BuildState(store, (a1, 2, 0), (whale, 1, whaleSlots), (a2, 1, 0), (a3, 1, 0), (b1, 1, 0));
            var rootC = BuildState(store, (a1, 3, 0), (whale, 1, whaleSlots), (a2, 1, 0), (a3, 1, 0), (b1, 1, 0));
            var keccak = new Sha3Keccack();
            var whaleLastSlot = Enumerable.Range(0, whaleSlots)
                .Select(i => keccak.CalculateHash(new[] { whale[0], (byte)i }))
                .OrderBy(h => h, ByteArrayComparer.Current).Last();

            var peer = new ParkedWhalePeer(
                new InProcessSnapPeer(new PatriciaSnapRequestHandler(store, new NoBytecodes())),
                rootA, rootB, rootC, whale, whaleLastSlot, refetchLimit: a1);

            var checkpoints = new List<(bool RefetchServed, byte[] Task0Next)>();
            var catchUpTask0Next = new List<byte[]>();
            var client = new SnapSyncClient(peer, new TrieSnapSyncSink(new InMemoryContentNodeStore(), new InMemoryStateStore(), (ISnapFlatStateWriter)null))
            {
                AccountConcurrency = 2,
                LargeContractConcurrency = 1,
                RootRefreshIntervalMs = 15,
                CheckpointBytesThreshold = 1,
                PivotRefresher = _ => Task.FromResult(
                    peer.WhaleServedAtB.Task.IsCompleted ? rootC : peer.Move.Task.IsCompleted ? rootB : rootA),
            };
            client.PivotCatchUp = (tasks, ct) =>
            {
                lock (catchUpTask0Next) catchUpTask0Next.Add(tasks.FirstOrDefault(t => t.Last[0] < 0x80)?.Next);
                return Task.FromResult(peer.WhaleServedAtB.Task.IsCompleted ? rootC : rootB);
            };

            void Record(SnapSyncClient.SnapSyncCheckpoint cp)
            {
                lock (checkpoints) checkpoints.Add((peer.RefetchServed, cp.State.Tasks[0].Next));
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await client.SyncStateWithCheckpointAsync(rootA, resumeFrom: null, checkpointSink: Record, ct: cts.Token);

            Assert.True(peer.WhaleServedAtB.Task.IsCompleted);
            Assert.Equal(2, catchUpTask0Next.Count);
            Assert.True(catchUpTask0Next[1] != null && ByteUtil.AreEqual(catchUpTask0Next[1], ZeroHash),
                "after the whale completed, the second move's durable snapshot does not hold task 0 at its page start");
            Assert.NotNull(peer.RefetchedPageLast);
            Assert.Equal(a1.ToHex(), peer.RefetchedPageLast.ToHex());
            var refetchedEnd = SnapHashRanges.IncrementHash(peer.RefetchedPageLast);
            var beforeRefetchServed = checkpoints.Where(c => !c.RefetchServed).ToList();
            Assert.NotEmpty(beforeRefetchServed);
            Assert.All(beforeRefetchServed, c => Assert.True(
                ByteArrayComparer.Current.Compare(c.Task0Next, refetchedEnd) <= 0,
                $"checkpoint persisted task 0 at 0x{c.Task0Next.ToHex()}, past the refetched page's end 0x{refetchedEnd.ToHex()}"));
        }
    }
}
