using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    public partial class SnapSyncClient
    {
        private sealed class SnapAccountRangeConsumer
        {
            private enum IdleOutcome { AllDone, Retry }

            private readonly SnapSyncClient _client;
            private readonly int _consumerIdx;
            private readonly SnapTaskSet _taskSet;
            private readonly byte[][] _liveTargetRootHolder;
            private readonly ConcurrentBag<AccountNeedingHeal> _accountsNeedingHeal;
            private readonly ConcurrentDictionary<string, DeferredStorageDebt> _deferredStorageDebts;
            private readonly ConcurrentDictionary<int, ActiveSnapLeaseInfo> _activeAccountRangeLeases;
            private readonly ITrieNodeStore _storageNodeStore;
            private readonly bool _cursoredWhalesSupported;
            private readonly Func<long> _getProgressSnapshot;
            private readonly ulong? _fetchPivotBlock;
            private readonly Action<AccountWorkerResult> _publishDeltas;
            private readonly Action _addAccount;
            private readonly Action<int, ulong> _publishBytecodes;
            private readonly Action<IReadOnlyList<byte[]>> _publishDeferredCode;
            private readonly Action<ulong> _maybeCheckpoint;
            private readonly byte[][] _frozenRootHolder;

            private AccountEncoder _accountDecoder;
            private ulong _reqId;
            private long _fetchFailures;
            private int _sameStageFailures;
            private long _idleSpins;
            private long _ownerlessIdleSpins;
            private long _lastIdleProgressSnapshot;
            private long _lastTaskSetDumpTick;

            public SnapAccountRangeConsumer(
                SnapSyncClient client, int consumerIdx, SnapTaskSet taskSet, byte[][] liveTargetRootHolder,
                ConcurrentBag<AccountNeedingHeal> accountsNeedingHeal,
                ConcurrentDictionary<string, DeferredStorageDebt> deferredStorageDebts,
                ConcurrentDictionary<int, ActiveSnapLeaseInfo> activeAccountRangeLeases,
                ITrieNodeStore storageNodeStore, bool cursoredWhalesSupported, Func<long> getProgressSnapshot, ulong? fetchPivotBlock,
                Action<AccountWorkerResult> publishDeltas, Action addAccount,
                Action<int, ulong> publishBytecodes, Action<IReadOnlyList<byte[]>> publishDeferredCode,
                Action<ulong> maybeCheckpoint, byte[][] frozenRootHolder = null)
            {
                _client = client; _consumerIdx = consumerIdx; _taskSet = taskSet;
                _liveTargetRootHolder = liveTargetRootHolder; _accountsNeedingHeal = accountsNeedingHeal;
                _deferredStorageDebts = deferredStorageDebts; _activeAccountRangeLeases = activeAccountRangeLeases;
                _storageNodeStore = storageNodeStore; _cursoredWhalesSupported = cursoredWhalesSupported;
                _getProgressSnapshot = getProgressSnapshot;
                _fetchPivotBlock = fetchPivotBlock; _publishDeltas = publishDeltas; _addAccount = addAccount;
                _publishBytecodes = publishBytecodes; _publishDeferredCode = publishDeferredCode;
                _maybeCheckpoint = maybeCheckpoint; _frozenRootHolder = frozenRootHolder;
            }

            public async Task RunAsync(CancellationToken ct)
            {
                _accountDecoder = new AccountEncoder();
                _reqId = (ulong)(_consumerIdx + 1) * 1_000_000UL;
                _fetchFailures = 0;
                _sameStageFailures = 0;
                _idleSpins = 0;
                _ownerlessIdleSpins = 0;
                _lastIdleProgressSnapshot = _getProgressSnapshot();
                _lastTaskSetDumpTick = 0;

                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    NarratePeriodicTaskSetState();

                    await _client.WaitWhileStateBackpressuredAsync(_consumerIdx, ct).ConfigureAwait(false);

                    var lease = _taskSet.LeaseNext();
                    if (lease == null)
                    {
                        if (await HandleNoLeasableFragmentAsync(ct).ConfigureAwait(false) == IdleOutcome.AllDone)
                            return;
                        continue;
                    }
                    _idleSpins = 0;
                    _ownerlessIdleSpins = 0;
                    _lastIdleProgressSnapshot = _getProgressSnapshot();

                    if (lease is SnapFragment.StorageSubtask storageFrag)
                    {
                        await ProcessStorageSubtaskLeaseAsync(storageFrag, ct).ConfigureAwait(false);
                        continue;
                    }
                    if (lease is SnapFragment.SmallStorageBatch smallBatchFrag)
                    {
                        await ProcessSmallStorageBatchLeaseAsync(smallBatchFrag, ct).ConfigureAwait(false);
                        continue;
                    }
                    if (lease is SnapFragment.Bytecode bytecodeFrag)
                    {
                        await ProcessBytecodeLeaseAsync(bytecodeFrag, ct).ConfigureAwait(false);
                        continue;
                    }
                    if (lease is not SnapFragment.AccountRange frag)
                        throw new NotSupportedException($"snap.consumer.unsupported_fragment kind={lease.GetType().Name}");
                    await ProcessAccountRangeLeaseAsync(frag, ct).ConfigureAwait(false);
                }
            }

            private void NarratePeriodicTaskSetState()
            {
                if (_consumerIdx == 0 && Environment.TickCount64 - _lastTaskSetDumpTick > 15000)
                {
                    _lastTaskSetDumpTick = Environment.TickCount64;
                    _client._logger.LogInformation("snap.taskset.state {State}", _taskSet.DescribeNotDone());
                }
            }

            private async Task<IdleOutcome> HandleNoLeasableFragmentAsync(CancellationToken ct)
            {
                if (_taskSet.AllDone) return IdleOutcome.AllDone;

                _idleSpins++;
                var diagnostics = _taskSet.GetDiagnostics();
                var activeLeases = SnapshotActiveLeases(_activeAccountRangeLeases);
                var progressSnapshot = _getProgressSnapshot();
                if (activeLeases.Count > 0 || progressSnapshot != _lastIdleProgressSnapshot)
                {
                    _ownerlessIdleSpins = 0;
                    _lastIdleProgressSnapshot = progressSnapshot;
                }
                else
                {
                    _ownerlessIdleSpins++;
                }

                var idleFor = TimeSpan.FromMilliseconds(_idleSpins * (long)SnapConsumerIdleDelayMs);
                var ownerlessIdleFor = TimeSpan.FromMilliseconds(_ownerlessIdleSpins * (long)SnapConsumerIdleDelayMs);
                if (_idleSpins % 600 == 0)
                {
                    if (activeLeases.Count > 0)
                    {
                        _client._logger.LogInformation(
                            "snap.taskset.waiting_on_active_work consumer={Consumer} idle_sec={IdleSec} active_leases={ActiveLeases} oldest_owner={OldestOwner} oldest_task={OldestTask} oldest_active_sec={OldestActiveSec} oldest_origin=0x{OldestOrigin} oldest_limit=0x{OldestLimit} progress={Progress} all_done={AllDone} has_leasable={HasLeasable} range_inflight={RangeInFlight} range_pending={RangePending}",
                            _consumerIdx, (long)idleFor.TotalSeconds, activeLeases.Count, activeLeases.OldestConsumer,
                            activeLeases.OldestTaskIndex, activeLeases.OldestAgeSeconds, activeLeases.OldestOrigin,
                            activeLeases.OldestLimit, progressSnapshot, diagnostics.AllDone, diagnostics.HasLeasableWork,
                            diagnostics.RangeInFlightCount, diagnostics.RangePendingCount);
                    }
                    else
                    {
                        _client._logger.LogWarning(
                            "snap.consumer.idle consumer={Consumer} no leasable fragment for {Spins} polls (~{Seconds}s) ownerless_idle_sec={OwnerlessIdleSec} progress={Progress} all_done={AllDone} has_leasable={HasLeasable} range_inflight={RangeInFlight} range_pending={RangePending} state_tasks={StateTasks} large_inflight={LargeInFlight} code_queue={CodeQueue}",
                            _consumerIdx, _idleSpins, (long)idleFor.TotalSeconds, (long)ownerlessIdleFor.TotalSeconds,
                            progressSnapshot, diagnostics.AllDone, diagnostics.HasLeasableWork,
                            diagnostics.RangeInFlightCount, diagnostics.RangePendingCount, diagnostics.StateTaskCount,
                            diagnostics.LargeSubtaskInFlightCount, diagnostics.CodeQueueCount);
                    }
                }

                if (_client.TaskSetStallTimeout > TimeSpan.Zero
                    && activeLeases.Count == 0
                    && !diagnostics.HasLeasableWork
                    && ownerlessIdleFor >= _client.TaskSetStallTimeout)
                {
                    _client._logger.LogError(
                        "snap.taskset.stalled consumer={Consumer} ownerless_idle_sec={IdleSec} progress={Progress} all_done={AllDone} has_leasable={HasLeasable} range_inflight={RangeInFlight} range_pending={RangePending} state_tasks={StateTasks} large_pending={LargePending} large_inflight={LargeInFlight} code_queue={CodeQueue}",
                        _consumerIdx, (long)ownerlessIdleFor.TotalSeconds, progressSnapshot, diagnostics.AllDone,
                        diagnostics.HasLeasableWork, diagnostics.RangeInFlightCount, diagnostics.RangePendingCount,
                        diagnostics.StateTaskCount, diagnostics.LargeSubtaskPendingCount,
                        diagnostics.LargeSubtaskInFlightCount, diagnostics.CodeQueueCount);
                    throw new SnapTaskSetStalledException(_consumerIdx, ownerlessIdleFor, progressSnapshot, diagnostics);
                }

                await Task.Delay(SnapConsumerIdleDelayMs, ct).ConfigureAwait(false);
                return IdleOutcome.Retry;
            }

            private async Task ProcessStorageSubtaskLeaseAsync(SnapFragment.StorageSubtask storageFrag, CancellationToken ct)
            {
                var storageStateRoot = Volatile.Read(ref _liveTargetRootHolder[0]);
                var page = new AccountWorkerResult();
                try
                {
                    await _client.ProcessStorageSubtaskAsync(
                            storageFrag, _taskSet, _storageNodeStore, storageStateRoot,
                            _accountsNeedingHeal, _deferredStorageDebts, _fetchPivotBlock, ct,
                            _frozenRootHolder, page)
                        .ConfigureAwait(false);
                }
                catch
                {
                    _taskSet.Revert(storageFrag);
                    throw;
                }
                _publishDeltas(page);
                _maybeCheckpoint(page.StorageBytesDelta);
            }

            private async Task ProcessSmallStorageBatchLeaseAsync(SnapFragment.SmallStorageBatch frag, CancellationToken ct)
            {
                var stateRoot = Volatile.Read(ref _liveTargetRootHolder[0]);
                var page = new AccountWorkerResult();

                var accounts = new List<(byte[] Hash, byte[] Root)>(frag.Items.Length);
                foreach (var item in frag.Items) accounts.Add((item.AccountHash, item.StorageRoot));

                try
                {
                    var fullyProcessed = await _client.FetchPageStorageAsync(
                            stateRoot, accounts, page,
                            _accountsNeedingHeal, _deferredStorageDebts, _fetchPivotBlock, _reqId,
                            NoProductiveSignal, NoFailureSignal, _taskSet, frag.TaskIndex,
                            _cursoredWhalesSupported,
                            () => Volatile.Read(ref _liveTargetRootHolder[0]),
                            ct, _frozenRootHolder)
                        .ConfigureAwait(false);

                    if (!fullyProcessed)
                    {
                        _taskSet.Revert(frag);
                        return;
                    }

                    var outcomes = new List<SmallStorageOutcome>(frag.Items.Length);
                    foreach (var item in frag.Items)
                        outcomes.Add(new SmallStorageOutcome(item.AccountHash, SmallStorageResult.Done, null));

                    _taskSet.CompleteSmallBatch(frag, outcomes, stateRoot);
                    _publishDeltas(page);
                }
                catch
                {
                    _taskSet.Revert(frag);
                    throw;
                }
            }

            private async Task ProcessBytecodeLeaseAsync(SnapFragment.Bytecode frag, CancellationToken ct)
            {
                try
                {
                    await FetchAndPublishBytecodesAsync(
                        new List<byte[]>(frag.Hashes), NoProductiveSignal, ct).ConfigureAwait(false);
                }
                catch
                {
                    _taskSet.Revert(frag);
                    throw;
                }
            }

            private static void NoProductiveSignal() { }

            private static void NoFailureSignal() { }

            private async Task ProcessAccountRangeLeaseAsync(SnapFragment.AccountRange frag, CancellationToken ct)
            {
                var activeLease = ActiveSnapLeaseInfo.Create(_consumerIdx, frag);
                _activeAccountRangeLeases[_consumerIdx] = activeLease;
                bool leaseOpen = true;
                void MarkProductive()
                {
                    activeLease.MarkProductive();
                    _sameStageFailures = 0;
                }
                void MarkFailure()
                {
                    _sameStageFailures++;
                    if (_client.ActiveLeaseSameStageFailureBudget > 0 && _sameStageFailures >= _client.ActiveLeaseSameStageFailureBudget)
                        throw new SnapTaskLeaseStalledException(
                            TimeSpan.FromMilliseconds(Math.Max(0, Environment.TickCount64 - activeLease.LastProductiveTick)),
                            SnapshotActiveLeases(_activeAccountRangeLeases),
                            _taskSet.GetDiagnostics());
                }
                try
                {

                var currentRoot = Volatile.Read(ref _liveTargetRootHolder[0]);

                if (FrozenRootDiverged(currentRoot))
                {
                    _taskSet.Revert(frag);
                    leaseOpen = false;
                    return;
                }

                AccountRangeMessage resp;
                Nethereum.Merkle.Patricia.ProofVerification.RangeProofResult? verifiedResult = null;
                List<byte[]> verifiedCanonicalValues = null;
                try
                {
                    resp = await _client._peer.GetAccountRangeAsync(new GetAccountRangeMessage
                    {
                        RequestId = _reqId++,
                        RootHash = currentRoot,
                        StartingHash = frag.Origin,
                        LimitHash = frag.Limit,
                        ResponseBytes = _client._responseBytesBudget
                    },
                    verifyResponse: r =>
                    {
                        var res = SnapProofVerifier.VerifyAccountRangeResponse(currentRoot, frag.Origin, r, out var vals);
                        if (res.Valid) { verifiedResult = res; verifiedCanonicalValues = vals; }
                        return res.Valid;
                    },
                    ct).ConfigureAwait(false);
                }
                catch (FetchRequestFailedException ex)
                {
                    activeLease.MarkActivity();
                    _taskSet.Revert(frag);
                    leaseOpen = false;
                    if (++_fetchFailures % 3 == 0)
                        _client._logger.LogWarning(
                            "snap.consumer.fetch_failed consumer={Consumer} failures={Failures} stage_failures={StageFailures} last={Error} - retrying account-range",
                            _consumerIdx, _fetchFailures, _sameStageFailures + 1, ex.InnerException?.Message ?? ex.Message);
                    MarkFailure();
                    await Task.Delay(SnapAccountRangeRetryDelayMs, ct).ConfigureAwait(false);
                    return;
                }

                activeLease.MarkActivity();

                var accountProofResult = ResolveAccountProof(currentRoot, frag, resp, verifiedResult, verifiedCanonicalValues, out var canonicalValues);

                if (resp.Accounts.Count == 0)
                {
                    if (accountProofResult.Valid)
                    {
                        MarkProductive();
                        _taskSet.CompleteAccountRange(frag, Array.Empty<AccountClassification>(), null, done: true);
                        leaseOpen = false;
                    }
                    else
                    {
                        _taskSet.Revert(frag);
                        leaseOpen = false;
                        MarkFailure();
                        await Task.Delay(SnapAccountRangeRetryDelayMs, ct).ConfigureAwait(false);
                    }
                    return;
                }

                if (!accountProofResult.Valid)
                    throw new InvalidOperationException(
                        $"Snap account-range proof failed for state root 0x{currentRoot.ToHex()} " +
                        $"starting at 0x{frag.Origin.ToHex()} ({resp.Accounts.Count} entries) — peer returned tampered or malformed data");

                bool rangeCompletedByBoundary = TrimAccountsBeyondLimit(resp, frag, canonicalValues);

                if (resp.Accounts.Count == 0)
                {
                    MarkProductive();
                    _taskSet.CompleteAccountRange(frag, Array.Empty<AccountClassification>(), null, done: true);
                    leaseOpen = false;
                    return;
                }

                _client._logger.LogDebug(
                    "snap.verify c={C} root=0x{Root} from=0x{From} received={Recv} proof=valid hasMore={More}",
                    _consumerIdx, currentRoot.ToHex(), frag.Origin.ToHex(), resp.Accounts.Count, accountProofResult.HasMore);

                var build = await BuildAccountPageAsync(resp, canonicalValues, ct).ConfigureAwait(false);
                var page = build.Page;
                var pageStorage = build.Storage;
                var pageCodeHashes = build.CodeHashes;
                var chunkBytes = build.ChunkBytes;
                MarkProductive();

                if (pageStorage.Count > 0)
                {
                    var storageFullyProcessed = await _client.FetchPageStorageAsync(
                            currentRoot,
                            pageStorage,
                            page,
                            _accountsNeedingHeal,
                            _deferredStorageDebts,
                            _fetchPivotBlock,
                            _reqId,
                            MarkProductive,
                            MarkFailure,
                            _taskSet,
                            frag.TaskIndex,
                            _cursoredWhalesSupported,
                            () => Volatile.Read(ref _liveTargetRootHolder[0]),
                            ct,
                            _frozenRootHolder)
                        .ConfigureAwait(false);
                    if (!storageFullyProcessed)
                    {
                        _taskSet.Revert(frag);
                        leaseOpen = false;
                        return;
                    }
                }
                chunkBytes += page.StorageBytesDelta;

                chunkBytes += await FetchAndPublishBytecodesAsync(pageCodeHashes, MarkProductive, ct).ConfigureAwait(false);

                _client.FlushAccountTrieForCheckpoint();

                _client._logger.LogDebug(
                    "snap.committed c={C} root=0x{Root} accounts={Acc} slots={Slots}",
                    _consumerIdx, currentRoot.ToHex(), resp.Accounts.Count, page.StorageSlotsSyncedDelta);

                _publishDeltas(page);
                for (ulong k = 0; k < page.AccountsSyncedDelta; k++) _addAccount();

                var lastHash = resp.Accounts[^1].Hash;
                bool done = AccountRangeReachedEnd(lastHash, frag, accountProofResult, rangeCompletedByBoundary);
                _taskSet.CompleteAccountRange(frag, Array.Empty<AccountClassification>(),
                    done ? null : lastHash, done);
                leaseOpen = false;
                _maybeCheckpoint(chunkBytes);
                }
                catch
                {
                    if (leaseOpen)
                        _taskSet.Revert(frag);
                    throw;
                }
                finally
                {
                    _activeAccountRangeLeases.TryRemove(_consumerIdx, out _);
                }
            }

            private bool FrozenRootDiverged(byte[] currentRoot)
                => _frozenRootHolder != null
                    && !ByteUtil.AreEqual(currentRoot, Volatile.Read(ref _frozenRootHolder[0]));

            private Nethereum.Merkle.Patricia.ProofVerification.RangeProofResult ResolveAccountProof(
                byte[] currentRoot, SnapFragment.AccountRange frag, AccountRangeMessage resp,
                Nethereum.Merkle.Patricia.ProofVerification.RangeProofResult? verifiedResult,
                List<byte[]> verifiedCanonicalValues, out List<byte[]> canonicalValues)
            {
                if (verifiedResult.HasValue)
                {
                    canonicalValues = verifiedCanonicalValues;
                    return verifiedResult.Value;
                }
                return SnapProofVerifier.VerifyAccountRangeResponse(currentRoot, frag.Origin, resp, out canonicalValues);
            }

            private bool TrimAccountsBeyondLimit(AccountRangeMessage resp, SnapFragment.AccountRange frag, List<byte[]> canonicalValues)
            {
                bool rangeCompletedByBoundary = false;
                for (int i = 0; i < resp.Accounts.Count; i++)
                {
                    int cmp = ByteArrayComparer.Current.Compare(resp.Accounts[i].Hash, frag.Limit);
                    if (cmp == 0)
                    {
                        rangeCompletedByBoundary = true;
                        continue;
                    }
                    if (cmp > 0)
                    {
                        resp.Accounts.RemoveRange(i, resp.Accounts.Count - i);
                        canonicalValues.RemoveRange(i, canonicalValues.Count - i);
                        rangeCompletedByBoundary = true;
                        break;
                    }
                }
                return rangeCompletedByBoundary;
            }

            private async Task<AccountPageBuild> BuildAccountPageAsync(AccountRangeMessage resp, List<byte[]> canonicalValues, CancellationToken ct)
            {
                var page = new AccountWorkerResult();
                var pageStorage = new List<(byte[] Hash, byte[] Root)>();
                var pageCodeHashes = new List<byte[]>();
                ulong chunkBytes = 0;
                for (int i = 0; i < resp.Accounts.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var entry = resp.Accounts[i];

                    await _client._sink.WriteAccountAsync(entry.Hash, entry.Body, ct).ConfigureAwait(false);
                    page.AccountsSyncedDelta++;
                    var slimLen = entry.Body == null ? 0 : (ulong)entry.Body.Length;
                    page.AccountBytesDelta += slimLen;
                    chunkBytes += slimLen;

                    var decoded = _accountDecoder.Decode(canonicalValues[i]);
                    if (!ByteUtil.AreEqual(decoded.StateRoot, DefaultValues.EMPTY_TRIE_HASH))
                        pageStorage.Add((entry.Hash, decoded.StateRoot));
                    if (!ByteUtil.AreEqual(decoded.CodeHash, DefaultValues.EMPTY_DATA_HASH))
                        pageCodeHashes.Add(decoded.CodeHash);
                }
                return new AccountPageBuild(page, pageStorage, pageCodeHashes, chunkBytes);
            }

            private async Task<ulong> FetchAndPublishBytecodesAsync(List<byte[]> pageCodeHashes, Action markProductive, CancellationToken ct)
            {
                var codeResult = await _client.FetchAndWriteBytecodesAsync(pageCodeHashes, _reqId, markProductive, ct).ConfigureAwait(false);
                _publishBytecodes(codeResult.Count, codeResult.Bytes);
                if (codeResult.Deferred.Count > 0) _publishDeferredCode(codeResult.Deferred);
                return codeResult.Bytes;
            }

            private bool AccountRangeReachedEnd(
                byte[] lastHash, SnapFragment.AccountRange frag,
                Nethereum.Merkle.Patricia.ProofVerification.RangeProofResult accountProofResult, bool rangeCompletedByBoundary)
                => rangeCompletedByBoundary
                    || !accountProofResult.HasMore
                    || ByteArrayComparer.Current.Compare(lastHash, frag.Limit) >= 0;

            private readonly struct AccountPageBuild
            {
                public readonly AccountWorkerResult Page;
                public readonly List<(byte[] Hash, byte[] Root)> Storage;
                public readonly List<byte[]> CodeHashes;
                public readonly ulong ChunkBytes;

                public AccountPageBuild(
                    AccountWorkerResult page, List<(byte[] Hash, byte[] Root)> storage,
                    List<byte[]> codeHashes, ulong chunkBytes)
                {
                    Page = page;
                    Storage = storage;
                    CodeHashes = codeHashes;
                    ChunkBytes = chunkBytes;
                }
            }
        }
    }
}
