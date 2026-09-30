using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapSyncClientFrozenPivotReentryTests
    {
        private sealed class InMemoryBytecodeStore : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private static (byte[] StateRoot, byte[] StorageRoot, List<(byte[] Key, byte[] Value)> Entries) BuildOwnerState(
            InMemoryContentNodeStore store, byte[] owner, int count, int seed)
        {
            var keccak = new Sha3Keccack();
            var storageTrie = new PatriciaTrie(store, owner);
            var entries = new List<(byte[] Key, byte[] Value)>();
            for (int i = 0; i < count; i++)
            {
                var key = keccak.CalculateHash(new[] { (byte)((seed >> 8) & 0xff), (byte)(seed & 0xff), (byte)i });
                var value = new byte[] { (byte)(i & 0xff), 0xCD };
                entries.Add((key, value));
                storageTrie.Put(key, value);
            }
            storageTrie.SaveDirtyNodesToStorage();
            var storageRoot = storageTrie.Root.GetHash();

            var account = new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1,
                StateRoot = storageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            };
            var stateTrie = new PatriciaTrie(store);
            stateTrie.Put(owner, new AccountEncoder().Encode(account));
            stateTrie.SaveDirtyNodesToStorage();

            return (stateTrie.Root.GetHash(), storageRoot, entries);
        }

        private sealed class MoveAfterFirstAccountRangePeer : ISnapPeer
        {
            private readonly ISnapPeer _inner;
            private readonly TaskCompletionSource<bool> _moveGate;
            private int _accountRangeCalls;

            public MoveAfterFirstAccountRangePeer(ISnapPeer inner, TaskCompletionSource<bool> moveGate)
            {
                _inner = inner;
                _moveGate = moveGate;
            }

            public int AccountRangeCalls => Volatile.Read(ref _accountRangeCalls);

            public async Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
            {
                var call = Interlocked.Increment(ref _accountRangeCalls);
                if (call == 1)
                {
                    _moveGate.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                }
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
        public async Task Sync_PivotMovesMidAttempt_FreezesPerAttempt_DrainsAndReentersAtNewRoot()
        {
            var store = new InMemoryContentNodeStore();
            var owner = new byte[32];
            owner[0] = 0x40;
            owner[31] = 0x01;

            var (stateRootA, storageRootA, _) = BuildOwnerState(store, owner, count: 3, seed: 1);
            var (stateRootB, storageRootB, entriesB) = BuildOwnerState(store, owner, count: 3, seed: 2);
            Assert.False(ByteArrayComparer.Current.Equals(stateRootA, stateRootB),
                "test fixture assumption: the two roots must differ");
            Assert.False(ByteArrayComparer.Current.Equals(storageRootA, storageRootB),
                "test fixture assumption: the two storage roots must differ");

            var handler = new PatriciaSnapRequestHandler(store, new InMemoryBytecodeStore());
            var moveGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var peer = new MoveAfterFirstAccountRangePeer(new InProcessSnapPeer(handler), moveGate);

            var client = new SnapSyncClient(peer)
            {
                AccountConcurrency = 1,
                RootRefreshIntervalMs = 15,
                PivotRefresher = _ => Task.FromResult(moveGate.Task.IsCompleted ? stateRootB : stateRootA),
            };

            var frozenRootsObserved = new List<byte[]>();
            client.OnPhase2CycleFrozen = root =>
            {
                lock (frozenRootsObserved) frozenRootsObserved.Add(root);
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await client.SyncStateWithCheckpointAsync(
                stateRootA, resumeFrom: null, checkpointSink: null, ct: cts.Token);

            Assert.Equal(2, frozenRootsObserved.Count);
            Assert.Equal(stateRootA, frozenRootsObserved[0], ByteArrayComparer.Current);
            Assert.Equal(stateRootB, frozenRootsObserved[1], ByteArrayComparer.Current);

            Assert.Equal(2, peer.AccountRangeCalls);

            Assert.Equal(stateRootB, result.ComputedRoot, ByteArrayComparer.Current);
            Assert.Equal(stateRootB, result.FinalTargetRoot, ByteArrayComparer.Current);
            Assert.True(result.RootMatchesTarget);

            Assert.NotNull(result.StateTrie);
            var accountRlp = result.StateTrie.Get(owner);
            Assert.NotNull(accountRlp);
            var decoded = new AccountEncoder().Decode(accountRlp);
            Assert.Equal(storageRootB, decoded.StateRoot, ByteArrayComparer.Current);

            Assert.NotEmpty(entriesB);
        }

        private sealed class RootRecordingPeer : ISnapPeer
        {
            private readonly ISnapPeer _inner;
            private readonly Func<bool> _valveOn;
            private readonly List<(byte[] Root, bool ValveOn)> _accountRangeRequests = new();

            public RootRecordingPeer(ISnapPeer inner, Func<bool> valveOn)
            {
                _inner = inner;
                _valveOn = valveOn;
            }

            public List<(byte[] Root, bool ValveOn)> AccountRangeRequests()
            {
                lock (_accountRangeRequests) return new List<(byte[] Root, bool ValveOn)>(_accountRangeRequests);
            }

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
            {
                lock (_accountRangeRequests) _accountRangeRequests.Add((r.RootHash, _valveOn()));
                return _inner.GetAccountRangeAsync(r, ct);
            }

            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
                => _inner.GetStorageRangesAsync(r, ct);
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => _inner.GetByteCodesAsync(r, ct);
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => _inner.GetTrieNodesAsync(r, ct);
        }

        [Fact]
        public async Task Given_EveryConsumerParkedByStorageBackpressure_When_ThePivotMoves_Then_Phase2ReanchorsAtTheNewRootWithTheValveStillOnAndFetchesOnlyAtTheNewRootOnceItClears()
        {
            var store = new InMemoryContentNodeStore();
            var owner = new byte[32];
            owner[0] = 0x40;
            owner[31] = 0x02;

            var (stateRootA, _, _) = BuildOwnerState(store, owner, count: 3, seed: 3);
            var (stateRootB, storageRootB, _) = BuildOwnerState(store, owner, count: 3, seed: 4);
            Assert.False(ByteArrayComparer.Current.Equals(stateRootA, stateRootB),
                "test fixture assumption: the two roots must differ");

            var valveOn = 1;
            bool ValveOn() => Volatile.Read(ref valveOn) == 1;

            var handler = new PatriciaSnapRequestHandler(store, new InMemoryBytecodeStore());
            var peer = new RootRecordingPeer(new InProcessSnapPeer(handler), ValveOn);

            var client = new SnapSyncClient(peer)
            {
                AccountConcurrency = 2,
                RootRefreshIntervalMs = 15,
                StateBackpressurePollMs = 10,
                PivotRefresher = _ => Task.FromResult(stateRootB),
                StateWriteBackpressure = () => ValveOn() ? "state WRITE-STOP: rocksdb.is-write-stopped=1 (test)" : null,
            };

            var frozenRootsObserved = new List<(byte[] Root, bool ValveOn)>();
            var reanchoredAtB = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.OnPhase2CycleFrozen = root =>
            {
                lock (frozenRootsObserved) frozenRootsObserved.Add((root, ValveOn()));
                if (ByteArrayComparer.Current.Equals(root, stateRootB)) reanchoredAtB.TrySetResult(ValveOn());
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var sync = client.SyncStateWithCheckpointAsync(stateRootA, resumeFrom: null, checkpointSink: null, ct: cts.Token);

            Assert.True(await reanchoredAtB.Task.WaitAsync(TimeSpan.FromSeconds(10)));
            await Task.Delay(150);
            Assert.False(sync.IsCompleted);
            Assert.Empty(peer.AccountRangeRequests());

            Volatile.Write(ref valveOn, 0);
            var result = await sync;

            Assert.Equal(2, frozenRootsObserved.Count);
            Assert.Equal(stateRootA, frozenRootsObserved[0].Root, ByteArrayComparer.Current);
            Assert.Equal(stateRootB, frozenRootsObserved[1].Root, ByteArrayComparer.Current);
            Assert.All(frozenRootsObserved, f => Assert.True(f.ValveOn));

            var requests = peer.AccountRangeRequests();
            Assert.NotEmpty(requests);
            Assert.All(requests, r =>
            {
                Assert.Equal(stateRootB, r.Root, ByteArrayComparer.Current);
                Assert.False(r.ValveOn);
            });

            Assert.Equal(stateRootB, result.ComputedRoot, ByteArrayComparer.Current);
            Assert.Equal(stateRootB, result.FinalTargetRoot, ByteArrayComparer.Current);
            Assert.True(result.RootMatchesTarget);
            var decoded = new AccountEncoder().Decode(result.StateTrie.Get(owner));
            Assert.Equal(storageRootB, decoded.StateRoot, ByteArrayComparer.Current);
        }

        private static byte[] Acc(byte first)
        {
            var a = new byte[32];
            a[0] = first;
            a[31] = 0x11;
            return a;
        }

        private sealed class MoveOnSecondStorageRangePeer : ISnapPeer
        {
            private readonly ISnapPeer _inner;
            private readonly TaskCompletionSource<bool> _moveGate;
            private int _storageRangeCalls;

            public MoveOnSecondStorageRangePeer(ISnapPeer inner, TaskCompletionSource<bool> moveGate)
            {
                _inner = inner;
                _moveGate = moveGate;
            }

            public int StorageRangeCalls => Volatile.Read(ref _storageRangeCalls);

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
                => _inner.GetAccountRangeAsync(r, ct);

            public async Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
            {
                var call = Interlocked.Increment(ref _storageRangeCalls);
                if (call == 2)
                {
                    _moveGate.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                }
                return await _inner.GetStorageRangesAsync(r, ct).ConfigureAwait(false);
            }

            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => _inner.GetByteCodesAsync(r, ct);
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => _inner.GetTrieNodesAsync(r, ct);
        }

        [Fact]
        public async Task Sync_PivotMovesWhileWhaleStorageSubtaskInFlight_ReentersAtNewRootWithoutMismatch()
        {
            var store = new InMemoryContentNodeStore();
            var owner = Acc(0x70);

            var (stateRootA, storageRootA, _) = BuildOwnerState(store, owner, count: 30, seed: 5);
            var (stateRootB, storageRootB, entriesB) = BuildOwnerState(store, owner, count: 30, seed: 6);
            Assert.False(ByteArrayComparer.Current.Equals(stateRootA, stateRootB),
                "test fixture assumption: the two roots must differ");
            Assert.False(ByteArrayComparer.Current.Equals(storageRootA, storageRootB),
                "test fixture assumption: the two storage roots must differ");

            var handler = new PatriciaSnapRequestHandler(store, new InMemoryBytecodeStore());
            var moveGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var peer = new MoveOnSecondStorageRangePeer(new InProcessSnapPeer(handler), moveGate);

            var clientNodeStore = new InMemoryContentNodeStore();
            var clientStateStore = new InMemoryStateStore();
            var sink = new TrieSnapSyncSink(clientNodeStore, clientStateStore, flatWriter: null);

            var client = new SnapSyncClient(peer, sink, responseBytesBudget: 300UL)
            {
                AccountConcurrency = 1,
                LargeContractConcurrency = 2,
                RootRefreshIntervalMs = 15,
                TaskSetStallTimeout = TimeSpan.FromSeconds(3),
                ActiveLeaseStallTimeout = TimeSpan.FromSeconds(3),
                PivotRefresher = _ => Task.FromResult(moveGate.Task.IsCompleted ? stateRootB : stateRootA),
            };

            var frozenRootsObserved = new List<byte[]>();
            client.OnPhase2CycleFrozen = root =>
            {
                lock (frozenRootsObserved) frozenRootsObserved.Add(root);
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            var result = await client.SyncStateWithCheckpointAsync(
                stateRootA, resumeFrom: null, checkpointSink: null, ct: cts.Token);

            Assert.Equal(2, frozenRootsObserved.Count);
            Assert.Equal(stateRootA, frozenRootsObserved[0], ByteArrayComparer.Current);
            Assert.Equal(stateRootB, frozenRootsObserved[1], ByteArrayComparer.Current);

            Assert.Equal(stateRootB, result.ComputedRoot, ByteArrayComparer.Current);
            Assert.Equal(stateRootB, result.FinalTargetRoot, ByteArrayComparer.Current);
            Assert.True(result.RootMatchesTarget);
            Assert.DoesNotContain(result.AccountsNeedingHeal,
                a => ByteArrayComparer.Current.Equals(a.AccountHash, owner));

            var reloaded = PatriciaTrie.LoadFromStorage(storageRootB, clientNodeStore, owner);
            foreach (var (key, value) in entriesB)
                Assert.Equal(value, reloaded.Get(key));
        }
    }
}
