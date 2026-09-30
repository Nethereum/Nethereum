using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Snap.CatchUp;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapPhase2BalHealPivotMoveTests
    {
        private sealed class InMemoryBytecodeStore : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private static (byte[] StateRoot, byte[] StorageRoot) BuildOwnerState(
            InMemoryContentNodeStore store, byte[] owner, int seed)
        {
            var keccak = new Sha3Keccack();
            var storageTrie = new PatriciaTrie(store, owner);
            var key = keccak.CalculateHash(new[] { (byte)seed });
            storageTrie.Put(key, new byte[] { (byte)seed, 0xCD });
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

            return (stateTrie.Root.GetHash(), storageRoot);
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
        public async Task Given_ZeroRangesHaveProgressedYet_When_ThePivotMovesMidAttempt_Then_ThePivotCatchUpRunsOnceAndSyncConvergesAtTheReturnedRoot()
        {
            var store = new InMemoryContentNodeStore();
            var owner = new byte[32];
            owner[0] = 0x41;
            owner[31] = 0x01;

            var (stateRootA, _) = BuildOwnerState(store, owner, seed: 21);
            var (stateRootB, _) = BuildOwnerState(store, owner, seed: 22);
            Assert.False(ByteArrayComparer.Current.Equals(stateRootA, stateRootB));

            var handler = new PatriciaSnapRequestHandler(store, new InMemoryBytecodeStore());
            var moveGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var peer = new MoveAfterFirstAccountRangePeer(new InProcessSnapPeer(handler), moveGate);

            var catchUps = 0;
            var client = new SnapSyncClient(peer)
            {
                AccountConcurrency = 1,
                RootRefreshIntervalMs = 15,
                PivotRefresher = _ => Task.FromResult(moveGate.Task.IsCompleted ? stateRootB : stateRootA),
                PivotCatchUp = (tasks, ct) =>
                {
                    Interlocked.Increment(ref catchUps);
                    Assert.NotNull(tasks);
                    return Task.FromResult(stateRootB);
                },
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await client.SyncStateWithCheckpointAsync(
                stateRootA, resumeFrom: null, checkpointSink: _ => { }, ct: cts.Token);

            Assert.Equal(1, catchUps);
            Assert.Equal(stateRootB, result.FinalTargetRoot, ByteArrayComparer.Current);
            Assert.Equal(stateRootB, result.StateTrie.Root.GetHash(), ByteArrayComparer.Current);
        }

        [Fact]
        public async Task Given_NoPivotCatchUpIsWired_When_ThePivotMovesMidAttempt_Then_BehaviorIsUnaffected()
        {
            var store = new InMemoryContentNodeStore();
            var owner = new byte[32];
            owner[0] = 0x43;
            owner[31] = 0x03;

            var (stateRootA, _) = BuildOwnerState(store, owner, seed: 41);
            var (stateRootB, _) = BuildOwnerState(store, owner, seed: 42);

            var handler = new PatriciaSnapRequestHandler(store, new InMemoryBytecodeStore());
            var moveGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var peer = new MoveAfterFirstAccountRangePeer(new InProcessSnapPeer(handler), moveGate);

            var client = new SnapSyncClient(peer)
            {
                AccountConcurrency = 1,
                RootRefreshIntervalMs = 15,
                PivotRefresher = _ => Task.FromResult(moveGate.Task.IsCompleted ? stateRootB : stateRootA),
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await client.SyncStateWithCheckpointAsync(
                stateRootA, resumeFrom: null, checkpointSink: null, ct: cts.Token);

            Assert.True(result.RootMatchesTarget);
            Assert.Equal(stateRootB, result.ComputedRoot, ByteArrayComparer.Current);
        }

        [Fact]
        public async Task Given_OneRangeAlreadyCompleted_When_ThePivotMovesMidAttemptOnADisjointRange_Then_TheCompletedRangeIsNotRefetched_AndTheCatchUpFrontierMarksItFetched()
        {
            var store = new InMemoryContentNodeStore();
            var ownerLow = new byte[32];
            ownerLow[0] = 0x10;
            ownerLow[31] = 0x05;
            var ownerHigh = new byte[32];
            ownerHigh[0] = 0x90;
            ownerHigh[31] = 0x06;

            var stateRootA = BuildTwoOwnerStateRoot(store, ownerLow, lowSeed: 51, ownerHigh, highSeed: 52);
            var stateRootB = BuildTwoOwnerStateRoot(store, ownerLow, lowSeed: 51, ownerHigh, highSeed: 53);
            Assert.False(ByteArrayComparer.Current.Equals(stateRootA, stateRootB));

            var handler = new PatriciaSnapRequestHandler(store, new InMemoryBytecodeStore());
            var lowRangeComplete = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var highRangeStalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var peer = new RangeSelectiveStallPeer(new InProcessSnapPeer(handler), lowRangeComplete, highRangeStalled);

            bool Moved() => lowRangeComplete.Task.IsCompleted && highRangeStalled.Task.IsCompleted;

            IReadOnlyList<SnapSyncAccountTask> catchUpTasks = null;
            var catchUps = 0;
            var client = new SnapSyncClient(peer)
            {
                AccountConcurrency = 2,
                RootRefreshIntervalMs = 15,
                PivotRefresher = _ => Task.FromResult(Moved() ? stateRootB : stateRootA),
                PivotCatchUp = (tasks, ct) =>
                {
                    Interlocked.Increment(ref catchUps);
                    catchUpTasks = tasks;
                    return Task.FromResult(stateRootB);
                },
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await client.SyncStateWithCheckpointAsync(
                stateRootA, resumeFrom: null, checkpointSink: _ => { }, ct: cts.Token);

            Assert.Equal(stateRootB, result.StateTrie.Root.GetHash(), ByteArrayComparer.Current);
            Assert.Equal(1, catchUps);
            Assert.Equal(1, peer.LowRangeAccountCalls);

            Assert.NotNull(catchUpTasks);
            var frontier = new SnapTaskFrontier(catchUpTasks);
            Assert.True(frontier.IsAccountFetched(ownerLow),
                "the frontier handed to the catch-up must reflect the range that already completed");
            Assert.False(frontier.IsAccountFetched(ownerHigh));
        }

        [Fact]
        public async Task Given_APivotMove_When_TheOldAttemptIsStillDraining_Then_ThePivotCatchUpWaitsForFullDrainBeforeRunning()
        {
            var store = new InMemoryContentNodeStore();
            var owner = new byte[32];
            owner[0] = 0x44;
            owner[31] = 0x07;

            var (stateRootA, _) = BuildOwnerState(store, owner, seed: 61);
            var (stateRootB, _) = BuildOwnerState(store, owner, seed: 62);

            var handler = new PatriciaSnapRequestHandler(store, new InMemoryBytecodeStore());
            var moveGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var peer = new SlowToDrainPeer(new InProcessSnapPeer(handler), moveGate, TimeSpan.FromMilliseconds(250));

            var observedDrainedFlags = new List<bool>();
            var client = new SnapSyncClient(peer)
            {
                AccountConcurrency = 1,
                RootRefreshIntervalMs = 15,
                PivotRefresher = _ => Task.FromResult(moveGate.Task.IsCompleted ? stateRootB : stateRootA),
                PivotCatchUp = (tasks, ct) =>
                {
                    lock (observedDrainedFlags) observedDrainedFlags.Add(peer.ConsumerFullyDrained);
                    return Task.FromResult(stateRootB);
                },
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var result = await client.SyncStateWithCheckpointAsync(
                stateRootA, resumeFrom: null, checkpointSink: _ => { }, ct: cts.Token);

            var observed = Assert.Single(observedDrainedFlags);
            Assert.True(observed, "the old attempt's consumers must drain fully before the pivot catch-up runs");
            Assert.Equal(stateRootB, result.StateTrie.Root.GetHash(), ByteArrayComparer.Current);
        }

        private static Account BuildAccount(InMemoryContentNodeStore store, byte[] owner, int seed)
        {
            var keccak = new Sha3Keccack();
            var storageTrie = new PatriciaTrie(store, owner);
            var key = keccak.CalculateHash(new[] { (byte)seed });
            storageTrie.Put(key, new byte[] { (byte)seed, 0xCD });
            storageTrie.SaveDirtyNodesToStorage();
            var storageRoot = storageTrie.Root.GetHash();

            return new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1,
                StateRoot = storageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            };
        }

        private static byte[] BuildTwoOwnerStateRoot(
            InMemoryContentNodeStore store, byte[] ownerLow, int lowSeed, byte[] ownerHigh, int highSeed)
        {
            var encoder = new AccountEncoder();
            var stateTrie = new PatriciaTrie(store);
            stateTrie.Put(ownerLow, encoder.Encode(BuildAccount(store, ownerLow, lowSeed)));
            stateTrie.Put(ownerHigh, encoder.Encode(BuildAccount(store, ownerHigh, highSeed)));
            stateTrie.SaveDirtyNodesToStorage();
            return stateTrie.Root.GetHash();
        }

        private sealed class RangeSelectiveStallPeer : ISnapPeer
        {
            private readonly ISnapPeer _inner;
            private readonly TaskCompletionSource<bool> _lowRangeComplete;
            private readonly TaskCompletionSource<bool> _highRangeStalled;
            private int _lowRangeAccountCalls;
            private int _highRangeAccountCalls;

            public int LowRangeAccountCalls => Volatile.Read(ref _lowRangeAccountCalls);

            public RangeSelectiveStallPeer(
                ISnapPeer inner, TaskCompletionSource<bool> lowRangeComplete, TaskCompletionSource<bool> highRangeStalled)
            {
                _inner = inner;
                _lowRangeComplete = lowRangeComplete;
                _highRangeStalled = highRangeStalled;
            }

            public async Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
            {
                bool isHighRange = r.StartingHash != null && r.StartingHash.Length > 0 && r.StartingHash[0] >= 0x80;
                if (isHighRange)
                {
                    var call = Interlocked.Increment(ref _highRangeAccountCalls);
                    if (call == 1)
                    {
                        _highRangeStalled.TrySetResult(true);
                        await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                    }
                }
                else
                {
                    Interlocked.Increment(ref _lowRangeAccountCalls);
                }
                return await _inner.GetAccountRangeAsync(r, ct).ConfigureAwait(false);
            }

            public async Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
            {
                var resp = await _inner.GetStorageRangesAsync(r, ct).ConfigureAwait(false);
                _lowRangeComplete.TrySetResult(true);
                return resp;
            }

            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => _inner.GetByteCodesAsync(r, ct);
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => _inner.GetTrieNodesAsync(r, ct);
        }


        private sealed class SlowToDrainPeer : ISnapPeer
        {
            private readonly ISnapPeer _inner;
            private readonly TaskCompletionSource<bool> _moveGate;
            private readonly TimeSpan _drainDelay;
            private int _accountRangeCalls;
            public volatile bool ConsumerFullyDrained;

            public SlowToDrainPeer(ISnapPeer inner, TaskCompletionSource<bool> moveGate, TimeSpan drainDelay)
            {
                _inner = inner;
                _moveGate = moveGate;
                _drainDelay = drainDelay;
            }

            public async Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
            {
                var call = Interlocked.Increment(ref _accountRangeCalls);
                if (call == 1)
                {
                    _moveGate.TrySetResult(true);
                    try
                    {
                        await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                    }
                    finally
                    {
                        await Task.Delay(_drainDelay, CancellationToken.None).ConfigureAwait(false);
                        ConsumerFullyDrained = true;
                    }
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

    }
}
