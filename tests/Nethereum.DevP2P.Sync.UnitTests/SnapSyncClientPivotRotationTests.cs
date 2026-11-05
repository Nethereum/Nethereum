using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapSyncClientPivotRotationTests
    {
        private static readonly Sha3KeccackHashProvider Hash = new();

        private sealed class ScriptedSnapPeer : ISnapPeer
        {
            private readonly AccountRangeMessage _accountRange;
            private readonly Func<GetStorageRangesMessage, int, StorageRangesMessage> _storageResponder;
            private readonly Func<GetByteCodesMessage, int, ByteCodesMessage> _byteCodeResponder;
            private int _storageCallCount;
            private int _byteCodeCallCount;

            public ScriptedSnapPeer(
                AccountRangeMessage accountRange,
                Func<GetStorageRangesMessage, int, StorageRangesMessage> storageResponder,
                Func<GetByteCodesMessage, int, ByteCodesMessage> byteCodeResponder = null)
            {
                _accountRange = accountRange;
                _storageResponder = storageResponder;
                _byteCodeResponder = byteCodeResponder;
            }

            public int StorageCallCount => _storageCallCount;
            public int ByteCodeCallCount => _byteCodeCallCount;

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
                => Task.FromResult(_accountRange);

            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
            {
                var idx = Interlocked.Increment(ref _storageCallCount) - 1;
                return Task.FromResult(_storageResponder(r, idx));
            }

            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
            {
                var idx = Interlocked.Increment(ref _byteCodeCallCount) - 1;
                if (_byteCodeResponder != null) return Task.FromResult(_byteCodeResponder(r, idx));
                return Task.FromResult(new ByteCodesMessage { RequestId = r.RequestId, Codes = new List<byte[]>() });
            }

            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => Task.FromResult(new TrieNodesMessage { RequestId = r.RequestId, Nodes = new List<byte[]>() });
        }

        private static (AccountRangeMessage Range, byte[] StateRoot, byte[] AccountHash, byte[] StorageRoot) BuildSingleAccountRange()
        {
            var storageRoot = Hash.ComputeHash(new byte[] { 0xAB, 0xCD });
            var addrBytes = new byte[] { 0x11 };
            var accountHash = Hash.ComputeHash(addrBytes);

            var account = new Account
            {
                Nonce = (EvmUInt256)0,
                Balance = (EvmUInt256)0,
                StateRoot = storageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            };
            var canonical = new AccountEncoder().Encode(account);
            var slim = SlimAccountEncoder.ToSlim(canonical);

            var storage = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(storage);
            trie.Put(accountHash, canonical);
            trie.SaveDirtyNodesToStorage();
            var stateRoot = trie.Root.GetHash();

            var proof = PatriciaRangeProofGenerator.GenerateProof(
                trie.Root, storage, new byte[32], FilledHash(0xff));

            var range = new AccountRangeMessage
            {
                RequestId = 1,
                Accounts = new List<AccountRangeMessage.AccountEntry>
                {
                    new() { Hash = accountHash, Body = slim }
                },
                Proof = proof,
            };

            return (range, stateRoot, accountHash, storageRoot);
        }

        private static (AccountRangeMessage Range, byte[] StateRoot) BuildEmptyStorageAccountRange()
        {
            var accountHash = Hash.ComputeHash(new byte[] { 0x22 });
            var account = new Account
            {
                Nonce = (EvmUInt256)0,
                Balance = (EvmUInt256)0,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            };
            var canonical = new AccountEncoder().Encode(account);
            var slim = SlimAccountEncoder.ToSlim(canonical);

            var storage = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(storage);
            trie.Put(accountHash, canonical);
            trie.SaveDirtyNodesToStorage();
            var stateRoot = trie.Root.GetHash();

            var proof = PatriciaRangeProofGenerator.GenerateProof(
                trie.Root, storage, new byte[32], FilledHash(0xff));

            var range = new AccountRangeMessage
            {
                RequestId = 1,
                Accounts = new List<AccountRangeMessage.AccountEntry>
                {
                    new() { Hash = accountHash, Body = slim }
                },
                Proof = proof,
            };
            return (range, stateRoot);
        }

        private static byte[] FilledHash(byte b)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = b;
            return h;
        }

        private static StorageRangesMessage UnverifiableSlotsResponse(byte[] slotHash, byte[] value)
        {
            return new StorageRangesMessage
            {
                RequestId = 1,
                Slots = new List<List<StorageRangesMessage.SlotEntry>>
                {
                    new() { new StorageRangesMessage.SlotEntry { Hash = slotHash, Data = value } }
                },
                Proof = new List<byte[]>(),
            };
        }


        [Fact]
        public async Task Given_AChunkedOwnerWhosePagesStraddleAPivotMove_When_SyncStateWithCheckpointAsyncCompletes_Then_TheResultCarriesTheOwnerNeedingHeal()
        {
            const int total = 40;
            var store = new InMemoryContentNodeStore();
            var owner = Hash.ComputeHash(new byte[] { 0x56 });
            var (stateRootA, _, _) = BuildOwnerState(store, owner, total, seed: 90);
            var (stateRootB, _, _) = BuildOwnerState(store, owner, total, seed: 100);
            var handler = new Nethereum.DevP2P.Sync.Serving.PatriciaSnapRequestHandler(store, new NoCodeStore());
            var moved = 0;
            var peer = new MovingRootSnapPeer(new InProcessSnapPeer(handler), () => Volatile.Write(ref moved, 1));
            var recorder = new RecordingSnapSink();
            var client = new SnapSyncClient(peer, recorder.Sink, responseBytesBudget: 240UL)
            {
                AccountConcurrency = 1,
                LargeContractConcurrency = 1,
                RootRefreshIntervalMs = 10,
                PivotRefresher = _ => Task.FromResult(Volatile.Read(ref moved) == 0 ? stateRootA : stateRootB),
            };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            var result = await client.SyncStateWithCheckpointAsync(stateRootA, resumeFrom: null, checkpointSink: _ => { }, ct: cts.Token);

            var healed = Assert.Single(result.AccountsNeedingHeal);
            Assert.Equal(owner, healed.AccountHash);
            Assert.Equal(stateRootB, result.FinalTargetRoot);
            Assert.True(result.RootMatchesTarget);
        }

        private sealed class MovingRootSnapPeer : ISnapPeer
        {
            private readonly ISnapPeer _inner;
            private readonly Action _onMovingStoragePage;
            private int _storageCalls;

            public MovingRootSnapPeer(ISnapPeer inner, Action onMovingStoragePage)
            {
                _inner = inner;
                _onMovingStoragePage = onMovingStoragePage;
            }

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
                => _inner.GetAccountRangeAsync(r, ct);

            public async Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
            {
                if (Interlocked.Increment(ref _storageCalls) == 3)
                {
                    _onMovingStoragePage();
                    await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                }
                return await _inner.GetStorageRangesAsync(r, ct).ConfigureAwait(false);
            }

            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => _inner.GetByteCodesAsync(r, ct);

            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => _inner.GetTrieNodesAsync(r, ct);
        }

        private static (byte[] StateRoot, byte[] StorageRoot, List<(byte[] Key, byte[] Value)> Entries) BuildOwnerState(
            InMemoryContentNodeStore store, byte[] owner, int count, int seed)
        {
            var storageTrie = new PatriciaTrie(store, owner);
            var entries = new List<(byte[] Key, byte[] Value)>();
            for (int i = 0; i < count; i++)
            {
                var key = Hash.ComputeHash(new[] { (byte)((seed >> 8) & 0xff), (byte)(seed & 0xff), (byte)(i >> 8), (byte)(i & 0xff) });
                var value = Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)(i & 0xff), (byte)((i >> 4) & 0xff), 0xCD });
                entries.Add((key, value));
                storageTrie.Put(key, value);
            }
            storageTrie.SaveDirtyNodesToStorage();
            var storageRoot = storageTrie.Root.GetHash();
            var stateTrie = new PatriciaTrie(store);
            stateTrie.Put(owner, new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)1,
                StateRoot = storageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            }));
            stateTrie.SaveDirtyNodesToStorage();
            return (stateTrie.Root.GetHash(), storageRoot, entries);
        }

        [Fact]
        public async Task Storage_DriftingDirectPeer_IsRejectedByTheVerifierThenServed_NothingWrittenForTheRejectedRepliesAndNoNeedHeal()
        {
            const int rejectedReplies = 3;
            const int slotCount = 12;
            var server = new InMemoryContentNodeStore();
            var accountHash = Hash.ComputeHash(new byte[] { 0x33 });
            var storageTrie = new PatriciaTrie(server);
            for (int i = 0; i < slotCount; i++)
                storageTrie.Put(Hash.ComputeHash(new byte[] { 0x44, (byte)i }), Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)(i + 1) }));
            storageTrie.SaveDirtyNodesToStorage();
            var stateTrie = new PatriciaTrie(server);
            stateTrie.Put(accountHash, new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)0,
                Balance = (EvmUInt256)0,
                StateRoot = storageTrie.Root.GetHash(),
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            }));
            stateTrie.SaveDirtyNodesToStorage();
            var stateRoot = stateTrie.Root.GetHash();
            var handler = new Nethereum.DevP2P.Sync.Serving.PatriciaSnapRequestHandler(server, new NoCodeStore());
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var range = await handler.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RequestId = 1, RootHash = stateRoot, StartingHash = new byte[32], LimitHash = FilledHash(0xff), ResponseBytes = 524_288UL,
            }, cts.Token);
            var driftedSlot = Hash.ComputeHash(new byte[] { 0x42 });
            var peer = new ScriptedSnapPeer(range, (req, idx) => idx < rejectedReplies
                ? UnverifiableSlotsResponse(driftedSlot, new byte[] { 0x01 })
                : handler.GetStorageRangesAsync(req, cts.Token).GetAwaiter().GetResult());
            var recorder = new RecordingSnapSink();
            var client = new SnapSyncClient(peer, recorder.Sink) { AccountConcurrency = 1 };

            var result = await client.SyncStateAsync(stateRoot, cts.Token);

            Assert.True(peer.StorageCallCount > rejectedReplies, $"storage calls={peer.StorageCallCount}");
            Assert.Equal(slotCount, recorder.Flat.SlotsWritten.Count);
            Assert.DoesNotContain(recorder.Flat.SlotsWritten, w => ByteUtil.AreEqual(w.Slot, driftedSlot));
            Assert.Empty(result.AccountsNeedingHeal);
        }

        [Fact]
        public async Task Storage_VerifiedFully_NotMarkedForHeal()
        {
            var (range, stateRoot) = BuildEmptyStorageAccountRange();
            var peer = new ScriptedSnapPeer(range,
                (req, idx) => new StorageRangesMessage { RequestId = 1, Slots = new(), Proof = new() });

            var recorder = new RecordingSnapSink();
            var client = new SnapSyncClient(peer, recorder.Sink) { AccountConcurrency = 1 };

            var result = await client.SyncStateAsync(stateRoot);

            Assert.NotNull(result.AccountsNeedingHeal);
            Assert.Empty(result.AccountsNeedingHeal);
        }

        [Fact]
        public async Task Storage_EmptyResponseForNonEmptyRoot_IsRejectedAndRetriedUntilServed()
        {
            const int emptyReplies = 2;
            var server = new InMemoryContentNodeStore();
            var accountHash = Hash.ComputeHash(new byte[] { 0x33 });
            var storageTrie = new PatriciaTrie(server);
            var slotCount = 12;
            for (int i = 0; i < slotCount; i++)
                storageTrie.Put(Hash.ComputeHash(new byte[] { 0x44, (byte)i }), Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)(i + 1) }));
            storageTrie.SaveDirtyNodesToStorage();
            var stateTrie = new PatriciaTrie(server);
            stateTrie.Put(accountHash, new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)0,
                Balance = (EvmUInt256)0,
                StateRoot = storageTrie.Root.GetHash(),
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            }));
            stateTrie.SaveDirtyNodesToStorage();
            var stateRoot = stateTrie.Root.GetHash();
            var handler = new Nethereum.DevP2P.Sync.Serving.PatriciaSnapRequestHandler(server, new NoCodeStore());
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var range = await handler.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RequestId = 1, RootHash = stateRoot, StartingHash = new byte[32], LimitHash = FilledHash(0xff), ResponseBytes = 524_288UL,
            }, cts.Token);
            var peer = new ScriptedSnapPeer(range, (req, idx) => idx < emptyReplies
                ? new StorageRangesMessage { RequestId = req.RequestId, Slots = new List<List<StorageRangesMessage.SlotEntry>> { new() }, Proof = new() }
                : handler.GetStorageRangesAsync(req, cts.Token).GetAwaiter().GetResult());
            var recorder = new RecordingSnapSink();
            var client = new SnapSyncClient(peer, recorder.Sink) { AccountConcurrency = 1 };

            var result = await client.SyncStateAsync(stateRoot, cts.Token);

            Assert.True(peer.StorageCallCount > emptyReplies, $"storage calls={peer.StorageCallCount}");
            Assert.Equal(slotCount, recorder.Flat.SlotsWritten.Count);
            Assert.Empty(result.AccountsNeedingHeal);
        }

        private sealed class NoCodeStore : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        [Fact]
        public async Task Bytecode_TruncatedResponse_RetriedUntilServed_NotDropped()
        {
            var code = new byte[] { 0x60, 0x00, 0x60, 0x00, 0xF3 };
            var codeHash = Hash.ComputeHash(code);

            var accountHash = Hash.ComputeHash(new byte[] { 0x55 });
            var account = new Account
            {
                Nonce = (EvmUInt256)0,
                Balance = (EvmUInt256)0,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = codeHash,
            };
            var slim = SlimAccountEncoder.ToSlim(new AccountEncoder().Encode(account));
            var stateStorage = new InMemoryContentNodeStore();
            var stateTrie = new PatriciaTrie(stateStorage);
            stateTrie.Put(accountHash, new AccountEncoder().Encode(account));
            stateTrie.SaveDirtyNodesToStorage();
            var stateRoot = stateTrie.Root.GetHash();
            var range = new AccountRangeMessage
            {
                RequestId = 1,
                Accounts = new List<AccountRangeMessage.AccountEntry> { new() { Hash = accountHash, Body = slim } },
                Proof = PatriciaRangeProofGenerator.GenerateProof(stateTrie.Root, stateStorage, new byte[32], FilledHash(0xff)),
            };

            var peer = new ScriptedSnapPeer(range,
                (req, idx) => new StorageRangesMessage { RequestId = 1, Slots = new(), Proof = new() },
                (req, idx) => idx == 0
                    ? new ByteCodesMessage { RequestId = req.RequestId, Codes = new List<byte[]>() }
                    : new ByteCodesMessage { RequestId = req.RequestId, Codes = new List<byte[]> { code } });

            var recorder = new RecordingSnapSink();
            var client = new SnapSyncClient(peer, recorder.Sink) { AccountConcurrency = 1 };

            await client.SyncStateAsync(stateRoot);

            Assert.Equal(code, await recorder.State.GetCodeAsync(codeHash));
            Assert.True(peer.ByteCodeCallCount >= 2, $"truncated code must be retried; calls={peer.ByteCodeCallCount}");
        }


        [Fact]
        public async Task LargeContract_SubRangeFails_PartialWritesRolledBack()
        {
            var (range, stateRoot, _, _) = BuildSingleAccountRange();
            var peer = new ScriptedSnapPeer(range, (req, idx) =>
            {
                throw new InvalidOperationException("simulated peer disconnect");
            });

            var recorder = new RecordingSnapSink();
            var client = new SnapSyncClient(peer, recorder.Sink) { AccountConcurrency = 1 };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.SyncStateAsync(stateRoot));

            Assert.Empty(recorder.Flat.SlotsWritten);
        }

        [Fact]
        public async Task LargeContract_AllSubRangesSucceed_AllSlotsWritten()
        {
            var (range, stateRoot) = BuildEmptyStorageAccountRange();
            var peer = new ScriptedSnapPeer(range,
                (req, idx) => new StorageRangesMessage { RequestId = 1, Slots = new(), Proof = new() });

            var recorder = new RecordingSnapSink();
            var client = new SnapSyncClient(peer, recorder.Sink) { AccountConcurrency = 1 };

            var result = await client.SyncStateAsync(stateRoot);

            Assert.NotNull(result.AccountsNeedingHeal);
            Assert.Empty(result.AccountsNeedingHeal);
        }
    }
}
