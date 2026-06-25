using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Pulls full state for a given root from a snap/1 peer:
    /// 1. Walks the account range in slices, decodes each slim body, and streams every
    ///    account into the provided <see cref="ISnapSyncSink"/>.
    /// 2. For each account with a non-empty storage root, streams the storage trie ranges
    ///    into the sink while still inside the account-range batch (no full-account-list
    ///    accumulation).
    /// 3. After all account ranges land, pulls bytecodes by codeHash for accounts with
    ///    non-empty code and streams them into the sink.
    /// 4. Asks the sink to finalise its running state-root computation; compares it to
    ///    <c>targetRoot</c>. Mismatch =&gt; tampered or incomplete data, throw.
    ///
    /// A peer that returns a tampered proof is rejected on the spot; a peer that returns
    /// a tampered range that survives the per-account-storage check is caught at the
    /// final root comparison. Either way, the client never returns a "successful" result
    /// against a target root that wasn't actually reached.
    /// </summary>
    public class SnapSyncClient
    {
        private readonly ISnapPeer _peer;
        private readonly ISnapSyncSink _sink;
        private readonly int _accountsPerRequest;
        private readonly ulong _responseBytesBudget;
        private readonly ILogger _logger;
        private readonly SnapSyncMetrics _metrics;

        public SnapSyncClient(ISnapPeer peer, int accountsPerRequest = 256, ulong responseBytesBudget = 524_288UL)
            : this(peer, sink: null, accountsPerRequest, responseBytesBudget) { }

        /// <summary>
        /// Construct with an explicit sink. Pass <c>null</c> to use the in-memory sink (back-compat
        /// default for AppChain and tests).
        /// </summary>
        public SnapSyncClient(
            ISnapPeer peer,
            ISnapSyncSink sink,
            int accountsPerRequest = 256,
            ulong responseBytesBudget = 524_288UL,
            ILogger logger = null,
            SnapSyncMetrics metrics = null)
        {
            _peer = peer ?? throw new ArgumentNullException(nameof(peer));
            _sink = sink ?? new InMemorySnapSyncSink();
            _accountsPerRequest = accountsPerRequest;
            _responseBytesBudget = responseBytesBudget;
            _logger = logger ?? NullLogger.Instance;
            _metrics = metrics;
        }

        private Action<int, int> _undersizedResumeWarning => (count, conc) =>
            _logger.LogWarning(
                "snap.resume.degraded tasks={Count} expected_concurrency={Conc}",
                count, conc);

        /// <summary>
        /// Legacy pivot-refresh callback. Unused in the fixed-root model — the sync
        /// runs a fixed target root and the bootstrapper cancels + restarts it with a
        /// fresh pivot once the head moves ~2x the pivot-trail distance ahead.
        /// Retained only for API compatibility.
        /// </summary>
        public Func<CancellationToken, Task<byte[]>> PivotRefresher { get; set; }

        private const int SnapAccountRangeRetryDelayMs = 1_000;

        /// <summary>
        /// Account-range partition concurrency. The full account hashspace is
        /// split into this many contiguous
        /// sub-ranges, each driven by an independent worker so peer-bound
        /// latency parallelises across the snap stream.
        /// </summary>
        public int AccountConcurrency { get; set; } = 16;

        public class SyncResult
        {
            public ISnapSyncSink Sink { get; set; }
            public byte[] ComputedRoot { get; set; }
            public bool RootMatchesTarget { get; set; }
            public int AccountCount { get; set; }

            // Back-compat for in-memory consumers (AppChain + snap conformance tests).
            // Null when running with a streaming sink (e.g. RocksDb).
            public PatriciaTrie StateTrie { get; set; }
            public InMemoryTrieStorage TrieStorage { get; set; }
            public Dictionary<string, byte[]> BytecodeByHash { get; set; } = new();

            /// <summary>
            /// The state root in effect when the sync ended — equal to the
            /// caller's targetRoot when no pivot rotation occurred, otherwise
            /// the most recent root returned by <see cref="PivotRefresher"/>.
            /// Callers that follow up with a heal phase should use this root.
            /// </summary>
            public byte[] FinalTargetRoot { get; set; }

            /// <summary>
            /// Accounts whose storage trie drifted mid-fetch (the peer's snapshot
            /// rotated past the storageRoot we captured at account-decode time).
            /// Each entry pairs the keccak(address) account hash with the
            /// storageRoot we verified the first chunk against, flagging it for
            /// heal: the heal phase targets only the unconverged storage subtree
            /// rather than re-walking every account. Empty on the happy path.
            /// </summary>
            public IReadOnlyList<AccountNeedingHeal> AccountsNeedingHeal { get; set; }
                = Array.Empty<AccountNeedingHeal>();
        }

        /// <summary>
        /// Per-account heal hint produced when storage drift forces the snap
        /// stream to skip an account. Captures the storageRoot the verifier
        /// matched against so the heal phase can re-fetch under the right
        /// expected subtree root.
        /// </summary>
        public sealed record AccountNeedingHeal(byte[] AccountHash, byte[] ExpectedStorageRoot);

        /// <summary>
        /// Outcome of pulling one account's storage range. <c>NeedsHeal</c> is
        /// true when the peer's snapshot drifted past the captured storageRoot
        /// mid-fetch — partial slots may already be persisted; the heal phase
        /// reconciles via <see cref="AccountNeedingHeal"/>. <c>SlotsWritten</c>
        /// and <c>BytesWritten</c> feed the running snap-sync counters that
        /// land in the persisted <see cref="SnapSyncState"/>.
        /// </summary>
        private sealed record StorageFetchResult(
            bool Completed, bool NeedsHeal, ulong SlotsWritten, ulong BytesWritten);

        /// <summary>
        /// Aggregated outcome of one account-range worker. Carries final
        /// cursor + running counter deltas back to the orchestrator so the
        /// next checkpoint reflects all workers' contributions.
        /// </summary>
        private sealed class AccountWorkerResult
        {
            public byte[] FinalNext;
            public byte[] Last;
            public int AccountCount;
            public ulong AccountsSyncedDelta;
            public ulong AccountBytesDelta;
            public ulong StorageSlotsSyncedDelta;
            public ulong StorageBytesDelta;
            public List<AccountNeedingHeal> AccountsNeedingHeal = new();
        }

        public Task<SyncResult> SyncStateAsync(byte[] targetRoot, CancellationToken ct = default)
            => SyncStateAsync(targetRoot, resumeFrom: null, checkpointSink: null, ct);

        /// <summary>
        /// Checkpoint flush threshold. The persisted <see cref="SnapSyncState"/>
        /// blob is rewritten via the
        /// <c>checkpointSink</c> only after the running counters cross this
        /// many bytes since the last persist. Stops the snap-sync stream from
        /// dominating disk I/O with a per-chunk metadata write while still
        /// bounding the kill-and-restart re-fetch window to ~8 MB of accounts /
        /// storage / bytecodes.
        /// </summary>
        private const ulong CheckpointBytesThreshold = 8UL * 1024 * 1024;

        /// <summary>
        /// Per-request hash cap for <c>GetByteCodes</c> (1024). The snap server truncates
        /// responses to its own per-response byte budget regardless, so
        /// chunking the request side at this cap matches what the server is
        /// willing to ever return in one round-trip.
        /// </summary>
        private const int MaxCodeRequestCount = 1024;

        /// <summary>
        /// Resume-aware entry point. <paramref name="resumeFrom"/> seeds the
        /// account-range cursor and counters from a persisted
        /// <see cref="SnapSyncState"/>. <paramref name="checkpointSink"/> is
        /// invoked after every chunk whose accumulated byte count crosses the
        /// 8 MB threshold and on graceful shutdown (cancellation). Pass
        /// <c>null</c> for both to behave exactly like the legacy entry point.
        /// </summary>
        public async Task<SyncResult> SyncStateAsync(
            byte[] targetRoot,
            SnapSyncState resumeFrom,
            Action<SnapSyncState> checkpointSink,
            CancellationToken ct = default)
        {
            if (targetRoot == null || targetRoot.Length != 32)
                throw new ArgumentException("targetRoot must be 32 bytes", nameof(targetRoot));

            await _sink.BeginAsync(targetRoot, ct).ConfigureAwait(false);

            var concurrency = Math.Max(1, AccountConcurrency);

            // Seed the worker tasks. Resume reuses the persisted task list
            // verbatim; fresh start splits [0..0xff..ff] into
            // <see cref="AccountConcurrency"/> contiguous sub-ranges so all
            // workers can drive their account-range walks in parallel.
            List<SnapSyncAccountTask> seedTasks;
            if (resumeFrom != null && resumeFrom.Tasks != null && resumeFrom.Tasks.Count > 0)
            {
                seedTasks = new List<SnapSyncAccountTask>(resumeFrom.Tasks);
                if (seedTasks.Count < concurrency)
                {
                    _undersizedResumeWarning?.Invoke(seedTasks.Count, concurrency);
                }
            }
            else
            {
                seedTasks = new List<SnapSyncAccountTask>(concurrency);
                var ranges = SplitHashRange(new byte[32], FilledHash(0xff), concurrency);
                foreach (var range in ranges)
                {
                    seedTasks.Add(new SnapSyncAccountTask
                    {
                        Next = range.Start,
                        Last = range.End,
                        StorageCompleted = Array.Empty<byte[]>(),
                        SubTasks = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(),
                    });
                }
            }

            var codeHashesToFetch = new ConcurrentBag<byte[]>();

            long accountsSynced = (long)(resumeFrom?.Counters?.AccountsSynced ?? 0);
            long accountBytes = (long)(resumeFrom?.Counters?.AccountBytes ?? 0);
            long storageSlotsSynced = (long)(resumeFrom?.Counters?.StorageSlotsSynced ?? 0);
            long storageBytes = (long)(resumeFrom?.Counters?.StorageBytes ?? 0);
            long bytecodesSynced = (long)(resumeFrom?.Counters?.BytecodesSynced ?? 0);
            long bytecodeBytes = (long)(resumeFrom?.Counters?.BytecodeBytes ?? 0);
            long bytesSinceLastCheckpoint = 0;

            // Per-task live next cursor used by BuildCheckpointState to
            // reflect the latest progress without re-entering the workers.
            // Size by seedTasks.Count — a resume may carry more or fewer
            // tasks than the current AccountConcurrency, so allocating against
            // concurrency would index out of range when the saved task list
            // exceeded the constant.
            var liveTaskNext = new byte[seedTasks.Count][];
            for (int i = 0; i < seedTasks.Count; i++)
                liveTaskNext[i] = (byte[])seedTasks[i].Next.Clone();

            SnapSyncState BuildCheckpointState(SnapPhase phase, byte[] healTargetRoot)
            {
                var snapshot = new SnapSyncAccountTask[seedTasks.Count];
                for (int i = 0; i < seedTasks.Count; i++)
                {
                    var seedTask = seedTasks[i];
                    snapshot[i] = new SnapSyncAccountTask
                    {
                        Next = (byte[])Volatile.Read(ref liveTaskNext[i]).Clone(),
                        Last = seedTask.Last,
                        StorageCompleted = seedTask.StorageCompleted ?? Array.Empty<byte[]>(),
                        SubTasks = seedTask.SubTasks
                            ?? new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(),
                    };
                }
                return new SnapSyncState
                {
                    SchemaVersion = 1,
                    Phase = phase,
                    PivotBlockNumber = resumeFrom?.PivotBlockNumber ?? 0,
                    PivotBlockHash = resumeFrom?.PivotBlockHash ?? new byte[32],
                    HealTargetRoot = healTargetRoot ?? resumeFrom?.HealTargetRoot ?? new byte[32],
                    Tasks = snapshot,
                    Counters = new SnapSyncCounters
                    {
                        AccountsSynced = (ulong)Interlocked.Read(ref accountsSynced),
                        AccountBytes = (ulong)Interlocked.Read(ref accountBytes),
                        StorageSlotsSynced = (ulong)Interlocked.Read(ref storageSlotsSynced),
                        StorageBytes = (ulong)Interlocked.Read(ref storageBytes),
                        BytecodesSynced = (ulong)Interlocked.Read(ref bytecodesSynced),
                        BytecodeBytes = (ulong)Interlocked.Read(ref bytecodeBytes),
                        TrieNodesHealed = resumeFrom?.Counters?.TrieNodesHealed ?? 0,
                        TrieNodeBytesHealed = resumeFrom?.Counters?.TrieNodeBytesHealed ?? 0,
                        BytecodesHealed = resumeFrom?.Counters?.BytecodesHealed ?? 0,
                    },
                };
            }

            // Coordinated checkpoint emission. Any worker may push bytes into
            // the shared counter; when the threshold is crossed, exactly one
            // worker wins the CAS reset and emits the checkpoint, with workers
            // contributing concurrently.
            var checkpointLock = new object();
            void MaybeCheckpoint(ulong addedBytes)
            {
                if (checkpointSink == null) return;
                var newTotal = Interlocked.Add(ref bytesSinceLastCheckpoint, (long)addedBytes);
                if (newTotal < (long)CheckpointBytesThreshold) return;
                // Reset the running counter under a CAS so concurrent callers
                // race for the right to emit. The losing caller sees a value
                // below the threshold and returns; the winning caller proceeds
                // to invoke the user's checkpoint sink under a lock so the
                // sink itself never sees overlapping calls.
                if (Interlocked.Exchange(ref bytesSinceLastCheckpoint, 0) < (long)CheckpointBytesThreshold)
                    return;
                lock (checkpointLock)
                {
                    checkpointSink(BuildCheckpointState(SnapPhase.Phase2Running, healTargetRoot: null));
                }
            }

            // Fan out one worker per partition. Each runs the same loop the
            // single-stream client used to run, scoped to its [Next..Last]
            // range. Workers share the live pivot root, counters, and the
            // codeHashesToFetch bag; they do NOT share the next-start cursor.
            // Single-element int array holds the shared CAS lock slot used to
            // serialize pivot-refresh calls; an array is the simplest way to
            // get a stable ref-target across lambda captures.
            var pivotRefresherLock = new int[1];
            // Fixed pivot root for the whole sync cycle: the bootstrapper cancels and
            // restarts this sync with a fresh pivot when the head moves ~2x the
            // pivot-trail distance ahead. No mid-cycle root swap — liveTargetRootHolder
            // stays at targetRoot.
            var liveTargetRootHolder = new byte[][] { targetRoot };
            var workers = new List<Task<AccountWorkerResult>>(seedTasks.Count);

            try
            {
                for (int i = 0; i < seedTasks.Count; i++)
                {
                    var taskIdx = i;
                    var taskState = seedTasks[i];
                    workers.Add(Task.Run(() => RunAccountWorkerAsync(
                        taskIdx,
                        taskState,
                        liveTargetRootHolder,
                        pivotRefresherLock,
                        liveTaskNext,
                        codeHashesToFetch,
                        deltas =>
                        {
                            Interlocked.Add(ref accountsSynced, (long)deltas.AccountsSyncedDelta);
                            Interlocked.Add(ref accountBytes, (long)deltas.AccountBytesDelta);
                            Interlocked.Add(ref storageSlotsSynced, (long)deltas.StorageSlotsSyncedDelta);
                            Interlocked.Add(ref storageBytes, (long)deltas.StorageBytesDelta);
                            _metrics?.RecordPhase2AccountsSynced(
                                (long)deltas.AccountsSyncedDelta, (long)deltas.AccountBytesDelta);
                            _metrics?.RecordPhase2StorageSynced(
                                (long)deltas.StorageSlotsSyncedDelta, (long)deltas.StorageBytesDelta);
                        },
                        MaybeCheckpoint,
                        ct), ct));
                }

                var results = await Task.WhenAll(workers).ConfigureAwait(false);

                // Refresh seedTasks with each worker's final Next cursor so
                // the post-finally checkpoint (BuildCheckpointState) reflects
                // the converged hash positions.
                for (int i = 0; i < results.Length; i++)
                {
                    Volatile.Write(ref liveTaskNext[i], results[i].FinalNext);
                }

                int totalAccountCount = 0;
                var accountsNeedingHeal = new List<AccountNeedingHeal>();
                foreach (var r in results)
                {
                    totalAccountCount += r.AccountCount;
                    accountsNeedingHeal.AddRange(r.AccountsNeedingHeal);
                }

                // Bytecode batching: chunked GetByteCodes calls so each
                // request stays inside the peer's per-response code-count and
                // byte budgets. The peer's responseBytesBudget (~512 KB) caps
                // any single response; the explicit MaxCodeRequestCount cap
                // bounds each request. Without the chunking a single batch
                // covering tens of thousands of bytecodes was silently
                // truncated, losing the tail. Hashes not returned by the peer
                // in a given chunk are dropped from the queue — the heal phase
                // reissues them as trie-node walks discover the same codeHash
                // references again.
                if (!codeHashesToFetch.IsEmpty)
                {
                    var collected = new List<byte[]>();
                    foreach (var hash in codeHashesToFetch) collected.Add(hash);
                    var deduped = DedupeAndFilterCodeHashes(collected);
                    if (deduped.Count > 0)
                    {
                        var keccak = Sha3Keccack.Current;
                        ulong codeChunkBytes = 0;
                        ulong codeReqId = 1;

                        for (int offset = 0; offset < deduped.Count; offset += MaxCodeRequestCount)
                        {
                            ct.ThrowIfCancellationRequested();
                            int take = Math.Min(MaxCodeRequestCount, deduped.Count - offset);
                            var chunk = deduped.GetRange(offset, take);

                            var codesResp = await _peer.GetByteCodesAsync(new GetByteCodesMessage
                            {
                                RequestId = codeReqId++,
                                Hashes = chunk,
                                ResponseBytes = _responseBytesBudget
                            }, ct).ConfigureAwait(false);

                            var byHash = new Dictionary<byte[], byte[]>(ByteArrayComparer.Current);
                            foreach (var code in codesResp.Codes)
                            {
                                if (code == null || code.Length == 0) continue;
                                var hash = keccak.CalculateHash(code);
                                byHash[hash] = code;
                            }

                            foreach (var requestedHash in chunk)
                            {
                                if (byHash.TryGetValue(requestedHash, out var code))
                                {
                                    await _sink.WriteBytecodeAsync(requestedHash, code, ct).ConfigureAwait(false);
                                    Interlocked.Increment(ref bytecodesSynced);
                                    _metrics?.RecordPhase2BytecodesSynced(1);
                                    var clen = code == null ? 0 : (ulong)code.Length;
                                    Interlocked.Add(ref bytecodeBytes, (long)clen);
                                    codeChunkBytes += clen;
                                }
                            }
                        }

                        MaybeCheckpoint(codeChunkBytes);
                    }
                }

                var computedRoot = await _sink.FinaliseRootAsync(ct).ConfigureAwait(false);
                var finalRoot = Volatile.Read(ref liveTargetRootHolder[0]);
                var result = new SyncResult
                {
                    Sink = _sink,
                    ComputedRoot = computedRoot,
                    RootMatchesTarget = ByteUtil.AreEqual(computedRoot, finalRoot),
                    AccountCount = totalAccountCount,
                    FinalTargetRoot = finalRoot,
                    AccountsNeedingHeal = accountsNeedingHeal,
                };

                // Back-compat: expose the in-memory tries/bytecodes on the result for AppChain
                // consumers that still read them directly. Streaming sinks leave these null.
                if (_sink is InMemorySnapSyncSink mem)
                {
                    result.StateTrie = mem.StateTrie;
                    result.TrieStorage = mem.TrieStorage;
                    foreach (var kv in mem.BytecodeByHash)
                        result.BytecodeByHash[kv.Key] = kv.Value;
                }

                if (!result.RootMatchesTarget)
                    throw new InvalidOperationException(
                        $"Snap-sync result root {result.ComputedRoot.ToHex()} does not match target {finalRoot.ToHex()} — peer returned tampered or incomplete data");

                return result;
            }
            finally
            {
                // Defer-style guaranteed flush of the latest progress on EVERY
                // exit path (success, OperationCanceledException, peer-proof
                // failures throwing mid-batch, sink writes throwing, etc.).
                // Runs regardless of how the sync returns.
                // Best-effort: if the sink itself throws
                // on persist, the secondary failure is suppressed so the
                // original exception (if any) propagates intact.
                if (checkpointSink != null)
                {
                    try
                    {
                        lock (checkpointLock)
                        {
                            checkpointSink(BuildCheckpointState(SnapPhase.Phase2Running, healTargetRoot: null));
                        }
                    }
                    catch
                    {
                        // Persist failure on the finally path is non-fatal —
                        // never let it mask the original exception (or swallow
                        // a clean success path with a sink-side I/O error).
                    }
                }
            }
        }

        // Worker body. One per partition. Drives GetAccountRange against the
        // worker's [Next..Last] sub-range until the range is exhausted, the
        // peer signals !HasMore, or cancellation fires. Per-account storage
        // pulls happen serially within the worker; the
        // LargeContractConcurrency=16 fan-out already parallelises the
        // single-contract heavy case.
        private async Task<AccountWorkerResult> RunAccountWorkerAsync(
            int taskIdx,
            SnapSyncAccountTask state,
            byte[][] liveTargetRootHolder,
            int[] pivotRefresherLock,
            byte[][] liveTaskNext,
            ConcurrentBag<byte[]> codeHashesToFetch,
            Action<AccountWorkerResult> publishDeltas,
            Action<ulong> maybeCheckpoint,
            CancellationToken ct)
        {
            var accountDecoder = new AccountEncoder();
            var result = new AccountWorkerResult
            {
                FinalNext = (byte[])state.Next.Clone(),
                Last = state.Last,
            };

            var nextStart = (byte[])state.Next.Clone();
            var endOfRange = state.Last ?? FilledHash(0xff);
            ulong reqId = (ulong)(taskIdx + 1) * 1_000_000UL;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                // Fixed root for the whole cycle. If no connected peer can serve state
                // at this root right now (all syncing, or the root aged out), keep
                // retrying other peers — the bootstrapper cancels + restarts this sync
                // with a fresh pivot once the head moves ~2x the pivot-trail distance.
                var currentRoot = Volatile.Read(ref liveTargetRootHolder[0]);
                AccountRangeMessage resp;
                try
                {
                    resp = await _peer.GetAccountRangeAsync(new GetAccountRangeMessage
                    {
                        RequestId = reqId++,
                        RootHash = currentRoot,
                        StartingHash = nextStart,
                        LimitHash = endOfRange,
                        ResponseBytes = _responseBytesBudget
                    }, ct).ConfigureAwait(false);
                }
                catch (FetchRequestFailedException)
                {
                    await Task.Delay(SnapAccountRangeRetryDelayMs, ct).ConfigureAwait(false);
                    continue;
                }

                if (resp.Accounts.Count == 0)
                    break;

                var keys = new List<byte[]>(resp.Accounts.Count);
                var canonicalValues = new List<byte[]>(resp.Accounts.Count);
                foreach (var entry in resp.Accounts)
                {
                    keys.Add(entry.Hash);
                    canonicalValues.Add(SlimAccountEncoder.FromSlim(entry.Body));
                }
                var accountProof = (IList<byte[]>)(resp.Proof ?? new List<byte[]>());
                var accountProofResult = PatriciaRangeProofVerifier.VerifyRangeProof(
                    currentRoot, nextStart, keys, canonicalValues, accountProof);
                if (!accountProofResult.Valid)
                    throw new InvalidOperationException(
                        $"Snap account-range proof failed for state root 0x{currentRoot.ToHex()} " +
                        $"starting at 0x{nextStart.ToHex()} ({resp.Accounts.Count} entries) — peer returned tampered or malformed data");

                ulong chunkBytes = 0;
                ulong storageSlotsThisChunk = 0;
                ulong storageBytesThisChunk = 0;
                for (int i = 0; i < resp.Accounts.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var entry = resp.Accounts[i];

                    await _sink.WriteAccountAsync(entry.Hash, entry.Body, ct).ConfigureAwait(false);
                    result.AccountCount++;
                    result.AccountsSyncedDelta++;
                    var slimLen = entry.Body == null ? 0 : (ulong)entry.Body.Length;
                    result.AccountBytesDelta += slimLen;
                    chunkBytes += slimLen;

                    var decoded = accountDecoder.Decode(canonicalValues[i]);
                    if (!ByteUtil.AreEqual(decoded.StateRoot, DefaultValues.EMPTY_TRIE_HASH))
                    {
                        var storageResult = await PullStorageForAccountAsync(
                            currentRoot, entry.Hash, decoded.StateRoot, reqId++, ct)
                            .ConfigureAwait(false);
                        if (storageResult.NeedsHeal)
                            result.AccountsNeedingHeal.Add(new AccountNeedingHeal(entry.Hash, decoded.StateRoot));
                        result.StorageSlotsSyncedDelta += storageResult.SlotsWritten;
                        result.StorageBytesDelta += storageResult.BytesWritten;
                        storageSlotsThisChunk += storageResult.SlotsWritten;
                        storageBytesThisChunk += storageResult.BytesWritten;
                        chunkBytes += storageResult.BytesWritten;
                    }
                    if (!ByteUtil.AreEqual(decoded.CodeHash, DefaultValues.EMPTY_DATA_HASH))
                        codeHashesToFetch.Add(decoded.CodeHash);
                }

                // Publish this chunk's counter deltas eagerly so concurrent
                // checkpoints capture this worker's contribution without
                // waiting for the full task to drain. Reset the per-worker
                // deltas so the next chunk only publishes its own additions.
                var publish = new AccountWorkerResult
                {
                    AccountsSyncedDelta = result.AccountsSyncedDelta,
                    AccountBytesDelta = result.AccountBytesDelta,
                    StorageSlotsSyncedDelta = result.StorageSlotsSyncedDelta,
                    StorageBytesDelta = result.StorageBytesDelta,
                };
                publishDeltas(publish);
                result.AccountsSyncedDelta = 0;
                result.AccountBytesDelta = 0;
                result.StorageSlotsSyncedDelta = 0;
                result.StorageBytesDelta = 0;

                if (!accountProofResult.HasMore || ByteArrayComparer.Current.Compare(resp.Accounts[^1].Hash, endOfRange) >= 0)
                {
                    nextStart = endOfRange;
                    Volatile.Write(ref liveTaskNext[taskIdx], (byte[])nextStart.Clone());
                    maybeCheckpoint(chunkBytes);
                    break;
                }
                nextStart = IncrementHash(resp.Accounts[^1].Hash);
                Volatile.Write(ref liveTaskNext[taskIdx], (byte[])nextStart.Clone());
                maybeCheckpoint(chunkBytes);
            }

            result.FinalNext = nextStart;
            return result;
        }

        // Storage fan-out concurrency: when a single contract's storage is
        // larger than one response budget can serve, partition the remaining
        // hash space across this many
        // parallel fetches. Without this, USDC alone (>1 GB storage) blocks
        // the entire snap stream behind serial round-trips. Configurable so
        // we can isolate whether a proof failure is parallel-specific.
        public int LargeContractConcurrency { get; set; } = 16;

        // Per-account scope serialization. The sink's Begin/Write*/End
        // contract requires that ONE account scope is open at a time, but
        // multiple workers race to call BeginAccountStorageAsync. Hold this
        // semaphore for the entire Begin → End (or Abort) triplet so the
        // sink's invariant is preserved at the account granularity.
        private readonly SemaphoreSlim _accountScopeGate = new SemaphoreSlim(1, 1);

        private async Task<StorageFetchResult> PullStorageForAccountAsync(
            byte[] stateRoot, byte[] accountHash, byte[] storageRoot, ulong reqId, CancellationToken ct)
        {
            await _accountScopeGate.WaitAsync(ct).ConfigureAwait(false);
            bool scopeOpen = false;
            try
            {
                await _sink.BeginAccountStorageAsync(accountHash, storageRoot, ct).ConfigureAwait(false);
                scopeOpen = true;

                var firstStart = new byte[32];
                var firstResp = await _peer.GetStorageRangesAsync(new GetStorageRangesMessage
                {
                    RequestId = reqId,
                    RootHash = stateRoot,
                    AccountHashes = new List<byte[]> { accountHash },
                    StartingHash = firstStart,
                    LimitHash = FilledHash(0xff),
                    ResponseBytes = _responseBytesBudget
                }, ct).ConfigureAwait(false);

                if (firstResp.Slots.Count == 0 || firstResp.Slots[0].Count == 0)
                {
                    await _sink.EndAccountStorageAsync(ct).ConfigureAwait(false);
                    scopeOpen = false;
                    return new StorageFetchResult(Completed: true, NeedsHeal: false, SlotsWritten: 0, BytesWritten: 0);
                }

                var firstProofResult = TryVerifyStorageChunk(
                    accountHash, storageRoot, firstStart, firstResp);
                if (!firstProofResult.HasValue)
                {
                    await _sink.AbortAccountStorageAsync(ct).ConfigureAwait(false);
                    scopeOpen = false;
                    return new StorageFetchResult(Completed: false, NeedsHeal: true, SlotsWritten: 0, BytesWritten: 0);
                }

                ulong slotsCount = 0;
                ulong bytesCount = 0;
                foreach (var slot in firstResp.Slots[0])
                {
                    await _sink.WriteStorageSlotAsync(slot.Hash, slot.Data, ct).ConfigureAwait(false);
                    slotsCount++;
                    if (slot.Data != null) bytesCount += (ulong)slot.Data.Length;
                }
                if (!firstProofResult.Value.HasMore)
                {
                    await _sink.EndAccountStorageAsync(ct).ConfigureAwait(false);
                    scopeOpen = false;
                    return new StorageFetchResult(Completed: true, NeedsHeal: false, SlotsWritten: slotsCount, BytesWritten: bytesCount);
                }

                var lastFirstHash = firstResp.Slots[0][^1].Hash;
                var resumeFrom = IncrementHash(lastFirstHash);
                var concurrency = Math.Max(1, LargeContractConcurrency);
                var ranges = SplitHashRange(resumeFrom, FilledHash(0xff), concurrency);

                var pendingBuffers = new List<(byte[] Hash, byte[] Data)>[ranges.Count];
                var subTasks = new List<Task<bool>>(ranges.Count);
                for (int idx = 0; idx < ranges.Count; idx++)
                {
                    var capturedRange = ranges[idx];
                    var bufferIndex = idx;
                    var buffer = new List<(byte[] Hash, byte[] Data)>();
                    pendingBuffers[bufferIndex] = buffer;
                    subTasks.Add(Task.Run(async () =>
                    {
                        var subStart = capturedRange.Start;
                        while (true)
                        {
                            ct.ThrowIfCancellationRequested();

                            var subResp = await _peer.GetStorageRangesAsync(new GetStorageRangesMessage
                            {
                                RequestId = 0,
                                RootHash = stateRoot,
                                AccountHashes = new List<byte[]> { accountHash },
                                StartingHash = subStart,
                                LimitHash = FilledHash(0xff),
                                ResponseBytes = _responseBytesBudget
                            }, ct).ConfigureAwait(false);

                            if (subResp.Slots.Count == 0 || subResp.Slots[0].Count == 0) return true;

                            var subProofResult = TryVerifyStorageChunk(
                                accountHash, storageRoot, subStart, subResp);
                            if (!subProofResult.HasValue) return false;

                            bool crossed = false;
                            foreach (var slot in subResp.Slots[0])
                            {
                                if (ByteArrayComparer.Current.Compare(slot.Hash, capturedRange.End) > 0)
                                {
                                    crossed = true;
                                    break;
                                }
                                buffer.Add((slot.Hash, slot.Data));
                            }

                            if (crossed || !subProofResult.Value.HasMore) return true;
                            if (buffer.Count == 0) return true;
                            subStart = IncrementHash(buffer[^1].Hash);
                        }
                    }, ct));
                }

                bool allVerified;
                try
                {
                    var subResults = await Task.WhenAll(subTasks).ConfigureAwait(false);
                    allVerified = true;
                    foreach (var ok in subResults)
                    {
                        if (!ok) { allVerified = false; break; }
                    }
                }
                catch (Exception)
                {
                    await _sink.AbortAccountStorageAsync(ct).ConfigureAwait(false);
                    scopeOpen = false;
                    ct.ThrowIfCancellationRequested();
                    return new StorageFetchResult(Completed: false, NeedsHeal: true, SlotsWritten: slotsCount, BytesWritten: bytesCount);
                }

                if (!allVerified)
                {
                    await _sink.AbortAccountStorageAsync(ct).ConfigureAwait(false);
                    scopeOpen = false;
                    return new StorageFetchResult(Completed: false, NeedsHeal: true, SlotsWritten: slotsCount, BytesWritten: bytesCount);
                }

                foreach (var buffer in pendingBuffers)
                {
                    foreach (var slot in buffer)
                    {
                        await _sink.WriteStorageSlotAsync(slot.Hash, slot.Data, ct).ConfigureAwait(false);
                        slotsCount++;
                        if (slot.Data != null) bytesCount += (ulong)slot.Data.Length;
                    }
                }

                await _sink.EndAccountStorageAsync(ct).ConfigureAwait(false);
                scopeOpen = false;
                return new StorageFetchResult(Completed: true, NeedsHeal: false, SlotsWritten: slotsCount, BytesWritten: bytesCount);
            }
            finally
            {
                if (scopeOpen)
                {
                    try { await _sink.AbortAccountStorageAsync(ct).ConfigureAwait(false); }
                    catch { /* secondary failure on abort is non-fatal */ }
                }
                _accountScopeGate.Release();
            }
        }

        // Verify a storage chunk against the captured storageRoot. Returns
        // the proof result on success, null on failure (caller treats null
        // as "account drifted since pivot — defer to heal phase"). Marks the
        // account for heal rather than aborting the entire snap stream.
        private RangeProofResult? TryVerifyStorageChunk(
            byte[] accountHash, byte[] storageRoot, byte[] startingHash, StorageRangesMessage resp)
        {
            var slotKeys = new List<byte[]>(resp.Slots[0].Count);
            var slotValues = new List<byte[]>(resp.Slots[0].Count);
            foreach (var slot in resp.Slots[0])
            {
                slotKeys.Add(slot.Hash);
                slotValues.Add(slot.Data);
            }
            var storageProof = (IList<byte[]>)(resp.Proof ?? new List<byte[]>());
            var result = PatriciaRangeProofVerifier.VerifyRangeProof(
                storageRoot, startingHash, slotKeys, slotValues, storageProof);
            return result.Valid ? result : (RangeProofResult?)null;
        }

        // Partition the hash space [from, to] into `chunks` contiguous
        // sub-ranges. Last sub-range absorbs the modulus so the union is
        // exactly [from, to].
        private static List<(byte[] Start, byte[] End)> SplitHashRange(
            byte[] from, byte[] to, int chunks)
        {
            var fromBig = new System.Numerics.BigInteger(from, isUnsigned: true, isBigEndian: true);
            var toBig = new System.Numerics.BigInteger(to, isUnsigned: true, isBigEndian: true);
            var span = toBig - fromBig + 1;
            if (span <= chunks)
                return new List<(byte[], byte[])> { (from, to) };
            var step = span / chunks;
            var ranges = new List<(byte[] Start, byte[] End)>(chunks);
            var cursor = fromBig;
            for (int i = 0; i < chunks; i++)
            {
                var end = i == chunks - 1 ? toBig : cursor + step - 1;
                ranges.Add((BigToHash(cursor), BigToHash(end)));
                cursor = end + 1;
            }
            return ranges;
        }

        private static byte[] BigToHash(System.Numerics.BigInteger value)
        {
            var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (bytes.Length == 32) return bytes;
            var padded = new byte[32];
            System.Buffer.BlockCopy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
            return padded;
        }

        private static byte[] IncrementHash(byte[] h)
        {
            var copy = (byte[])h.Clone();
            for (int i = copy.Length - 1; i >= 0; i--)
            {
                if (copy[i] != 0xff) { copy[i]++; return copy; }
                copy[i] = 0;
            }
            return copy;
        }

        private static byte[] FilledHash(byte b)
        {
            var hh = new byte[32];
            for (int i = 0; i < 32; i++) hh[i] = b;
            return hh;
        }

        /// <summary>
        /// Dedupe by hash and drop the empty-code hash (peers never return code for it
        /// because there is no code). Preserves first-seen order so request shape stays
        /// deterministic.
        /// </summary>
        private static List<byte[]> DedupeAndFilterCodeHashes(List<byte[]> hashes)
        {
            var seen = new HashSet<byte[]>(ByteArrayComparer.Current);
            var result = new List<byte[]>(hashes.Count);
            foreach (var h in hashes)
            {
                if (h == null || h.Length == 0) continue;
                if (ByteUtil.AreEqual(h, DefaultValues.EMPTY_DATA_HASH)) continue;
                if (seen.Add(h)) result.Add(h);
            }
            return result;
        }

    }
}
