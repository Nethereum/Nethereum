using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.CatchUp;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class WhaleStorageFlatWriteTests
    {
        private static readonly byte[] Owner = OwnerHash(0x42);

        private static byte[] OwnerHash(byte marker)
        {
            var o = new byte[32];
            o[0] = marker;
            o[31] = 0x11;
            return o;
        }

        private static (PatriciaTrie Trie, InMemoryContentNodeStore Storage, List<(byte[] Key, byte[] Rlp, byte[] Raw)> Entries)
            BuildStorageOracle(int count)
        {
            var keccak = new Sha3Keccack();
            var storage = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(storage);
            var entries = new List<(byte[] Key, byte[] Rlp, byte[] Raw)>();
            for (int i = 0; i < count; i++)
            {
                var key = keccak.CalculateHash(new[] { (byte)0x77, (byte)(i >> 8), (byte)(i & 0xff) });
                var raw = new byte[] { (byte)(i + 1), 0xCD, (byte)(i & 0x0f) };
                var rlp = Nethereum.RLP.RLP.EncodeElement(raw);
                entries.Add((key, rlp, raw));
                trie.Put(key, rlp);
            }
            trie.SaveDirtyNodesToStorage();
            entries.Sort((a, b) => ByteArrayComparer.Current.Compare(a.Key, b.Key));
            return (trie, storage, entries);
        }

        private static (List<byte[]> Keys, List<byte[]> Values, List<byte[]> Proof) Page(
            PatriciaTrie oracle, InMemoryContentNodeStore storage, List<(byte[] Key, byte[] Rlp, byte[] Raw)> entries, int from, int to)
        {
            var slice = entries.Skip(from).Take(to - from + 1).ToList();
            var keys = slice.Select(e => e.Key).ToList();
            var values = slice.Select(e => e.Rlp).ToList();
            var proof = PatriciaRangeProofGenerator.GenerateProof(oracle.Root, storage, keys[0], keys[^1]);
            return (keys, values, proof);
        }

        [Fact]
        public void Given_AVerifiedWhalePage_When_AppliedToAScopeWithAFlatWriter_Then_EverySlotIsWrittenToFlatAsItsRawValue()
        {
            var (oracle, oracleStorage, entries) = BuildStorageOracle(30);
            var flat = new RecordingFlatWriter();
            var scope = ResumableStorageScope.Open(new InMemoryContentNodeStore(), Owner, flatWriter: flat);

            var (keys, values, proof) = Page(oracle, oracleStorage, entries, 4, 17);
            var result = scope.ApplyVerifiedPage(oracle.Root.GetHash(), keys[0], keys, values, proof);

            Assert.True(result.Accepted);
            Assert.Equal(keys.Count, flat.Storage.Count);
            foreach (var (key, _, raw) in entries.Skip(4).Take(14))
                Assert.Equal(raw.ToHex(), flat.StorageOf(Owner, key).ToHex());
        }

        [Fact]
        public void Given_APageThatFailsItsProof_When_Applied_Then_NoFlatRowIsWritten()
        {
            var (oracle, oracleStorage, entries) = BuildStorageOracle(30);
            var flat = new RecordingFlatWriter();
            var scope = ResumableStorageScope.Open(new InMemoryContentNodeStore(), Owner, flatWriter: flat);

            var (keys, values, proof) = Page(oracle, oracleStorage, entries, 4, 17);
            values[2] = Nethereum.RLP.RLP.EncodeElement(new byte[] { 0xEE, 0xEE });
            var result = scope.ApplyVerifiedPage(oracle.Root.GetHash(), keys[0], keys, values, proof);

            Assert.False(result.Accepted);
            Assert.Empty(flat.Storage);
        }

        [Fact]
        public async Task Given_ACursoredWhaleDrainedByTheClient_When_TheSinkHasAFlatWriter_Then_EverySlotReachesFlat()
        {
            const int total = 25;
            const int pageSize = 8;
            var (oracle, oracleStorage, entries) = BuildStorageOracle(total);
            var storageRoot = oracle.Root.GetHash();

            var set = new SnapTaskSet(1) { LargeContractConcurrency = 1 };
            PromoteWhale(set, Owner, storageRoot);

            StorageRangesMessage Respond(byte[] start)
            {
                var page = entries.Where(e => ByteArrayComparer.Current.Compare(e.Key, start) >= 0).Take(pageSize).ToList();
                return new StorageRangesMessage
                {
                    RequestId = 0,
                    Slots = new List<List<StorageRangesMessage.SlotEntry>>
                    {
                        page.Select(e => new StorageRangesMessage.SlotEntry { Hash = e.Key, Data = e.Rlp }).ToList()
                    },
                    Proof = PatriciaRangeProofGenerator.GenerateProof(oracle.Root, oracleStorage, start, page[^1].Key),
                };
            }

            var flat = new RecordingFlatWriter();
            var trieStore = new InMemoryContentNodeStore();
            var sink = new TrieSnapSyncSink(trieStore, new InMemoryStateStore(), flat);
            var client = new SnapSyncClient(new PagingSnapPeer(Respond), sink);

            var rounds = 0;
            SnapFragment.StorageSubtask lease;
            while ((lease = (SnapFragment.StorageSubtask)set.LeaseNext()) != null)
            {
                Assert.True(++rounds <= total, "subtask never drained");
                await client.ProcessStorageSubtaskAsync(
                    lease, set, trieStore, storageRoot,
                    new ConcurrentBag<SnapSyncClient.AccountNeedingHeal>(),
                    new ConcurrentDictionary<string, DeferredStorageDebt>(), null, CancellationToken.None);
            }

            Assert.True(rounds > 1, "the whale must span several pages for this test to be meaningful");
            Assert.True(set.AllDone);
            Assert.Equal(total, flat.Storage.Count);
            foreach (var (key, _, raw) in entries)
                Assert.Equal(raw.ToHex(), flat.StorageOf(Owner, key).ToHex());
        }

        [Fact]
        public async Task Given_ABufferedFlatRowForASlot_When_APivotMoveCatchUpRuns_Then_TheSinkIsFlushedBeforeCatchUpAndTheBalValueWins()
        {
            var (durable, slot) = await RunPivotMoveWithABufferedSlotAsync(flushBeforeCheckpoint: true);

            Assert.Equal(BalValue.ToHex(), durable.StorageOf(LowOwner, slot).ToHex());
        }

        [Fact]
        public async Task Given_ABufferedFlatRowForASlot_When_APivotMoveCatchUpRunsWithoutAFlushBeforeIt_Then_TheStaleBufferedRowWins()
        {
            var (durable, slot) = await RunPivotMoveWithABufferedSlotAsync(flushBeforeCheckpoint: false);

            Assert.Equal(DownloadedValue.ToHex(), durable.StorageOf(LowOwner, slot).ToHex());
        }

        private static readonly byte[] LowOwner = Enumerable.Repeat((byte)0x10, 32).ToArray();
        private static readonly byte[] HighOwner = Enumerable.Repeat((byte)0x90, 32).ToArray();
        private static readonly byte[] DownloadedValue = { 0x0D };
        private static readonly byte[] BalValue = { 0x0B };

        private static async Task<(RecordingFlatWriter Durable, byte[] Slot)> RunPivotMoveWithABufferedSlotAsync(bool flushBeforeCheckpoint)
        {
            var store = new InMemoryContentNodeStore();
            var slot = Sha3Keccack.Current.CalculateHash(new byte[] { 0x01 });
            var storageTrie = new PatriciaTrie(store, LowOwner);
            storageTrie.Put(slot, Nethereum.RLP.RLP.EncodeElement(DownloadedValue));
            storageTrie.SaveDirtyNodesToStorage();
            byte[] Root(int highBalance)
            {
                var trie = new PatriciaTrie(store);
                trie.Put(LowOwner, new AccountEncoder().Encode(new Account
                {
                    Nonce = (EvmUInt256)1, Balance = (EvmUInt256)1,
                    StateRoot = storageTrie.Root.GetHash(), CodeHash = DefaultValues.EMPTY_DATA_HASH,
                }));
                trie.Put(HighOwner, new AccountEncoder().Encode(new Account
                {
                    Nonce = (EvmUInt256)1, Balance = (EvmUInt256)highBalance,
                    StateRoot = DefaultValues.EMPTY_TRIE_HASH, CodeHash = DefaultValues.EMPTY_DATA_HASH,
                }));
                trie.SaveDirtyNodesToStorage();
                return trie.Root.GetHash();
            }
            var rootA = Root(1);
            var rootB = Root(2);

            var durable = new RecordingFlatWriter();
            var buffered = new BufferingFlatWriter(durable);
            var peer = new HighRangeHeldAtRootPeer(
                new InProcessSnapPeer(new Nethereum.DevP2P.Sync.Serving.PatriciaSnapRequestHandler(store, new HeadStateLoader.BytecodeStore())), rootA);
            var client = new SnapSyncClient(peer, new TrieSnapSyncSink(new InMemoryContentNodeStore(), new InMemoryStateStore(), buffered))
            {
                AccountConcurrency = 1,
                RootRefreshIntervalMs = 15,
                PivotRefresher = _ => Task.FromResult(peer.Held.Task.IsCompleted ? rootB : rootA),
                PivotCatchUp = async (tasks, ct) =>
                {
                    await durable.SaveStorageByHashAsync(LowOwner, slot, BalValue);
                    return rootB;
                },
            };
            if (flushBeforeCheckpoint)
                client.FlushBulkFlatBeforeCheckpoint = buffered.Flush;

            var resumeFrom = new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = SnapPhase.Phase2Running,
                PivotBlockNumber = 0,
                PivotBlockHash = new byte[32],
                HealTargetRoot = new byte[32],
                Counters = SnapSyncCounters.Zero,
                Tasks = new List<SnapSyncAccountTask> { Chunk(0x00, 0x7f), Chunk(0x80, 0xff) },
            };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await client.SyncStateWithCheckpointAsync(rootA, resumeFrom, _ => { }, cts.Token);
            buffered.Flush();

            Assert.True(peer.Held.Task.IsCompleted, "the pivot never moved, so the catch-up ordering is unobserved");
            return (durable, slot);
        }

        private static SnapSyncAccountTask Chunk(byte first, byte last) => new SnapSyncAccountTask
        {
            Next = Enumerable.Range(0, 32).Select(i => i == 0 ? first : (byte)0).ToArray(),
            Last = Enumerable.Range(0, 32).Select(i => i == 0 ? last : (byte)0xff).ToArray(),
            StorageCompleted = Array.Empty<byte[]>(),
            SubTasks = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(ByteArrayComparer.Current),
        };

        private sealed class HighRangeHeldAtRootPeer : ISnapPeer
        {
            private readonly ISnapPeer _inner;
            private readonly byte[] _root;

            public HighRangeHeldAtRootPeer(ISnapPeer inner, byte[] root)
            {
                _inner = inner;
                _root = root;
            }

            public TaskCompletionSource<bool> Held { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public async Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
            {
                if (ByteUtil.AreEqual(r.RootHash, _root) && r.StartingHash is { Length: > 0 } && r.StartingHash[0] >= 0x80)
                {
                    Held.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                }
                return await _inner.GetAccountRangeAsync(r, ct).ConfigureAwait(false);
            }

            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default) => _inner.GetStorageRangesAsync(r, ct);
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default) => _inner.GetByteCodesAsync(r, ct);
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default) => _inner.GetTrieNodesAsync(r, ct);
        }

        private sealed class BufferingFlatWriter : ISnapFlatStateWriter
        {
            private readonly ISnapFlatStateWriter _durable;
            private readonly List<Func<Task>> _pending = new();

            public BufferingFlatWriter(ISnapFlatStateWriter durable) => _durable = durable;

            public void Flush()
            {
                List<Func<Task>> pending;
                lock (_pending)
                {
                    pending = _pending.ToList();
                    _pending.Clear();
                }
                foreach (var write in pending) write().GetAwaiter().GetResult();
            }

            public Task<Account> GetAccountByHashAsync(byte[] accountHash)
                => throw new NotSupportedException("write-only, like the SST bulk sink");

            public Task SaveAccountByHashAsync(byte[] accountHash, Account account) => Buffer(() => _durable.SaveAccountByHashAsync(accountHash, account));

            public Task DeleteAccountByHashAsync(byte[] accountHash) => Buffer(() => _durable.DeleteAccountByHashAsync(accountHash));

            public Task SaveStorageByHashAsync(byte[] accountHash, byte[] slotKeccak, byte[] value) => Buffer(() => _durable.SaveStorageByHashAsync(accountHash, slotKeccak, value));

            private Task Buffer(Func<Task> write)
            {
                lock (_pending) _pending.Add(write);
                return Task.CompletedTask;
            }
        }

        private static void PromoteWhale(SnapTaskSet set, byte[] account, byte[] storageRoot)
        {
            var frag = (SnapFragment.AccountRange)set.LeaseNext();
            set.CompleteAccountRange(frag,
                new List<AccountClassification> { new(account, storageRoot, DefaultValues.EMPTY_DATA_HASH) },
                lastHash: account, done: true);
            var batch = (SnapFragment.SmallStorageBatch)set.LeaseNext();
            set.CompleteSmallBatch(batch,
                new List<SmallStorageOutcome> { new(account, SmallStorageResult.Large, new byte[32]) }, storageRoot);
        }

        private sealed class PagingSnapPeer : ISnapPeer
        {
            private readonly Func<byte[], StorageRangesMessage> _respond;
            public PagingSnapPeer(Func<byte[], StorageRangesMessage> respond) => _respond = respond;

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
                => throw new NotSupportedException();

            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
                => Task.FromResult(_respond(r.StartingHash));

            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => throw new NotSupportedException();

            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => throw new NotSupportedException();
        }

        private sealed class RecordingFlatWriter : ISnapFlatStateWriter
        {
            private readonly object _gate = new();
            public readonly Dictionary<string, byte[]> Storage = new();
            public readonly List<(string Kind, string Hash)> Operations = new();

            public byte[] StorageOf(byte[] accountHash, byte[] slotHash)
            {
                lock (_gate) return Storage.TryGetValue(accountHash.ToHex() + ":" + slotHash.ToHex(), out var v) ? v : null;
            }

            public Task<Account> GetAccountByHashAsync(byte[] accountHash)
                => throw new NotSupportedException("write-only, like the SST bulk sink");

            public Task SaveAccountByHashAsync(byte[] accountHash, Account account)
            {
                lock (_gate) Operations.Add(("account", accountHash.ToHex()));
                return Task.CompletedTask;
            }

            public Task DeleteAccountByHashAsync(byte[] accountHash)
            {
                lock (_gate) Operations.Add(("delete", accountHash.ToHex()));
                return Task.CompletedTask;
            }

            public Task SaveStorageByHashAsync(byte[] accountHash, byte[] slotKeccak, byte[] value)
            {
                lock (_gate)
                {
                    Storage[accountHash.ToHex() + ":" + slotKeccak.ToHex()] = value;
                    Operations.Add(("storage", accountHash.ToHex()));
                }
                return Task.CompletedTask;
            }
        }
    }
}
