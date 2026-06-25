using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    /// <summary>
    /// Coverage for the §F.1-F.4 + I-reframed spec in
    /// <c>docs/internal/snap-sync-phases-vs-geth.md</c>: SnapSyncState
    /// persistence + resume-from-task-cursor + Phase-enum gating.
    /// </summary>
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
            public ValueTask BeginAccountStorageAsync(byte[] accountHash, byte[] expectedStorageRoot, CancellationToken ct) => default;
            public ValueTask WriteStorageSlotAsync(byte[] slotHash, byte[] valueRlp, CancellationToken ct)
            { SlotsWritten.Add(slotHash); return default; }
            public ValueTask EndAccountStorageAsync(CancellationToken ct) => default;
            public ValueTask AbortAccountStorageAsync(CancellationToken ct) => default;
            public ValueTask WriteBytecodeAsync(byte[] codeHash, byte[] code, CancellationToken ct)
            { BytecodesWritten.Add(codeHash); return default; }
            public ValueTask<byte[]> FinaliseRootAsync(CancellationToken ct)
                => new(_finaliseRoot ?? new byte[32]);
        }

        /// <summary>
        /// Captures every <c>StartingHash</c> the client sends on its
        /// <c>GetAccountRange</c> requests so tests can assert the resume
        /// cursor was honoured.
        /// </summary>
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
                => Task.FromResult(new StorageRangesMessage { RequestId = r.RequestId, Slots = new(), Proof = new() });
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => Task.FromResult(new ByteCodesMessage { RequestId = r.RequestId, Codes = new List<byte[]>() });
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => Task.FromResult(new TrieNodesMessage { RequestId = r.RequestId, Nodes = new List<byte[]>() });
        }

        /// <summary>
        /// Build a synthetic account-range response with one empty-storage
        /// account plus a left-edge proof anchored at <paramref name="startKey"/>.
        /// </summary>
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

            var trie = new PatriciaTrie();
            var storage = new InMemoryTrieStorage();
            trie.Put(accountHash, canonical, storage);
            trie.SaveDirtyNodesToStorage(storage);
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

        // ---------- Phase2 partial resume ----------

        [Fact]
        public async Task Phase2_PartialState_Resumes_From_PersistedTaskCursor()
        {
            // Persist a Phase2Running checkpoint whose only task starts at 0x80...
            // and assert SnapSyncClient sends its first GetAccountRange with
            // StartingHash == 0x80...
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

        // ---------- Phase3 skips Phase 2 ----------

        [Fact]
        public void Phase3_PersistedHealRoot_IsAvailableForResume()
        {
            // Phase3 resume contract: SnapBootstrapper sees Phase=Phase3Running
            // and routes through the heal-only path. SnapSyncClient.SyncStateAsync
            // is NOT invoked. This test asserts the persisted state survives
            // an encode/decode round trip with the heal-target root intact so the
            // bootstrapper's resume decision branch has the data it needs.
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

        // ---------- Schema version mismatch ----------

        [Fact]
        public void SchemaVersion_Mismatch_TreatedAsFresh()
        {
            // Old / unknown schema version → encoder.Decode returns null so the
            // bootstrapper falls through to the fresh-snap path. Without this
            // gate every struct evolution would silently brick on-disk state.
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

        // ---------- ClearSnapSyncState semantics ----------

        [Fact]
        public void ClearSnapSyncState_RemovesPersistedRow()
        {
            // Complete-orphan branch in SnapBootstrapper.RunAsync calls
            // ClearSnapSyncState() before restarting. Validate the store
            // primitive actually drops the row so a subsequent
            // GetSnapSyncState reads null.
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

        // ---------- Checkpoint-sink fires ----------

        [Fact]
        public async Task Checkpoint_Persisted_When_PhaseRunning()
        {
            // checkpointSink fires at least once per snap-sync run that produces
            // any progress — at the end-of-range chunk boundary the loop hits
            // its MaybeCheckpoint(chunkBytes) call. Validates the sink is
            // actually invoked with a Phase2Running payload whose Next cursor
            // is the right-edge (FilledHash(0xff)).
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

            // At least one checkpoint fired (at end-of-range, chunk size hits the
            // explicit MaybeCheckpoint). Verify shape: Phase2Running + at least
            // one task with the right-edge Next cursor.
            Assert.NotEmpty(captured);
            var last = captured[^1];
            Assert.Equal(SnapPhase.Phase2Running, last.Phase);
            Assert.Single(last.Tasks);
            Assert.Equal(FilledHash(0xff), last.Tasks[0].Next);
        }

        // ---------- Counter accumulation ----------

        [Fact]
        public async Task Counters_Accumulate_FromResumeFrom_AcrossSession()
        {
            // Seed running counters in resumeFrom (simulating prior session)
            // and verify the checkpointed counters reflect the cumulative total
            // (resumeFrom counters + this session's writes), not just this
            // session's writes. Mirrors geth's SyncProgress fields (sync.go:381-397).
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
            // 1 account written this session on top of 1000 from resume.
            Assert.Equal(1001UL, last.Counters.AccountsSynced);
            Assert.True(last.Counters.AccountBytes > 65_536, "account bytes must accumulate above the seed");
        }

        // ---------- CRITICAL-3: liveTaskNext sized by seedTasks.Count ----------

        [Fact]
        public async Task Resume_With_TaskCount_Exceeding_AccountConcurrency_NoIndexOutOfRange()
        {
            // Seed a resume state whose task array is larger than the
            // default AccountConcurrency (16). The pre-fix allocation of
            // liveTaskNext used `concurrency` rather than seedTasks.Count
            // and threw IndexOutOfRangeException on workers[i>15].
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

            // The pre-fix behaviour throws IndexOutOfRangeException during
            // BuildCheckpointState. After the fix the call returns normally.
            var ex = await Record.ExceptionAsync(() =>
                client.SyncStateAsync(stateRoot, resumeState, checkpointSink: null));
            Assert.Null(ex);
        }
    }
}
