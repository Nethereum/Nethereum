using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
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
    public class SnapSyncResumeTests
    {
        private static readonly Sha3KeccackHashProvider Hash = new();

        private sealed class CapturingSink : ISnapSyncSink
        {
            private byte[] _finaliseRoot;
            public List<byte[]> AccountsWritten { get; } = new();
            public List<byte[]> SlotsWritten { get; } = new();
            public List<byte[]> BytecodesWritten { get; } = new();

            public void SetFinaliseRoot(byte[] root) => _finaliseRoot = root;

            public ValueTask BeginAsync(byte[] targetRoot, CancellationToken ct) => default;
            public ValueTask WriteAccountAsync(byte[] accountHash, byte[] slimRlp, CancellationToken ct)
            { AccountsWritten.Add(accountHash); return default; }
            public ValueTask<IStorageScope> BeginAccountStorageAsync(byte[] accountHash, byte[] expectedStorageRoot, CancellationToken ct)
                => new(new Scope(SlotsWritten));
            private sealed class Scope : IStorageScope
            {
                private readonly List<byte[]> _slots;
                public Scope(List<byte[]> slots) => _slots = slots;
                public ValueTask WriteSlotAsync(byte[] slotHash, byte[] valueRlp, CancellationToken ct)
                { lock (_slots) _slots.Add(slotHash); return default; }
                public ValueTask EndAsync(CancellationToken ct) => default;
                public ValueTask AbortAsync(CancellationToken ct) => default;
            }
            public ValueTask WriteBytecodeAsync(byte[] codeHash, byte[] code, CancellationToken ct)
            { BytecodesWritten.Add(codeHash); return default; }
            public ValueTask<byte[]> FinaliseRootAsync(CancellationToken ct)
                => new(_finaliseRoot ?? new byte[32]);
        }

        private sealed class CapturingSnapPeer : ISnapPeer
        {
            private readonly List<AccountRangeMessage> _accountResponses;
            private int _idx;
            public List<byte[]> StartingHashesObserved { get; } = new();

            public CapturingSnapPeer(params AccountRangeMessage[] accountResponses)
            {
                _accountResponses = accountResponses.ToList();
            }

            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
            {
                StartingHashesObserved.Add((byte[])r.StartingHash.Clone());
                var resp = _accountResponses[Math.Min(_idx, _accountResponses.Count - 1)];
                _idx++;
                return Task.FromResult(resp);
            }
            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
                => Task.FromResult(new StorageRangesMessage
                {
                    RequestId = r.RequestId,
                    Slots = r.AccountHashes.Select(_ => new List<StorageRangesMessage.SlotEntry>()).ToList(),
                    Proof = new()
                });
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => Task.FromResult(new ByteCodesMessage { RequestId = r.RequestId, Codes = new List<byte[]>() });
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => Task.FromResult(new TrieNodesMessage { RequestId = r.RequestId, Nodes = new List<byte[]>() });
        }

        private static (AccountRangeMessage Range, byte[] StateRoot, byte[] AccountHash) BuildOneAccountRange(
            byte[] startKey, byte highByteForAccountHash)
        {
            var accountHash = new byte[32];
            accountHash[0] = highByteForAccountHash;
            for (int i = 1; i < 32; i++) accountHash[i] = (byte)(0x10 + i);

            var account = new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)100,
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
                trie.Root, storage, startKey, FilledHash(0xff));

            var range = new AccountRangeMessage
            {
                RequestId = 1,
                Accounts = new List<AccountRangeMessage.AccountEntry>
                {
                    new() { Hash = accountHash, Body = slim }
                },
                Proof = proof,
            };
            return (range, stateRoot, accountHash);
        }

        private static (AccountRangeMessage Range, byte[] StateRoot, byte[] AccountHash, byte[] StorageRoot) BuildOneStorageAccountRange(
            byte[] startKey, byte highByteForAccountHash)
        {
            var accountHash = new byte[32];
            accountHash[0] = highByteForAccountHash;
            for (int i = 1; i < 32; i++) accountHash[i] = (byte)(0x20 + i);

            var storageRoot = Hash.ComputeHash(new byte[] { 0xAB, highByteForAccountHash });
            var account = new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)100,
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
                trie.Root, storage, startKey, FilledHash(0xff));

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
        private static byte[] FilledHash(byte b)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = b;
            return h;
        }

        private static byte[] Hash32(byte high)
        {
            var h = new byte[32];
            h[0] = high;
            return h;
        }


        [Fact]
        public async Task Phase2_PartialState_Resumes_From_PersistedTaskCursor()
        {
            var seedNext = Hash32(0x80);
            var (range, stateRoot, _) = BuildOneAccountRange(seedNext, highByteForAccountHash: 0x90);

            var peer = new CapturingSnapPeer(range);
            var sink = new CapturingSink();
            sink.SetFinaliseRoot(stateRoot);
            var client = new SnapSyncClient(peer, sink);

            var resumeState = new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = SnapPhase.Phase2Running,
                PivotBlockNumber = 100,
                PivotBlockHash = new byte[32],
                HealTargetRoot = new byte[32],
                Tasks = new[]
                {
                    new SnapSyncAccountTask
                    {
                        Next = seedNext,
                        Last = FilledHash(0xff),
                        StorageCompleted = Array.Empty<byte[]>(),
                        SubTasks = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(),
                    }
                },
                Counters = SnapSyncCounters.Zero,
            };

            await client.SyncStateAsync(stateRoot, resumeState, checkpointSink: null);

            Assert.NotEmpty(peer.StartingHashesObserved);
            Assert.Equal(seedNext, peer.StartingHashesObserved[0]);
        }


        [Fact]
        public void Phase3_PersistedHealRoot_IsAvailableForResume()
        {
            var healRoot = new byte[32];
            for (int i = 0; i < 32; i++) healRoot[i] = (byte)(0xAB + i);

            var stored = new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = SnapPhase.Phase3Running,
                PivotBlockNumber = 200,
                PivotBlockHash = FilledHash(0x55),
                HealTargetRoot = healRoot,
                Tasks = Array.Empty<SnapSyncAccountTask>(),
                Counters = SnapSyncCounters.Zero,
            };

            var blob = SnapSyncStateRlpEncoder.Instance.Encode(stored);
            var roundTripped = SnapSyncStateRlpEncoder.Instance.Decode(blob);

            Assert.NotNull(roundTripped);
            Assert.Equal(SnapPhase.Phase3Running, roundTripped.Phase);
            Assert.Equal(200UL, roundTripped.PivotBlockNumber);
            Assert.Equal(healRoot, roundTripped.HealTargetRoot);
        }

        [Fact]
        public void Given_AGeneratingSnapSyncState_When_RoundTrippedThroughTheRlpEncoder_Then_ThePhaseIsGenerating()
        {
            var stored = new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = SnapPhase.Generating,
                PivotBlockNumber = 300,
                PivotBlockHash = FilledHash(0x66),
                HealTargetRoot = FilledHash(0x77),
                Tasks = Array.Empty<SnapSyncAccountTask>(),
                Counters = SnapSyncCounters.Zero,
            };

            var roundTripped = SnapSyncStateRlpEncoder.Instance.Decode(SnapSyncStateRlpEncoder.Instance.Encode(stored));

            Assert.NotNull(roundTripped);
            Assert.Equal(SnapPhase.Generating, roundTripped.Phase);
            Assert.Equal(4, (byte)roundTripped.Phase);
            Assert.Equal(300UL, roundTripped.PivotBlockNumber);
            Assert.Equal(FilledHash(0x66), roundTripped.PivotBlockHash);
            Assert.Equal(FilledHash(0x77), roundTripped.HealTargetRoot);
        }


        [Fact]
        public void SchemaVersion_Mismatch_TreatedAsFresh()
        {
            var unknownVersionRow = new SnapSyncState
            {
                SchemaVersion = 99,
                Phase = SnapPhase.Phase2Running,
                PivotBlockNumber = 1,
                PivotBlockHash = new byte[32],
                HealTargetRoot = new byte[32],
                Tasks = Array.Empty<SnapSyncAccountTask>(),
                Counters = SnapSyncCounters.Zero,
            };

            var blob = SnapSyncStateRlpEncoder.Instance.Encode(unknownVersionRow);
            var decoded = SnapSyncStateRlpEncoder.Instance.Decode(blob);

            Assert.Null(decoded);
        }


        [Fact]
        public void ClearSnapSyncState_RemovesPersistedRow()
        {
            var store = new Nethereum.CoreChain.Storage.InMemory.InMemoryChainMetadataStore();
            store.SaveSnapSyncState(new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = SnapPhase.Complete,
                PivotBlockNumber = 50,
                PivotBlockHash = new byte[32],
                HealTargetRoot = new byte[32],
                Tasks = Array.Empty<SnapSyncAccountTask>(),
                Counters = SnapSyncCounters.Zero,
            });

            Assert.NotNull(store.GetSnapSyncState());
            store.ClearSnapSyncState();
            Assert.Null(store.GetSnapSyncState());
        }


        [Fact]
        public async Task Checkpoint_Persisted_When_PhaseRunning()
        {
            var startKey = new byte[32];
            var (range, stateRoot, _) = BuildOneAccountRange(startKey, highByteForAccountHash: 0x10);
            var peer = new CapturingSnapPeer(range);
            var sink = new CapturingSink();
            sink.SetFinaliseRoot(stateRoot);
            var client = new SnapSyncClient(peer, sink) { AccountConcurrency = 1 };

            var captured = new List<SnapSyncState>();
            await client.SyncStateAsync(
                stateRoot,
                resumeFrom: null,
                checkpointSink: s => captured.Add(s));

            Assert.NotEmpty(captured);
            var last = captured[^1];
            Assert.Equal(SnapPhase.Phase2Running, last.Phase);
            Assert.Single(last.Tasks);
            Assert.Equal(FilledHash(0xff), last.Tasks[0].Next);
        }

        [Fact]
        public async Task FinalCheckpointFailure_WhenPhase2Completed_ThenThrows()
        {
            var startKey = new byte[32];
            var (range, stateRoot, _) = BuildOneAccountRange(startKey, highByteForAccountHash: 0x10);
            var peer = new CapturingSnapPeer(range);
            var sink = new CapturingSink();
            sink.SetFinaliseRoot(stateRoot);
            var client = new SnapSyncClient(peer, sink) { AccountConcurrency = 1 };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                client.SyncStateAsync(
                    stateRoot,
                    resumeFrom: null,
                    checkpointSink: _ => throw new IOException("checkpoint write failed")));

            Assert.Contains("final checkpoint", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.IsType<IOException>(ex.InnerException);
        }

        [Fact]
        public async Task Checkpoint_NeverPersists_WhenFlushBulkFlatBeforeCheckpointFails()
        {
            var startKey = new byte[32];
            var (range, stateRoot, _) = BuildOneAccountRange(startKey, highByteForAccountHash: 0x10);
            var peer = new CapturingSnapPeer(range);
            var sink = new CapturingSink();
            sink.SetFinaliseRoot(stateRoot);
            var client = new SnapSyncClient(peer, sink) { AccountConcurrency = 1 };
            client.FlushBulkFlatBeforeCheckpoint = () => throw new IOException("simulated: bulk-flat buffer not yet durable");

            var captured = new List<SnapSyncState>();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                client.SyncStateAsync(stateRoot, resumeFrom: null, checkpointSink: s => captured.Add(s)));

            Assert.IsType<IOException>(ex.InnerException);
            Assert.Empty(captured);
        }

        [Fact]
        public async Task CheckpointPayload_ContainsDeferredStorageDebt_BeforeAdvancedCursor()
        {
            var startKey = new byte[32];
            var (range, stateRoot, accountHash, storageRoot) = BuildOneStorageAccountRange(startKey, highByteForAccountHash: 0x10);
            var peer = new CapturingSnapPeer(range);
            var sink = new CapturingSink();
            sink.SetFinaliseRoot(stateRoot);
            var client = new SnapSyncClient(peer, sink) { AccountConcurrency = 1 };

            var captured = new List<SnapSyncClient.SnapSyncCheckpoint>();
            await client.SyncStateWithCheckpointAsync(
                stateRoot,
                resumeFrom: null,
                checkpointSink: checkpoint => captured.Add(checkpoint));

            var checkpoint = Assert.Single(captured.FindAll(c => c.DeferredStorageDebts.Count == 1));
            Assert.Equal(FilledHash(0xff), checkpoint.State.Tasks[0].Next);
            var debt = Assert.Single(checkpoint.DeferredStorageDebts);
            Assert.Equal(accountHash, debt.AccountHash);
            Assert.Equal(storageRoot, debt.DiscoveredStorageRoot);
            Assert.Equal(stateRoot, debt.FetchStateRoot);
            Assert.Equal(StorageCompleteness.DeferredUnavailable, debt.Status);
            Assert.Equal(DeferredStorageReason.NonEmptyRootReturnedNoSlots, debt.Reason);
        }

        [Fact]
        public async Task Counters_Accumulate_FromResumeFrom_AcrossSession()
        {
            var startKey = new byte[32];
            var (range, stateRoot, _) = BuildOneAccountRange(startKey, highByteForAccountHash: 0x10);
            var peer = new CapturingSnapPeer(range);
            var sink = new CapturingSink();
            sink.SetFinaliseRoot(stateRoot);
            var client = new SnapSyncClient(peer, sink);

            var resumeState = new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = SnapPhase.Phase2Running,
                PivotBlockNumber = 100,
                PivotBlockHash = new byte[32],
                HealTargetRoot = new byte[32],
                Tasks = new[]
                {
                    new SnapSyncAccountTask
                    {
                        Next = startKey,
                        Last = FilledHash(0xff),
                        StorageCompleted = Array.Empty<byte[]>(),
                        SubTasks = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(),
                    }
                },
                Counters = new SnapSyncCounters
                {
                    AccountsSynced = 1000, AccountBytes = 65_536,
                    StorageSlotsSynced = 0, StorageBytes = 0,
                    BytecodesSynced = 0, BytecodeBytes = 0,
                    TrieNodesHealed = 0, TrieNodeBytesHealed = 0,
                    BytecodesHealed = 0,
                },
            };

            var captured = new List<SnapSyncState>();
            await client.SyncStateAsync(stateRoot, resumeState, s => captured.Add(s));

            Assert.NotEmpty(captured);
            var last = captured[^1];
            Assert.Equal(1001UL, last.Counters.AccountsSynced);
            Assert.True(last.Counters.AccountBytes > 65_536, "account bytes must accumulate above the seed");
        }


        [Fact]
        public async Task Resume_With_TaskCount_Exceeding_AccountConcurrency_NoIndexOutOfRange()
        {
            var startKey = new byte[32];
            var (range, stateRoot, _) = BuildOneAccountRange(startKey, highByteForAccountHash: 0x10);
            var peer = new CapturingSnapPeer(range);
            var sink = new CapturingSink();
            sink.SetFinaliseRoot(stateRoot);
            var client = new SnapSyncClient(peer, sink);

            var tasks = new SnapSyncAccountTask[20];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = new SnapSyncAccountTask
                {
                    Next = new byte[32],
                    Last = FilledHash(0xff),
                    StorageCompleted = Array.Empty<byte[]>(),
                    SubTasks = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(),
                };
            }
            var resumeState = new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = SnapPhase.Phase2Running,
                PivotBlockNumber = 100,
                PivotBlockHash = new byte[32],
                HealTargetRoot = new byte[32],
                Tasks = tasks,
                Counters = SnapSyncCounters.Zero,
            };

            var ex = await Record.ExceptionAsync(() =>
                client.SyncStateAsync(stateRoot, resumeState, checkpointSink: null));
            Assert.Null(ex);
        }
    }
}
