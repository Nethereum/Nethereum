using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using static Nethereum.DevP2P.Sync.Snap.Client.SnapSyncClient;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    internal sealed class CursoredStorageSubtask
    {
        private const int RetryBackoffMs = 100;

        private readonly SnapSyncClient _client;
        private readonly SnapTaskSet _taskSet;
        private readonly ITrieNodeStore _storageNodeStore;
        private readonly FrozenRoot _frozen;
        private readonly Action _markFailure;

        public CursoredStorageSubtask(
            SnapSyncClient client,
            SnapTaskSet taskSet,
            ITrieNodeStore storageNodeStore,
            FrozenRoot frozen,
            Action markFailure = null)
        {
            _client = client;
            _taskSet = taskSet;
            _storageNodeStore = storageNodeStore;
            _frozen = frozen;
            _markFailure = markFailure;
        }

        public async Task RunAsync(
            SnapFragment.StorageSubtask frag,
            byte[] stateRoot,
            AccountWorkerResult page,
            CancellationToken ct)
        {
            if (!await ReproveIfRootRotatedAsync(
                    frag, stateRoot, ct).ConfigureAwait(false))
                return;

            var scope = OpenScope(frag);

            var expectedStorageRoot = _taskSet.GetStorageRoot(frag.TaskIndex, frag.AccountHash);

            if (_frozen.Diverged(stateRoot))
            {
                _taskSet.Revert(frag);
                return;
            }

            var (resp, failed) = await RequestAsync(frag, stateRoot, expectedStorageRoot, ct).ConfigureAwait(false);
            if (failed)
                return;

            var flattened = StorageSlots.Flatten(resp);

            var currentStorageRoot = _taskSet.GetStorageRoot(frag.TaskIndex, frag.AccountHash);
            if (currentStorageRoot == null)
            {
                return;
            }
            var pageResult = ApplyPage(scope, frag, currentStorageRoot, flattened, page);
            if (!pageResult.Accepted)
            {
                await RevertStorageSubtaskAsync(frag, ct).ConfigureAwait(false);
                return;
            }

            CompleteSubtask(frag, scope, currentStorageRoot, stateRoot, flattened, pageResult);
        }

        private async Task<(StorageRangesMessage Response, bool Failed)> RequestAsync(
            SnapFragment.StorageSubtask frag,
            byte[] stateRoot,
            byte[] expectedStorageRoot,
            CancellationToken ct)
        {
            try
            {
                var response = await _client.Requests.GetStorageRangeAsync(
                    stateRoot, new List<byte[]> { frag.AccountHash }, frag.Next, 0,
                    r => expectedStorageRoot == null || SnapProofVerifier.VerifyStorageRangeResponse(expectedStorageRoot, frag.Next, r),
                    ct).ConfigureAwait(false);
                return (response, false);
            }
            catch (FetchRequestFailedException)
            {
                _markFailure?.Invoke();
                await RevertStorageSubtaskAsync(frag, ct).ConfigureAwait(false);
                return (null, true);
            }
        }

        private static RangePageResult ApplyPage(
            ResumableStorageScope scope,
            SnapFragment.StorageSubtask frag,
            byte[] currentStorageRoot,
            StorageSlots.FlattenedStoragePage flattened,
            AccountWorkerResult page)
        {
            var pageResult = scope.ApplyVerifiedPage(currentStorageRoot, frag.Next, flattened.SlotHashes, flattened.ValuesRlp, flattened.ProofNodes);
            if (pageResult.Accepted && page != null)
                foreach (var value in flattened.ValuesRlp) page.AddStorageSlot(value);
            return pageResult;
        }

        private void CompleteSubtask(
            SnapFragment.StorageSubtask frag,
            ResumableStorageScope scope,
            byte[] currentStorageRoot,
            byte[] stateRoot,
            StorageSlots.FlattenedStoragePage flattened,
            RangePageResult pageResult)
        {
            var crossedBoundary = flattened.SlotHashes.Count > 0
                && ByteArrayComparer.Current.Compare(pageResult.Cursor, frag.Last) > 0;
            var subtaskDone = !pageResult.HasMore || crossedBoundary;

            var ownerDrained = _taskSet.CompleteSubtask(
                frag, moreRemaining: !subtaskDone,
                nextCursor: subtaskDone ? null : pageResult.Cursor);

            if (subtaskDone && ownerDrained)
                CompleteOwner(scope, frag, currentStorageRoot);
        }

        private ISnapFlatStateWriter SinkFlatWriter
            => (_client.Sink as TrieSnapSyncSink)?.FlatWriter ?? (_client.Sink as FlatSnapSyncSink)?.FlatWriter;

        private ResumableStorageScope OpenScope(SnapFragment.StorageSubtask frag)
            => (ResumableStorageScope)_taskSet.GetOrCreateStorageScope(
                frag.TaskIndex, frag.AccountHash,
                () => ResumableStorageScope.Open(_storageNodeStore, frag.AccountHash, flatWriter: SinkFlatWriter));

        private void CompleteOwner(
            ResumableStorageScope scope,
            SnapFragment.StorageSubtask frag,
            byte[] expectedRoot)
        {
            _taskSet.CompleteOwnerStorageUnderGate(() =>
            {
                scope.CommitDirtyNodes();
                _taskSet.ClearStorageScope(frag.TaskIndex, frag.AccountHash);
                if (_taskSet.MarkNeedHealIfRootMismatch(frag.AccountHash, scope.CurrentRootHash, expectedRoot))
                {
                    _client.Logger.LogDebug(
                        "snap.phase2.storage.needheal account=0x{Account} reason=chunked_root_mismatch",
                        frag.AccountHash.ToHex());
                }
                _taskSet.MarkStorageCompleted(frag.AccountHash);
            });
        }

        private async Task<bool> ReproveIfRootRotatedAsync(
            SnapFragment.StorageSubtask frag,
            byte[] stateRoot,
            CancellationToken ct)
        {
            var claim = await ClaimReprove(frag, stateRoot, ct).ConfigureAwait(false);
            if (claim == SnapTaskSet.ReproveClaim.NotNeeded) return true;
            if (claim == SnapTaskSet.ReproveClaim.InFlight) return false;

            var committed = false;
            try
            {
                var resolution = await DeferredStorageDebtFinalRootResolver.ResolveAccountStorageRootAsync(
                    stateRoot, frag.AccountHash, _client.Requests.GetOwnerAccountRangeAsync, ct).ConfigureAwait(false);

                switch (resolution.Status)
                {
                    case AccountStorageResolutionStatus.Found:
                        _taskSet.CommitReproveFound(frag.TaskIndex, frag.AccountHash, stateRoot, resolution.StorageRoot);
                        committed = true;
                        return true;

                    case AccountStorageResolutionStatus.AbsentOrEmpty:
                        _taskSet.CommitReproveAbsentOrEmpty(frag.TaskIndex, frag.AccountHash, stateRoot);
                        committed = true;
                        CompleteAbsentOwner(frag);
                        return false;

                    default:
                        _taskSet.AbandonReprove(frag.TaskIndex, frag.AccountHash);
                        committed = true;
                        await RevertStorageSubtaskAsync(frag, ct).ConfigureAwait(false);
                        return false;
                }
            }
            finally
            {
                if (!committed) _taskSet.AbandonReprove(frag.TaskIndex, frag.AccountHash);
            }
        }

        private async Task<SnapTaskSet.ReproveClaim> ClaimReprove(
            SnapFragment.StorageSubtask frag,
            byte[] stateRoot,
            CancellationToken ct)
        {
            var claim = _taskSet.TryClaimReprove(frag.TaskIndex, frag.AccountHash, stateRoot);
            if (claim == SnapTaskSet.ReproveClaim.InFlight)
            {
                _taskSet.Revert(frag);
                await Task.Delay(ConsumerIdleWatch.IdleDelayMs, ct).ConfigureAwait(false);
            }
            return claim;
        }

        private void CompleteAbsentOwner(SnapFragment.StorageSubtask frag)
        {
            CompleteOwner(OpenScope(frag), frag, null);
        }

        private async Task RevertStorageSubtaskAsync(
            SnapFragment.StorageSubtask frag, CancellationToken ct)
        {
            _taskSet.Revert(frag);
            await Task.Delay(RetryBackoffMs, ct).ConfigureAwait(false);
        }
    }
}
