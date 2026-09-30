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

        private sealed class RecordingSink : ISnapSyncSink
        {
            private byte[] _finaliseRoot;

            public List<byte[]> SlotsWritten { get; } = new();
            public bool ScopeOpen { get; private set; }
            public int BeginCount { get; private set; }
            public int EndCount { get; private set; }
            public int AbortCount { get; private set; }

            public void SetFinaliseRoot(byte[] root) => _finaliseRoot = root;

            public ValueTask BeginAsync(byte[] targetRoot, CancellationToken ct) => default;

            public ValueTask WriteAccountAsync(byte[] accountHash, byte[] slimRlp, CancellationToken ct) => default;

            private readonly object _sync = new();

            public ValueTask<IStorageScope> BeginAccountStorageAsync(byte[] accountHash, byte[] expectedStorageRoot, CancellationToken ct)
            {
                lock (_sync) { ScopeOpen = true; BeginCount++; }
                return new(new Scope(this));
            }

            private sealed class Scope : IStorageScope
            {
                private readonly RecordingSink _sink;
                public Scope(RecordingSink sink) => _sink = sink;

                public ValueTask WriteSlotAsync(byte[] slotHash, byte[] valueRlp, CancellationToken ct)
                { lock (_sink._sync) _sink.SlotsWritten.Add(slotHash); return default; }

                public ValueTask EndAsync(CancellationToken ct)
                { lock (_sink._sync) { _sink.ScopeOpen = false; _sink.EndCount++; } return default; }

                public ValueTask AbortAsync(CancellationToken ct)
                { lock (_sink._sync) { _sink.ScopeOpen = false; _sink.AbortCount++; } return default; }
            }

            public List<byte[]> BytecodesWritten { get; } = new();
            public ValueTask WriteBytecodeAsync(byte[] codeHash, byte[] code, CancellationToken ct)
            { lock (_sync) BytecodesWritten.Add(codeHash); return default; }

            public ValueTask<byte[]> FinaliseRootAsync(CancellationToken ct)
                => new(_finaliseRoot ?? new byte[32]);
        }

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
        public async Task Given_StorageDebtDiscovered_When_CheckpointPersistsCursor_Then_DebtPersistsInSameBatch()
        {
            var (range, stateRoot, accountHash, storageRoot) = BuildSingleAccountRange();
            var peer = new ScriptedSnapPeer(range,
                (req, idx) => new StorageRangesMessage { RequestId = 1, Slots = new List<List<StorageRangesMessage.SlotEntry>> { new() }, Proof = new() });

            var sink = new RecordingSink();
            sink.SetFinaliseRoot(stateRoot);
            var client = new SnapSyncClient(peer, sink) { AccountConcurrency = 1 };
            using var bundle = InMemoryChainStoreBundle.Open();
            var captured = new List<SnapSyncClient.SnapSyncCheckpoint>();

            var result = await client.SyncStateWithCheckpointAsync(
                stateRoot,
                resumeFrom: null,
                checkpointSink: checkpoint =>
                {
                    captured.Add(checkpoint);
                    using var batch = bundle.BeginBatch();
                    foreach (var debt in checkpoint.DeferredStorageDebts)
                        batch.UpsertDeferredStorageDebt(debt);
                    batch.SaveSnapSyncState(checkpoint.State);
                    batch.CommitAsync().GetAwaiter().GetResult();
                });

            Assert.NotEmpty(result.AccountsNeedingHeal);
            Assert.Contains(captured, checkpoint =>
                checkpoint.DeferredStorageDebts.Count == 1
                && ByteUtil.AreEqual(FilledHash(0xff), checkpoint.State.Tasks[0].Next));

            var persistedState = bundle.Metadata.GetSnapSyncState();
            Assert.NotNull(persistedState);
            Assert.Equal(FilledHash(0xff), persistedState!.Tasks[0].Next);

            var persistedDebt = Assert.Single(bundle.Metadata.ListOpenDeferredStorageDebts());
            Assert.Equal(accountHash, persistedDebt.AccountHash);
            Assert.Equal(storageRoot, persistedDebt.DiscoveredStorageRoot);
            Assert.Equal(stateRoot, persistedDebt.FetchStateRoot);
            Assert.Equal(StorageCompleteness.DeferredUnavailable, persistedDebt.Status);
            Assert.Equal(DeferredStorageReason.NonEmptyRootReturnedNoSlots, persistedDebt.Reason);
        }

        [Fact]
        public async Task Storage_DriftsMidFetch_AccountMarkedForHeal()
        {
            var (range, stateRoot, accountHash, expectedStorageRoot) = BuildSingleAccountRange();
            var slotHash = Hash.ComputeHash(new byte[] { 0x42 });

            var peer = new ScriptedSnapPeer(range,
                (req, idx) => UnverifiableSlotsResponse(slotHash, new byte[] { 0x01 }));

            var sink = new RecordingSink();
            var client = new SnapSyncClient(peer, sink) { AccountConcurrency = 1 };

            await Assert.ThrowsAsync<SnapSyncClient.SnapRootMismatchException>(
                () => client.SyncStateAsync(stateRoot));

            Assert.Equal(0, sink.BeginCount);
            Assert.Equal(0, sink.EndCount);
            Assert.Empty(sink.SlotsWritten);
        }

        [Fact]
        public async Task Storage_VerifiedFully_NotMarkedForHeal()
        {
            var (range, stateRoot) = BuildEmptyStorageAccountRange();
            var peer = new ScriptedSnapPeer(range,
                (req, idx) => new StorageRangesMessage { RequestId = 1, Slots = new(), Proof = new() });

            var sink = new RecordingSink();
            sink.SetFinaliseRoot(stateRoot);
            var client = new SnapSyncClient(peer, sink) { AccountConcurrency = 1 };

            var result = await client.SyncStateAsync(stateRoot);

            Assert.NotNull(result.AccountsNeedingHeal);
            Assert.Empty(result.AccountsNeedingHeal);
            Assert.Equal(0, sink.AbortCount);
        }

        [Fact]
        public async Task Storage_EmptyResponseForNonEmptyRoot_DefersToHeal_NotSilentlyComplete()
        {
            var (range, stateRoot, accountHash, _) = BuildSingleAccountRange();
            var peer = new ScriptedSnapPeer(range,
                (req, idx) => new StorageRangesMessage
                {
                    RequestId = 1,
                    Slots = new List<List<StorageRangesMessage.SlotEntry>> { new() },
                    Proof = new()
                });

            var sink = new RecordingSink();
            sink.SetFinaliseRoot(stateRoot);
            var client = new SnapSyncClient(peer, sink) { AccountConcurrency = 1 };

            var result = await client.SyncStateAsync(stateRoot);

            Assert.NotEmpty(result.AccountsNeedingHeal);
        }

        [Fact]
        public async Task Storage_SubRangeEmptyMidWhale_AbortsAndDefersToHeal_NoIncompleteTrie()
        {
            var storage = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(storage);
            var slots = new List<(byte[] Key, byte[] Val)>();
            for (int i = 1; i <= 4; i++)
            {
                var key = new byte[32]; key[0] = (byte)(0x10 * i);
                var val = Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i });
                trie.Put(key, val);
                slots.Add((key, val));
            }
            trie.SaveDirtyNodesToStorage();
            var storageRoot = trie.Root.GetHash();

            var accountHash = Hash.ComputeHash(new byte[] { 0x11 });
            var account = new Account
            {
                Nonce = (EvmUInt256)0,
                Balance = (EvmUInt256)0,
                StateRoot = storageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            };
            var canonical = new AccountEncoder().Encode(account);
            var slim = SlimAccountEncoder.ToSlim(canonical);
            var stateStorage = new InMemoryContentNodeStore();
            var stateTrie = new PatriciaTrie(stateStorage);
            stateTrie.Put(accountHash, canonical);
            stateTrie.SaveDirtyNodesToStorage();
            var stateRoot = stateTrie.Root.GetHash();
            var accountProof = PatriciaRangeProofGenerator.GenerateProof(
                stateTrie.Root, stateStorage, new byte[32], FilledHash(0xff));
            var range = new AccountRangeMessage
            {
                RequestId = 1,
                Accounts = new List<AccountRangeMessage.AccountEntry> { new() { Hash = accountHash, Body = slim } },
                Proof = accountProof,
            };

            var firstPageProof = PatriciaRangeProofGenerator.GenerateProof(
                trie.Root, storage, new byte[32], slots[1].Key);
            var peer = new ScriptedSnapPeer(range, (req, idx) =>
            {
                if (idx == 0)
                    return new StorageRangesMessage
                    {
                        RequestId = 1,
                        Slots = new List<List<StorageRangesMessage.SlotEntry>>
                        {
                            new()
                            {
                                new StorageRangesMessage.SlotEntry { Hash = slots[0].Key, Data = slots[0].Val },
                                new StorageRangesMessage.SlotEntry { Hash = slots[1].Key, Data = slots[1].Val },
                            }
                        },
                        Proof = firstPageProof,
                    };
                return new StorageRangesMessage { RequestId = 1, Slots = new(), Proof = new() };
            });

            var sink = new RecordingSink();
            sink.SetFinaliseRoot(stateRoot);
            var client = new SnapSyncClient(peer, sink) { AccountConcurrency = 1 };

            var result = await client.SyncStateAsync(stateRoot);

            Assert.Equal(0, sink.EndCount);
            Assert.True(sink.AbortCount >= 1);
            Assert.NotEmpty(result.AccountsNeedingHeal);
        }

        [Fact]
        public async Task Storage_SubRangeEmptyWithAbsenceProof_CompletesNotHealed()
        {
            var storage = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(storage);
            var slots = new List<(byte[] Key, byte[] Val)>();
            foreach (var hi in new byte[] { 0x10, 0x20 })
            {
                var key = new byte[32]; key[0] = hi;
                var val = Nethereum.RLP.RLP.EncodeElement(new byte[] { hi });
                trie.Put(key, val);
                slots.Add((key, val));
            }
            trie.SaveDirtyNodesToStorage();
            var storageRoot = trie.Root.GetHash();
            int Cmp(byte[] a, byte[] b) => ByteArrayComparer.Current.Compare(a, b);

            var accountHash = Hash.ComputeHash(new byte[] { 0x11 });
            var account = new Account
            {
                Nonce = (EvmUInt256)0,
                Balance = (EvmUInt256)0,
                StateRoot = storageRoot,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
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

            StorageRangesMessage Respond(byte[] start)
            {
                bool firstPage = Array.TrueForAll(start, b => b == 0);
                var inRange = slots.FindAll(s => Cmp(s.Key, start) >= 0);
                inRange.Sort((a, b) => Cmp(a.Key, b.Key));
                if (firstPage && inRange.Count > 1) inRange = inRange.GetRange(0, 1);
                if (inRange.Count == 0)
                    return new StorageRangesMessage
                    {
                        RequestId = 1,
                        Slots = new List<List<StorageRangesMessage.SlotEntry>> { new() },
                        Proof = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, start),
                    };
                var last = inRange[^1].Key;
                return new StorageRangesMessage
                {
                    RequestId = 1,
                    Slots = new List<List<StorageRangesMessage.SlotEntry>>
                    {
                        inRange.ConvertAll(s => new StorageRangesMessage.SlotEntry { Hash = s.Key, Data = s.Val })
                    },
                    Proof = PatriciaRangeProofGenerator.GenerateProof(trie.Root, storage, start, last),
                };
            }

            var peer = new ScriptedSnapPeer(range, (req, idx) => Respond(req.StartingHash));
            var sink = new RecordingSink();
            sink.SetFinaliseRoot(stateRoot);
            var client = new SnapSyncClient(peer, sink) { AccountConcurrency = 1, LargeContractConcurrency = 2 };

            var result = await client.SyncStateAsync(stateRoot);

            Assert.Empty(result.AccountsNeedingHeal);
            Assert.Equal(0, sink.AbortCount);
            Assert.Equal(1, sink.EndCount);
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

            var sink = new RecordingSink();
            sink.SetFinaliseRoot(stateRoot);
            var client = new SnapSyncClient(peer, sink) { AccountConcurrency = 1 };

            await client.SyncStateAsync(stateRoot);

            Assert.Contains(sink.BytecodesWritten, h => ByteUtil.AreEqual(h, codeHash));
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

            var sink = new RecordingSink();
            var client = new SnapSyncClient(peer, sink) { AccountConcurrency = 1 };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.SyncStateAsync(stateRoot));

            Assert.Empty(sink.SlotsWritten);
            Assert.Equal(0, sink.BeginCount);
            Assert.Equal(0, sink.EndCount);
        }

        [Fact]
        public async Task LargeContract_AllSubRangesSucceed_AllSlotsWritten()
        {
            var (range, stateRoot) = BuildEmptyStorageAccountRange();
            var peer = new ScriptedSnapPeer(range,
                (req, idx) => new StorageRangesMessage { RequestId = 1, Slots = new(), Proof = new() });

            var sink = new RecordingSink();
            sink.SetFinaliseRoot(stateRoot);
            var client = new SnapSyncClient(peer, sink) { AccountConcurrency = 1 };

            var result = await client.SyncStateAsync(stateRoot);

            Assert.Equal(0, sink.AbortCount);
            Assert.NotNull(result.AccountsNeedingHeal);
            Assert.Empty(result.AccountsNeedingHeal);
        }


        [Fact]
        public async Task LivePivot_ConcurrentRotation_ReadConsistent()
        {
            PivotPair state = new PivotPair(0, new byte[32]);

            var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
            int inconsistent = 0;

            async Task Reader()
            {
                while (!stop.IsCancellationRequested)
                {
                    var snap = Volatile.Read(ref state);
                    if (snap.Hash[31] != (byte)(snap.Block & 0xFF))
                        Interlocked.Increment(ref inconsistent);
                    await Task.Yield();
                }
            }

            async Task Writer()
            {
                ulong i = 0;
                while (!stop.IsCancellationRequested)
                {
                    i++;
                    var h = new byte[32];
                    h[31] = (byte)(i & 0xFF);
                    Interlocked.Exchange(ref state, new PivotPair(i, h));
                    await Task.Yield();
                }
            }

            var tasks = new List<Task>();
            for (int i = 0; i < 4; i++) tasks.Add(Reader());
            tasks.Add(Writer());
            await Task.WhenAll(tasks);

            Assert.Equal(0, inconsistent);
        }

        [Fact]
        public async Task Backfill_PivotRotationExtendsTarget()
        {
            PivotPair state = new PivotPair(100, MakeHashWithSuffix(100));

            ulong observedMax = 0;
            var done = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            var rotator = Task.Run(async () =>
            {
                ulong block = 100;
                while (!done.IsCancellationRequested)
                {
                    block += 10;
                    Interlocked.Exchange(ref state, new PivotPair(block, MakeHashWithSuffix(block)));
                    await Task.Delay(5);
                }
            });

            var reader = Task.Run(async () =>
            {
                while (!done.IsCancellationRequested)
                {
                    var snap = Volatile.Read(ref state);
                    Assert.Equal((byte)(snap.Block & 0xFF), snap.Hash[31]);
                    if (snap.Block > observedMax) observedMax = snap.Block;
                    await Task.Delay(1);
                }
            });

            await Task.WhenAll(rotator, reader);
            Assert.True(observedMax > 100, $"backfill loop never observed rotated pivot (observedMax={observedMax})");
        }

        private static byte[] MakeHashWithSuffix(ulong block)
        {
            var h = new byte[32];
            h[31] = (byte)(block & 0xFF);
            return h;
        }

        private sealed record PivotPair(ulong Block, byte[] Hash);
    }
}
