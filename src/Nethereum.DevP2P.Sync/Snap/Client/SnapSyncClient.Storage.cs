using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.ProofVerification;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    public partial class SnapSyncClient
    {
        public async Task<bool> FetchPageStorageAsync(
            byte[] stateRoot,
            List<(byte[] Hash, byte[] Root)> storageAccounts,
            AccountWorkerResult page,
            ConcurrentBag<AccountNeedingHeal> accountsNeedingHeal,
            ConcurrentDictionary<string, DeferredStorageDebt> deferredStorageDebts,
            ulong? fetchPivotBlock,
            ulong reqId,
            Action markProductive,
            Action markFailure,
            SnapTaskSet taskSet,
            int taskIndex,
            bool cursoredWhalesSupported,
            Func<byte[]> getLiveRoot,
            CancellationToken ct,
            byte[][] frozenRootHolder = null)
        {
            var remaining = storageAccounts;
            var reqIdHolder = new ulong[] { reqId };
            var dispatchRoot = stateRoot;
            int consecutiveEmptyDispatches = 0;
            while (remaining.Count > 0)
            {
                ct.ThrowIfCancellationRequested();

                var liveRoot = getLiveRoot != null ? getLiveRoot() : dispatchRoot;
                if (liveRoot != null && !ByteUtil.AreEqual(liveRoot, dispatchRoot))
                {
                    var (reresolved, allResolved) = await ReresolveStorageRootsAtLiveRootAsync(
                            liveRoot, remaining, ct)
                        .ConfigureAwait(false);
                    remaining = reresolved;
                    if (!allResolved)
                    {
                        await Task.Delay(SnapAccountRangeRetryDelayMs, ct).ConfigureAwait(false);
                        continue;
                    }
                    dispatchRoot = liveRoot;
                    consecutiveEmptyDispatches = 0;
                    if (remaining.Count == 0) return true;
                }

                if (frozenRootHolder != null
                    && !ByteUtil.AreEqual(dispatchRoot, Volatile.Read(ref frozenRootHolder[0])))
                    return false;

                var accountHashes = AccountHashesOf(remaining);

                StorageRangesMessage resp;
                var dispatchedRemaining = remaining;
                try
                {
                    resp = await _peer.GetStorageRangesAsync(new GetStorageRangesMessage
                    {
                        RequestId = reqIdHolder[0]++,
                        RootHash = dispatchRoot,
                        AccountHashes = accountHashes,
                        StartingHash = new byte[32],
                        LimitHash = SnapHashRanges.FilledHash(0xff),
                        ResponseBytes = _responseBytesBudget
                    },
                    verifyResponse: r => SnapProofVerifier.VerifyBatchStorageResponse(dispatchedRemaining, r),
                    ct).ConfigureAwait(false);
                }
                catch (FetchRequestFailedException)
                {
                    markFailure?.Invoke();
                    await Task.Delay(SnapAccountRangeRetryDelayMs, ct).ConfigureAwait(false);
                    continue;
                }

                int returned = resp.Slots.Count;
                if (returned == 0)
                {
                    consecutiveEmptyDispatches++;
                    await Task.Delay(EmptyDispatchBackoffMs(consecutiveEmptyDispatches), ct).ConfigureAwait(false);
                    continue;
                }
                consecutiveEmptyDispatches = 0;
                bool lastTruncated = resp.Proof != null && resp.Proof.Count > 0;

                await ApplyStorageRangePageAsync(
                    resp, remaining, returned, lastTruncated, cursoredWhalesSupported, dispatchRoot,
                    taskSet, taskIndex, page, accountsNeedingHeal, deferredStorageDebts,
                    fetchPivotBlock, reqIdHolder, markProductive, ct).ConfigureAwait(false);

                remaining = remaining.GetRange(returned, remaining.Count - returned);
            }
            return true;
        }

        private ISnapFlatStateWriter SinkFlatWriter
            => (_sink as TrieSnapSyncSink)?.FlatWriter ?? (_sink as FlatSnapSyncSink)?.FlatWriter;

        private static List<byte[]> AccountHashesOf(List<(byte[] Hash, byte[] Root)> accounts)
        {
            var accountHashes = new List<byte[]>(accounts.Count);
            foreach (var a in accounts) accountHashes.Add(a.Hash);
            return accountHashes;
        }

        private static int EmptyDispatchBackoffMs(int consecutiveEmptyDispatches)
            => Math.Min(
                SnapAccountRangeRetryDelayMs * (1 << Math.Min(consecutiveEmptyDispatches - 1, 8)),
                MaxEmptyDispatchBackoffMs);

        private async Task ApplyStorageRangePageAsync(
            StorageRangesMessage resp,
            List<(byte[] Hash, byte[] Root)> remaining,
            int returned,
            bool lastTruncated,
            bool cursoredWhalesSupported,
            byte[] dispatchRoot,
            SnapTaskSet taskSet,
            int taskIndex,
            AccountWorkerResult page,
            ConcurrentBag<AccountNeedingHeal> accountsNeedingHeal,
            ConcurrentDictionary<string, DeferredStorageDebt> deferredStorageDebts,
            ulong? fetchPivotBlock,
            ulong[] reqIdHolder,
            Action markProductive,
            CancellationToken ct)
        {
            for (int i = 0; i < returned; i++)
            {
                var (hash, root) = remaining[i];
                var slots = resp.Slots[i];
                bool isTruncatedWhale = lastTruncated && i == returned - 1;
                await ProcessBatchStorageAccountAsync(
                    hash, root, slots, isTruncatedWhale, cursoredWhalesSupported, dispatchRoot,
                    taskSet, taskIndex, page, accountsNeedingHeal, deferredStorageDebts,
                    fetchPivotBlock, reqIdHolder, markProductive, ct).ConfigureAwait(false);
            }
        }

        private async Task ProcessBatchStorageAccountAsync(
            byte[] hash,
            byte[] root,
            List<StorageRangesMessage.SlotEntry> slots,
            bool isTruncatedWhale,
            bool cursoredWhalesSupported,
            byte[] dispatchRoot,
            SnapTaskSet taskSet,
            int taskIndex,
            AccountWorkerResult page,
            ConcurrentBag<AccountNeedingHeal> accountsNeedingHeal,
            ConcurrentDictionary<string, DeferredStorageDebt> deferredStorageDebts,
            ulong? fetchPivotBlock,
            ulong[] reqIdHolder,
            Action markProductive,
            CancellationToken ct)
        {
            if (isTruncatedWhale)
            {
                if (cursoredWhalesSupported)
                {
                    taskSet.CreateLargeContract(
                        taskIndex, hash, root, dispatchRoot,
                        pageSlotCount: slots.Count,
                        lastSlotHash: slots.Count > 0 ? slots[^1].Hash : null);
                    return;
                }

                await PullAndRecordWhaleStorageAsync(
                    hash, root, dispatchRoot, page, accountsNeedingHeal, deferredStorageDebts,
                    fetchPivotBlock, reqIdHolder, markProductive, ct).ConfigureAwait(false);
                return;
            }

            if (slots.Count == 0)
            {
                RecordDeferredStorageDebt(
                    accountsNeedingHeal,
                    deferredStorageDebts,
                    hash,
                    root,
                    dispatchRoot,
                    fetchPivotBlock,
                    DeferredStorageReason.NonEmptyRootReturnedNoSlots,
                    StorageCompleteness.DeferredUnavailable);
                return;
            }

            if (!StoragePageMatchesStorageRoot(slots, root))
            {
                RecordDeferredStorageDebt(
                    accountsNeedingHeal,
                    deferredStorageDebts,
                    hash,
                    root,
                    dispatchRoot,
                    fetchPivotBlock,
                    DeferredStorageReason.StorageRootVerifyMismatch,
                    StorageCompleteness.DeferredUnavailable);
                return;
            }

            await WriteVerifiedStoragePageAsync(hash, root, slots, page, markProductive, ct).ConfigureAwait(false);
        }

        private async Task PullAndRecordWhaleStorageAsync(
            byte[] hash,
            byte[] root,
            byte[] dispatchRoot,
            AccountWorkerResult page,
            ConcurrentBag<AccountNeedingHeal> accountsNeedingHeal,
            ConcurrentDictionary<string, DeferredStorageDebt> deferredStorageDebts,
            ulong? fetchPivotBlock,
            ulong[] reqIdHolder,
            Action markProductive,
            CancellationToken ct)
        {
            var whale = await PullStorageForAccountAsync(dispatchRoot, hash, root, reqIdHolder[0]++, page, markProductive, ct)
                .ConfigureAwait(false);
            if (whale.NeedsHeal)
                RecordDeferredStorageDebt(
                    accountsNeedingHeal,
                    deferredStorageDebts,
                    hash,
                    root,
                    dispatchRoot,
                    fetchPivotBlock,
                    whale.Reason ?? DeferredStorageReason.BigAccountSubrangeFailed,
                    StorageCompleteness.DeferredBigAccount);
        }

        private static bool StoragePageMatchesStorageRoot(List<StorageRangesMessage.SlotEntry> slots, byte[] root)
        {
            var verifyTrie = new Nethereum.Merkle.Patricia.PatriciaTrie();
            foreach (var slot in slots)
                verifyTrie.Put(slot.Hash, slot.Data);
            return ByteUtil.AreEqual(verifyTrie.Root.GetHash(), root);
        }

        private async Task WriteVerifiedStoragePageAsync(
            byte[] hash,
            byte[] root,
            List<StorageRangesMessage.SlotEntry> slots,
            AccountWorkerResult page,
            Action markProductive,
            CancellationToken ct)
        {
            var scope = await _sink.BeginAccountStorageAsync(hash, root, ct).ConfigureAwait(false);
            foreach (var slot in slots)
            {
                await scope.WriteSlotAsync(slot.Hash, slot.Data, ct).ConfigureAwait(false);
                markProductive();
                page.AddStorageSlot(slot.Data);
            }
            await scope.EndAsync(ct).ConfigureAwait(false);
        }

        private async Task<(List<(byte[] Hash, byte[] Root)> Remaining, bool AllResolved)> ReresolveStorageRootsAtLiveRootAsync(
            byte[] liveRoot,
            List<(byte[] Hash, byte[] Root)> accounts,
            CancellationToken ct)
        {
            var resolved = new List<(byte[] Hash, byte[] Root)>(accounts.Count);
            var allResolved = true;
            foreach (var (hash, root) in accounts)
            {
                ct.ThrowIfCancellationRequested();
                var resolution = await DeferredStorageDebtFinalRootResolver.ResolveAccountStorageRootAsync(
                        liveRoot, hash, FetchOwnerAccountRangeAsync, ct)
                    .ConfigureAwait(false);

                switch (resolution.Status)
                {
                    case AccountStorageResolutionStatus.Found:
                        resolved.Add((hash, resolution.StorageRoot));
                        break;
                    case AccountStorageResolutionStatus.AbsentOrEmpty:
                        break;
                    default:
                        resolved.Add((hash, root));
                        allResolved = false;
                        break;
                }
            }
            return (resolved, allResolved);
        }

        private void RecordDeferredStorageDebt(
            ConcurrentBag<AccountNeedingHeal> accountsNeedingHeal,
            ConcurrentDictionary<string, DeferredStorageDebt> deferredStorageDebts,
            byte[] accountHash,
            byte[] storageRoot,
            byte[] fetchStateRoot,
            ulong? fetchPivotBlock,
            DeferredStorageReason reason,
            StorageCompleteness status)
        {
            accountsNeedingHeal.Add(new AccountNeedingHeal(accountHash, storageRoot));
            var debt = new DeferredStorageDebt
            {
                AccountHash = (byte[])accountHash.Clone(),
                DiscoveredStorageRoot = (byte[])storageRoot.Clone(),
                FetchStateRoot = (byte[])fetchStateRoot.Clone(),
                FetchPivotBlock = fetchPivotBlock,
                Reason = reason,
                Status = status,
            };

            var key = ByteUtil.Merge(accountHash, storageRoot).ToHex();
            var added = deferredStorageDebts.TryAdd(key, debt);
            if (!added)
            {
                deferredStorageDebts[key] = debt;
                return;
            }

            var accountHex = accountHash.ToHex();
            var storageRootHex = storageRoot.ToHex();
            var openDebts = deferredStorageDebts.Count;
            if (status == StorageCompleteness.DeferredBigAccount)
            {
                _logger.LogWarning(
                    "snap.phase2.bigaccount.deferred reason={Reason} account=0x{AccountHash} storage_root=0x{StorageRoot} pivot={Pivot} open_debts={OpenDebts}",
                    reason,
                    accountHex,
                    storageRootHex,
                    fetchPivotBlock,
                    openDebts);
            }
            else
            {
                _logger.LogDebug(
                    "snap.phase2.storage.deferred status={Status} reason={Reason} account=0x{AccountHash} storage_root=0x{StorageRoot} pivot={Pivot} open_debts={OpenDebts}",
                    status,
                    reason,
                    accountHex,
                    storageRootHex,
                    fetchPivotBlock,
                    openDebts);
            }
        }

        public async Task ProcessStorageSubtaskAsync(
            SnapFragment.StorageSubtask frag,
            SnapTaskSet taskSet,
            ITrieNodeStore storageNodeStore,
            byte[] stateRoot,
            ConcurrentBag<AccountNeedingHeal> accountsNeedingHeal,
            ConcurrentDictionary<string, DeferredStorageDebt> deferredStorageDebts,
            ulong? fetchPivotBlock,
            CancellationToken ct,
            byte[][] frozenRootHolder = null,
            AccountWorkerResult page = null)
        {
            if (TryCompleteOwnerViaRecordedStorageRoot(frag, taskSet, storageNodeStore))
                return;

            if (!await ReproveOwnerIfRootRotatedAsync(
                    frag, taskSet, storageNodeStore, stateRoot, ct).ConfigureAwait(false))
                return;

            var scope = (ResumableStorageScope)taskSet.GetOrCreateStorageScope(
                frag.TaskIndex, frag.AccountHash,
                () => ResumableStorageScope.Open(storageNodeStore, frag.AccountHash, flatWriter: SinkFlatWriter));

            var expectedStorageRoot = taskSet.GetStorageRoot(frag.TaskIndex, frag.AccountHash);

            if (frozenRootHolder != null
                && !ByteUtil.AreEqual(stateRoot, Volatile.Read(ref frozenRootHolder[0])))
            {
                taskSet.Revert(frag);
                return;
            }

            StorageRangesMessage resp;
            try
            {
                resp = await _peer.GetStorageRangesAsync(new GetStorageRangesMessage
                {
                    RequestId = 0,
                    RootHash = stateRoot,
                    AccountHashes = new List<byte[]> { frag.AccountHash },
                    StartingHash = frag.Next,
                    LimitHash = SnapHashRanges.FilledHash(0xff),
                    ResponseBytes = _responseBytesBudget
                },
                verifyResponse: r => expectedStorageRoot == null || SnapProofVerifier.VerifyStorageRangeResponse(expectedStorageRoot, frag.Next, r),
                ct).ConfigureAwait(false);
            }
            catch (FetchRequestFailedException)
            {
                await RevertStorageSubtaskAsync(frag, taskSet, ct).ConfigureAwait(false);
                return;
            }

            var flattened = FlattenStoragePage(resp);

            var currentStorageRoot = taskSet.GetStorageRoot(frag.TaskIndex, frag.AccountHash);
            if (currentStorageRoot == null)
            {
                return;
            }
            var pageResult = scope.ApplyVerifiedPage(currentStorageRoot, frag.Next, flattened.SlotHashes, flattened.ValuesRlp, flattened.ProofNodes);
            if (!pageResult.Accepted)
            {
                await RevertStorageSubtaskAsync(frag, taskSet, ct).ConfigureAwait(false);
                return;
            }
            if (page != null)
                foreach (var value in flattened.ValuesRlp) page.AddStorageSlot(value);

            var crossedBoundary = flattened.SlotHashes.Count > 0
                && ByteArrayComparer.Current.Compare(pageResult.Cursor, frag.Last) > 0;
            var subtaskDone = !pageResult.HasMore || crossedBoundary;

            var ownerDrained = taskSet.CompleteSubtask(
                frag, moreRemaining: !subtaskDone,
                nextCursor: subtaskDone ? null : pageResult.Cursor);

            if (subtaskDone && ownerDrained)
                CommitAndCompleteOwnerStorage(
                    frag, taskSet, scope, currentStorageRoot, stateRoot,
                    accountsNeedingHeal, deferredStorageDebts, fetchPivotBlock);
        }

        private bool TryCompleteOwnerViaRecordedStorageRoot(
            SnapFragment.StorageSubtask frag,
            SnapTaskSet taskSet,
            ITrieNodeStore storageNodeStore)
        {
            var resumeScope = (ResumableStorageScope)taskSet.GetOrCreateStorageScope(
                frag.TaskIndex, frag.AccountHash,
                () => ResumableStorageScope.Open(storageNodeStore, frag.AccountHash, flatWriter: SinkFlatWriter));
            var recordedStorageRoot = taskSet.GetStorageRoot(frag.TaskIndex, frag.AccountHash);
            if (recordedStorageRoot == null || !ByteUtil.AreEqual(resumeScope.CurrentRootHash, recordedStorageRoot))
                return false;

            taskSet.CompleteOwnerStorageUnderGate(() =>
            {
                if (taskSet.CompleteOwnerViaLocalRoot(frag.TaskIndex, frag.AccountHash))
                {
                    resumeScope.CommitDirtyNodes();
                    taskSet.ClearStorageScope(frag.TaskIndex, frag.AccountHash);
                }
            });
            return true;
        }

        private readonly record struct FlattenedStoragePage(
            List<byte[]> SlotHashes, List<byte[]> ValuesRlp, IList<byte[]> ProofNodes);

        private static FlattenedStoragePage FlattenStoragePage(StorageRangesMessage resp)
        {
            var slots = resp.Slots.Count > 0 ? resp.Slots[0] : new List<StorageRangesMessage.SlotEntry>();
            var slotHashes = new List<byte[]>(slots.Count);
            var valuesRlp = new List<byte[]>(slots.Count);
            foreach (var slot in slots)
            {
                slotHashes.Add(slot.Hash);
                valuesRlp.Add(slot.Data);
            }
            var proofNodes = (IList<byte[]>)(resp.Proof ?? new List<byte[]>());
            return new FlattenedStoragePage(slotHashes, valuesRlp, proofNodes);
        }

        private void CommitAndCompleteOwnerStorage(
            SnapFragment.StorageSubtask frag,
            SnapTaskSet taskSet,
            ResumableStorageScope scope,
            byte[] currentStorageRoot,
            byte[] stateRoot,
            ConcurrentBag<AccountNeedingHeal> accountsNeedingHeal,
            ConcurrentDictionary<string, DeferredStorageDebt> deferredStorageDebts,
            ulong? fetchPivotBlock)
        {
            taskSet.CompleteOwnerStorageUnderGate(() =>
            {
                scope.CommitDirtyNodes();
                taskSet.ClearStorageScope(frag.TaskIndex, frag.AccountHash);
                if (scope.CurrentRootHash is { } localRoot && !Nethereum.Util.ByteUtil.AreEqual(localRoot, currentStorageRoot))
                {
                    RecordDeferredStorageDebt(
                        accountsNeedingHeal, deferredStorageDebts,
                        frag.AccountHash, currentStorageRoot, stateRoot, fetchPivotBlock,
                        DeferredStorageReason.BigAccountChunked, StorageCompleteness.DeferredBigAccount);
                }
                taskSet.MarkStorageCompleted(frag.AccountHash);
            });
        }

        private async Task<bool> ReproveOwnerIfRootRotatedAsync(
            SnapFragment.StorageSubtask frag,
            SnapTaskSet taskSet,
            ITrieNodeStore storageNodeStore,
            byte[] stateRoot,
            CancellationToken ct)
        {
            var claim = taskSet.TryClaimReprove(frag.TaskIndex, frag.AccountHash, stateRoot);
            if (claim == SnapTaskSet.ReproveClaim.NotNeeded) return true;
            if (claim == SnapTaskSet.ReproveClaim.InFlight)
            {
                taskSet.Revert(frag);
                await Task.Delay(SnapConsumerIdleDelayMs, ct).ConfigureAwait(false);
                return false;
            }

            var committed = false;
            try
            {
                var resolution = await DeferredStorageDebtFinalRootResolver.ResolveAccountStorageRootAsync(
                    stateRoot, frag.AccountHash, FetchOwnerAccountRangeAsync, ct).ConfigureAwait(false);

                switch (resolution.Status)
                {
                    case AccountStorageResolutionStatus.Found:
                        taskSet.CommitReproveFound(frag.TaskIndex, frag.AccountHash, stateRoot, resolution.StorageRoot);
                        committed = true;
                        return true;

                    case AccountStorageResolutionStatus.AbsentOrEmpty:
                        taskSet.CommitReproveAbsentOrEmpty(frag.TaskIndex, frag.AccountHash, stateRoot);
                        committed = true;
                        var scope = (ResumableStorageScope)taskSet.GetOrCreateStorageScope(
                            frag.TaskIndex, frag.AccountHash,
                            () => ResumableStorageScope.Open(storageNodeStore, frag.AccountHash, flatWriter: SinkFlatWriter));
                        taskSet.CompleteOwnerStorageUnderGate(() =>
                        {
                            scope.CommitDirtyNodes();
                            taskSet.ClearStorageScope(frag.TaskIndex, frag.AccountHash);
                            taskSet.MarkStorageCompleted(frag.AccountHash);
                        });
                        return false;

                    default:
                        taskSet.AbandonReprove(frag.TaskIndex, frag.AccountHash);
                        committed = true;
                        await RevertStorageSubtaskAsync(frag, taskSet, ct).ConfigureAwait(false);
                        return false;
                }
            }
            finally
            {
                if (!committed) taskSet.AbandonReprove(frag.TaskIndex, frag.AccountHash);
            }
        }

        private async Task RevertStorageSubtaskAsync(
            SnapFragment.StorageSubtask frag, SnapTaskSet taskSet, CancellationToken ct)
        {
            taskSet.Revert(frag);
            await Task.Delay(StorageSubtaskRetryBackoffMs, ct).ConfigureAwait(false);
        }

        private Task<AccountRangeMessage> FetchOwnerAccountRangeAsync(
            byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
            => _peer.GetAccountRangeAsync(new GetAccountRangeMessage
            {
                RequestId = 0,
                RootHash = stateRoot,
                StartingHash = startingHash,
                LimitHash = limitHash,
                ResponseBytes = responseBytes
            }, ct);

        private async Task<bool> StreamStorageSubRangeAsync(
            (byte[] Start, byte[] End) capturedRange,
            byte[] stateRoot,
            byte[] accountHash,
            byte[] storageRoot,
            IStorageScope scope,
            AccountWorkerResult page,
            Action markProductive,
            CancellationToken ct)
        {
            var subStart = capturedRange.Start;
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var dispatchStart = subStart;
                var subResp = await _peer.GetStorageRangesAsync(new GetStorageRangesMessage
                {
                    RequestId = 0,
                    RootHash = stateRoot,
                    AccountHashes = new List<byte[]> { accountHash },
                    StartingHash = subStart,
                    LimitHash = SnapHashRanges.FilledHash(0xff),
                    ResponseBytes = _responseBytesBudget
                },
                verifyResponse: r => SnapProofVerifier.VerifyStorageRangeResponse(storageRoot, dispatchStart, r),
                ct).ConfigureAwait(false);

                if (subResp.Slots.Count == 0 || subResp.Slots[0].Count == 0)
                {
                    var absenceProof = (IList<byte[]>)(subResp.Proof ?? new List<byte[]>());
                    return ProofVerification.Current.Range.Verify(
                        storageRoot, subStart,
                        System.Array.Empty<byte[]>(), System.Array.Empty<byte[]>(),
                        absenceProof).Valid;
                }

                var subProofResult = TryVerifyStorageChunk(
                    accountHash, storageRoot, subStart, subResp);
                if (!subProofResult.HasValue) return false;

                bool crossed = false;
                byte[] lastHash = null;
                foreach (var slot in subResp.Slots[0])
                {
                    if (ByteArrayComparer.Current.Compare(slot.Hash, capturedRange.End) > 0)
                    {
                        crossed = true;
                        break;
                    }
                    await scope.WriteSlotAsync(slot.Hash, slot.Data, ct).ConfigureAwait(false);
                    markProductive();
                    page.AddStorageSlot(slot.Data);
                    lastHash = slot.Hash;
                }

                if (crossed || !subProofResult.Value.HasMore) return true;
                if (lastHash == null) return false;
                subStart = SnapHashRanges.IncrementHash(lastHash);
            }
        }

        private async Task<StorageFetchResult> PullStorageForAccountAsync(
            byte[] stateRoot, byte[] accountHash, byte[] storageRoot, ulong reqId, AccountWorkerResult page,
            Action markProductive, CancellationToken ct)
        {
            var scope = await _sink.BeginAccountStorageAsync(accountHash, storageRoot, ct).ConfigureAwait(false);
            bool scopeOpen = true;
            try
            {
                var firstStart = new byte[32];
                var firstResp = await _peer.GetStorageRangesAsync(new GetStorageRangesMessage
                {
                    RequestId = reqId,
                    RootHash = stateRoot,
                    AccountHashes = new List<byte[]> { accountHash },
                    StartingHash = firstStart,
                    LimitHash = SnapHashRanges.FilledHash(0xff),
                    ResponseBytes = _responseBytesBudget
                },
                verifyResponse: r => SnapProofVerifier.VerifyStorageRangeResponse(storageRoot, firstStart, r),
                ct).ConfigureAwait(false);

                if (firstResp.Slots.Count == 0 || firstResp.Slots[0].Count == 0)
                {
                    await scope.AbortAsync(ct).ConfigureAwait(false);
                    scopeOpen = false;
                    return new StorageFetchResult(Completed: false, NeedsHeal: true, Reason: DeferredStorageReason.BigAccountInitialEmpty);
                }

                var firstProofResult = TryVerifyStorageChunk(
                    accountHash, storageRoot, firstStart, firstResp);
                if (!firstProofResult.HasValue)
                {
                    await scope.AbortAsync(ct).ConfigureAwait(false);
                    scopeOpen = false;
                    return new StorageFetchResult(Completed: false, NeedsHeal: true, Reason: DeferredStorageReason.BigAccountInitialProofInvalid);
                }

                await WriteFirstStoragePageAsync(
                    scope, firstResp.Slots[0], page, markProductive, ct).ConfigureAwait(false);
                if (!firstProofResult.Value.HasMore)
                {
                    await scope.EndAsync(ct).ConfigureAwait(false);
                    scopeOpen = false;
                    return new StorageFetchResult(Completed: true, NeedsHeal: false, Reason: null);
                }

                var lastFirstHash = firstResp.Slots[0][^1].Hash;
                var resumeFrom = SnapHashRanges.IncrementHash(lastFirstHash);

                var subTasks = LaunchStorageSubRangeTasks(
                    resumeFrom, stateRoot, accountHash, storageRoot, scope,
                    page, markProductive, ct);

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
                    await scope.AbortAsync(ct).ConfigureAwait(false);
                    scopeOpen = false;
                    ct.ThrowIfCancellationRequested();
                    return new StorageFetchResult(Completed: false, NeedsHeal: true,
                        Reason: DeferredStorageReason.BigAccountException);
                }

                if (!allVerified)
                {
                    await scope.AbortAsync(ct).ConfigureAwait(false);
                    scopeOpen = false;
                    return new StorageFetchResult(Completed: false, NeedsHeal: true,
                        Reason: DeferredStorageReason.BigAccountSubrangeFailed);
                }

                await scope.EndAsync(ct).ConfigureAwait(false);
                scopeOpen = false;
                return new StorageFetchResult(Completed: true, NeedsHeal: false, Reason: null);
            }
            finally
            {
                if (scopeOpen)
                {
                    try { await scope.AbortAsync(ct).ConfigureAwait(false); }
                    catch { }
                }
            }
        }

        private async Task WriteFirstStoragePageAsync(
            IStorageScope scope,
            IReadOnlyList<StorageRangesMessage.SlotEntry> slots,
            AccountWorkerResult page,
            Action markProductive,
            CancellationToken ct)
        {
            foreach (var slot in slots)
            {
                await scope.WriteSlotAsync(slot.Hash, slot.Data, ct).ConfigureAwait(false);
                markProductive();
                page.AddStorageSlot(slot.Data);
            }
        }

        private List<Task<bool>> LaunchStorageSubRangeTasks(
            byte[] resumeFrom,
            byte[] stateRoot,
            byte[] accountHash,
            byte[] storageRoot,
            IStorageScope scope,
            AccountWorkerResult page,
            Action markProductive,
            CancellationToken ct)
        {
            var concurrency = Math.Max(1, LargeContractConcurrency);
            var ranges = SnapHashRanges.SplitHashRange(resumeFrom, SnapHashRanges.FilledHash(0xff), concurrency);
            var subTasks = new List<Task<bool>>(ranges.Count);
            for (int idx = 0; idx < ranges.Count; idx++)
            {
                var capturedRange = ranges[idx];
                subTasks.Add(Task.Run(() => StreamStorageSubRangeAsync(
                    capturedRange, stateRoot, accountHash, storageRoot, scope,
                    page, markProductive, ct), ct));
            }
            return subTasks;
        }

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
            var result = ProofVerification.Current.Range.Verify(
                storageRoot, startingHash, slotKeys, slotValues, storageProof);
            return result.Valid ? result : (RangeProofResult?)null;
        }
    }
}
