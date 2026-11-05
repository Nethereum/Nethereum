using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    internal sealed class BatchStorageFetch
    {
        private const int MaxProofOnlyBackoffMs = 30_000;

        private readonly SnapSyncClient _client;
        private readonly StorageFetchContext _ctx;

        private List<(byte[] Hash, byte[] Root)> _remaining;
        private byte[] _dispatchRoot;
        private readonly Backoff _proofOnlyBackoff = new Backoff(SnapPeerRequests.RetryDelayMs);

        private enum RootStep { Proceed, Retry, Complete }

        public BatchStorageFetch(SnapSyncClient client, StorageFetchContext ctx)
        {
            _client = client;
            _ctx = ctx;
        }

        public async Task<bool> RunAsync(
            byte[] stateRoot,
            List<(byte[] Hash, byte[] Root)> storageAccounts,
            CancellationToken ct)
        {
            _remaining = storageAccounts;
            _dispatchRoot = stateRoot;
            _proofOnlyBackoff.Reset();
            while (_remaining.Count > 0)
            {
                ct.ThrowIfCancellationRequested();

                var step = await ReresolveAtLiveRootAsync(ct).ConfigureAwait(false);
                if (step == RootStep.Retry) continue;
                if (step == RootStep.Complete) return true;

                if (_ctx.Frozen.Diverged(_dispatchRoot))
                    return false;

                var (resp, failed) = await RequestPageAsync(ct).ConfigureAwait(false);
                if (failed) continue;

                int returned = resp.Slots.Count;
                if (returned == 0)
                {
                    await OnProofOnlyReplyAsync(ct).ConfigureAwait(false);
                    continue;
                }
                _proofOnlyBackoff.Reset();
                bool lastTruncated = resp.Proof != null && resp.Proof.Count > 0;

                await ApplyStorageRangePageAsync(
                    resp, _remaining, returned, lastTruncated, _dispatchRoot, ct).ConfigureAwait(false);

                _remaining = _remaining.GetRange(returned, _remaining.Count - returned);
            }
            return true;
        }

        private async Task<RootStep> ReresolveAtLiveRootAsync(CancellationToken ct)
        {
            var liveRoot = _ctx.GetLiveRoot != null ? _ctx.GetLiveRoot() : _dispatchRoot;
            if (liveRoot == null || ByteUtil.AreEqual(liveRoot, _dispatchRoot))
                return RootStep.Proceed;

            var (reresolved, allResolved) = await ReresolveStorageRootsAtLiveRootAsync(
                    liveRoot, _remaining, ct)
                .ConfigureAwait(false);
            _remaining = reresolved;
            if (!allResolved)
            {
                await Task.Delay(SnapPeerRequests.RetryDelayMs, ct).ConfigureAwait(false);
                return RootStep.Retry;
            }
            _dispatchRoot = liveRoot;
            _proofOnlyBackoff.Reset();
            return _remaining.Count == 0 ? RootStep.Complete : RootStep.Proceed;
        }

        private async Task<(StorageRangesMessage Response, bool Failed)> RequestPageAsync(CancellationToken ct)
        {
            var accountHashes = AccountHashesOf(_remaining);

            var dispatchedRemaining = _remaining;
            try
            {
                var response = await _client.Requests.GetStorageRangeAsync(
                    _dispatchRoot, accountHashes, SnapHashRanges.MinHash(), _ctx.ReqIds[0]++,
                    r => SnapProofVerifier.VerifyBatchStorageResponse(dispatchedRemaining, r),
                    ct).ConfigureAwait(false);
                return (response, false);
            }
            catch (FetchRequestFailedException)
            {
                _ctx.MarkFailure?.Invoke();
                await Task.Delay(SnapPeerRequests.RetryDelayMs, ct).ConfigureAwait(false);
                return (null, true);
            }
        }

        private async Task OnProofOnlyReplyAsync(CancellationToken ct)
        {
            await Task.Delay(_proofOnlyBackoff.Current, ct).ConfigureAwait(false);
            _proofOnlyBackoff.Advance(MaxProofOnlyBackoffMs);
        }

        private static List<byte[]> AccountHashesOf(List<(byte[] Hash, byte[] Root)> accounts)
        {
            var accountHashes = new List<byte[]>(accounts.Count);
            foreach (var a in accounts) accountHashes.Add(a.Hash);
            return accountHashes;
        }

        private async Task ApplyStorageRangePageAsync(
            StorageRangesMessage resp,
            List<(byte[] Hash, byte[] Root)> remaining,
            int returned,
            bool lastTruncated,
            byte[] dispatchRoot,
            CancellationToken ct)
        {
            for (int i = 0; i < returned; i++)
            {
                var (hash, root) = remaining[i];
                var slots = resp.Slots[i];
                bool isTruncatedWhale = lastTruncated && i == returned - 1;
                await ApplyAccountAsync(
                    hash, root, slots, isTruncatedWhale, dispatchRoot, ct).ConfigureAwait(false);
            }
        }

        private async Task ApplyAccountAsync(
            byte[] hash,
            byte[] root,
            List<StorageRangesMessage.SlotEntry> slots,
            bool isTruncatedWhale,
            byte[] dispatchRoot,
            CancellationToken ct)
        {
            if (isTruncatedWhale)
            {
                HandleTruncatedWhale(hash, root, slots, dispatchRoot);
                return;
            }

            await WriteVerifiedStoragePageAsync(hash, root, slots, ct).ConfigureAwait(false);
        }

        private void HandleTruncatedWhale(
            byte[] hash,
            byte[] root,
            List<StorageRangesMessage.SlotEntry> slots,
            byte[] dispatchRoot)
        {
            if (_ctx.TaskSet.IsStorageCompleted(_ctx.TaskIndex, hash) && !StoredStorageRootIs(hash, root))
            {
                _ctx.TaskSet.MarkNeedHeal(hash);
                _client.Logger.LogDebug(
                    "snap.phase2.storage.needheal account=0x{Account} reason=redelivered_root_changed", hash.ToHex());
                return;
            }

            _ctx.TaskSet.CreateLargeContract(
                _ctx.TaskIndex, hash, root, dispatchRoot,
                pageSlotCount: slots.Count,
                lastSlotHash: slots.Count > 0 ? slots[^1].Hash : null);
        }

        private bool StoredStorageRootIs(byte[] account, byte[] root)
        {
            var store = _ctx.StorageNodeStore;
            if (store == null) return true;
            if (store is IRawNodeReader raw)
                return TrieNodeVerification.RootBlobHashesTo(raw.TryGetRawNode(account, Array.Empty<byte>()), root);
            return store.ContainsKey(root);
        }

        private async Task WriteVerifiedStoragePageAsync(
            byte[] hash,
            byte[] root,
            List<StorageRangesMessage.SlotEntry> slots,
            CancellationToken ct)
        {
            var scope = await _client.Sink.BeginAccountStorageAsync(hash, root, ct).ConfigureAwait(false);
            await StorageSlots.WriteAsync(scope, slots, _ctx.Page, _ctx.MarkProductive, ct).ConfigureAwait(false);
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
                        liveRoot, hash, _client.Requests.GetOwnerAccountRangeAsync, ct)
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
    }
}
