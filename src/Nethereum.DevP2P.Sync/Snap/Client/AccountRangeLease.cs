using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using static Nethereum.DevP2P.Sync.Snap.Client.SnapSyncClient;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    internal sealed class AccountRangeLease
    {
        private readonly ConsumerContext _ctx;
        private readonly SnapSyncClient _client;
        private readonly SnapTaskSet _taskSet;
        private readonly int _consumerIdx;
        private readonly ConsumerState _state;
        private readonly AccountEncoder _accountDecoder;
        private readonly SnapFragment.AccountRange _frag;
        private readonly ActiveSnapLeaseInfo _activeLease;

        private bool _leaseOpen = true;
        private byte[] _currentRoot;
        private AccountRangeMessage _resp;
        private RangeProofResult? _verifiedResult;
        private List<byte[]> _verifiedCanonicalValues;
        private RangeProofResult _accountProofResult;
        private List<byte[]> _canonicalValues;
        private bool _rangeCompletedByBoundary;

        public AccountRangeLease(
            ConsumerContext ctx, int consumerIdx, ConsumerState state,
            AccountEncoder accountDecoder, SnapFragment.AccountRange frag)
        {
            _ctx = ctx;
            _client = ctx.Client;
            _taskSet = ctx.TaskSet;
            _consumerIdx = consumerIdx;
            _state = state;
            _accountDecoder = accountDecoder;
            _frag = frag;
            _activeLease = ActiveSnapLeaseInfo.Create(_consumerIdx, frag);
            _ctx.Leases[_consumerIdx] = _activeLease;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            try
            {
                _currentRoot = _ctx.AttemptRoot;
                if (RevertIfRootDiverged()) return;
                if (!await FetchRangeAsync(ct).ConfigureAwait(false)) return;
                if (await CompleteIfProofOnlyReplyAsync(ct).ConfigureAwait(false)) return;
                ThrowIfProofInvalid();
                if (TrimAndCompleteIfNoAccountsLeft()) return;
                LogVerified();
                var build = await BuildPageAsync(ct).ConfigureAwait(false);
                MarkProductive();
                if (!await FetchPageStorageAsync(build.Page, build.Storage, ct).ConfigureAwait(false)) return;
                await CommitPageAsync(build, ct).ConfigureAwait(false);
            }
            catch
            {
                if (_leaseOpen)
                    _taskSet.Revert(_frag);
                throw;
            }
            finally
            {
                _ctx.Leases.TryRemove(_consumerIdx, out _);
            }
        }

        private void MarkProductive()
        {
            _activeLease.MarkProductive();
            _state.SameStageFailures = 0;
        }

        private void MarkFailure()
        {
            _state.SameStageFailures++;
            if (_client.ActiveLeaseSameStageFailureBudget > 0 && _state.SameStageFailures >= _client.ActiveLeaseSameStageFailureBudget)
                throw new SnapTaskLeaseStalledException(
                    TimeSpan.FromMilliseconds(Math.Max(0, Environment.TickCount64 - _activeLease.LastProductiveTick)),
                    Phase2Watchdog.SnapshotActiveLeases(_ctx.Leases),
                    _taskSet.GetDiagnostics());
        }

        private bool RevertIfRootDiverged()
        {
            if (!_ctx.Frozen.Diverged(_currentRoot))
                return false;
            _taskSet.Revert(_frag);
            _leaseOpen = false;
            return true;
        }

        private async Task<bool> FetchRangeAsync(CancellationToken ct)
        {
            try
            {
                _resp = await _client.Peer.GetAccountRangeAsync(new GetAccountRangeMessage
                {
                    RequestId = _state.ReqId++,
                    RootHash = _currentRoot,
                    StartingHash = _frag.Origin,
                    LimitHash = _frag.Limit,
                    ResponseBytes = _client.ResponseBytesBudget
                },
                verifyResponse: r =>
                {
                    var res = SnapProofVerifier.VerifyAccountRangeResponse(_currentRoot, _frag.Origin, r, out var vals);
                    if (res.Valid) { _verifiedResult = res; _verifiedCanonicalValues = vals; }
                    return res.Valid;
                },
                ct).ConfigureAwait(false);
            }
            catch (FetchRequestFailedException ex)
            {
                _activeLease.MarkActivity();
                _taskSet.Revert(_frag);
                _leaseOpen = false;
                if (++_state.FetchFailures % 3 == 0)
                    _client.Logger.LogWarning(
                        "snap.consumer.fetch_failed consumer={Consumer} failures={Failures} stage_failures={StageFailures} last={Error} - retrying account-range",
                        _consumerIdx, _state.FetchFailures, _state.SameStageFailures + 1, ex.InnerException?.Message ?? ex.Message);
                MarkFailure();
                await Task.Delay(SnapPeerRequests.RetryDelayMs, ct).ConfigureAwait(false);
                return false;
            }
            _activeLease.MarkActivity();
            return true;
        }

        private async Task<bool> CompleteIfProofOnlyReplyAsync(CancellationToken ct)
        {
            _accountProofResult = ResolveAccountProof(out _canonicalValues);
            if (_resp.Accounts.Count != 0) return false;
            if (_accountProofResult.Valid)
            {
                CompleteRangeWithoutAccounts();
            }
            else
            {
                _taskSet.Revert(_frag);
                _leaseOpen = false;
                MarkFailure();
                await Task.Delay(SnapPeerRequests.RetryDelayMs, ct).ConfigureAwait(false);
            }
            return true;
        }

        private void ThrowIfProofInvalid()
        {
            if (!_accountProofResult.Valid)
                throw new InvalidOperationException(
                    $"Snap account-range proof failed for state root 0x{_currentRoot.ToHex()} " +
                    $"starting at 0x{_frag.Origin.ToHex()} ({_resp.Accounts.Count} entries) — peer returned tampered or malformed data");
        }

        private bool TrimAndCompleteIfNoAccountsLeft()
        {
            _rangeCompletedByBoundary = TrimAccountsBeyondLimit();
            if (_resp.Accounts.Count != 0) return false;
            CompleteRangeWithoutAccounts();
            return true;
        }

        private void CompleteRangeWithoutAccounts()
        {
            MarkProductive();
            _taskSet.CompleteAccountRange(_frag, Array.Empty<AccountClassification>(), null, done: true);
            _leaseOpen = false;
        }

        private void LogVerified()
        {
            _client.Logger.LogDebug(
                "snap.verify c={C} root=0x{Root} from=0x{From} received={Recv} proof=valid hasMore={More}",
                _consumerIdx, _currentRoot.ToHex(), _frag.Origin.ToHex(), _resp.Accounts.Count, _accountProofResult.HasMore);
        }

        private async Task<bool> FetchPageStorageAsync(
            AccountWorkerResult page, List<(byte[] Hash, byte[] Root)> pageStorage, CancellationToken ct)
        {
            if (pageStorage.Count == 0) return true;
            var storageFullyProcessed = await new BatchStorageFetch(
                    _client,
                    StorageFetchContext.Create(_ctx, _state, page, _frag.TaskIndex, MarkProductive, MarkFailure))
                .RunAsync(_currentRoot, pageStorage, ct)
                .ConfigureAwait(false);
            if (storageFullyProcessed) return true;
            _taskSet.Revert(_frag);
            _leaseOpen = false;
            return false;
        }

        private async Task CommitPageAsync(AccountPageBuild build, CancellationToken ct)
        {
            var page = build.Page;
            var chunkBytes = build.ChunkBytes;
            chunkBytes += page.StorageBytesDelta;

            chunkBytes += await FetchAndPublishBytecodesAsync(_ctx, _state, build.CodeHashes, MarkProductive, ct).ConfigureAwait(false);

            FlushAccountTrieForCheckpoint();

            _client.Logger.LogDebug(
                "snap.committed c={C} root=0x{Root} accounts={Acc} slots={Slots}",
                _consumerIdx, _currentRoot.ToHex(), _resp.Accounts.Count, page.StorageSlotsSyncedDelta);

            _ctx.State.PublishAccountDeltas(page);
            for (ulong k = 0; k < page.AccountsSyncedDelta; k++) _ctx.AddAccount();

            var lastHash = _resp.Accounts[^1].Hash;
            bool done = AccountRangeReachedEnd(lastHash, _frag, _accountProofResult, _rangeCompletedByBoundary);
            _taskSet.CompleteAccountRange(_frag, Array.Empty<AccountClassification>(),
                done ? null : lastHash, done);
            _leaseOpen = false;
            _ctx.Checkpointer.MaybeCheckpoint(chunkBytes);
        }

        private RangeProofResult ResolveAccountProof(out List<byte[]> canonicalValues)
        {
            if (_verifiedResult.HasValue)
            {
                canonicalValues = _verifiedCanonicalValues;
                return _verifiedResult.Value;
            }
            return SnapProofVerifier.VerifyAccountRangeResponse(_currentRoot, _frag.Origin, _resp, out canonicalValues);
        }

        private bool TrimAccountsBeyondLimit()
        {
            bool rangeCompletedByBoundary = false;
            for (int i = 0; i < _resp.Accounts.Count; i++)
            {
                int cmp = ByteArrayComparer.Current.Compare(_resp.Accounts[i].Hash, _frag.Limit);
                if (cmp == 0)
                {
                    rangeCompletedByBoundary = true;
                    continue;
                }
                if (cmp > 0)
                {
                    _resp.Accounts.RemoveRange(i, _resp.Accounts.Count - i);
                    _canonicalValues.RemoveRange(i, _canonicalValues.Count - i);
                    rangeCompletedByBoundary = true;
                    break;
                }
            }
            return rangeCompletedByBoundary;
        }

        private async Task<AccountPageBuild> BuildPageAsync(CancellationToken ct)
        {
            var page = new AccountWorkerResult();
            var pageStorage = new List<(byte[] Hash, byte[] Root)>();
            var pageCodeHashes = new List<byte[]>();
            ulong chunkBytes = 0;
            for (int i = 0; i < _resp.Accounts.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var entry = _resp.Accounts[i];

                await _client.Sink.WriteAccountAsync(entry.Hash, entry.Body, ct).ConfigureAwait(false);
                page.AccountsSyncedDelta++;
                var slimLen = entry.Body == null ? 0 : (ulong)entry.Body.Length;
                page.AccountBytesDelta += slimLen;
                chunkBytes += slimLen;

                var decoded = _accountDecoder.Decode(_canonicalValues[i]);
                if (!ByteUtil.AreEqual(decoded.StateRoot, DefaultValues.EMPTY_TRIE_HASH))
                    pageStorage.Add((entry.Hash, decoded.StateRoot));
                if (!ByteUtil.AreEqual(decoded.CodeHash, DefaultValues.EMPTY_DATA_HASH))
                    pageCodeHashes.Add(decoded.CodeHash);
            }
            return new AccountPageBuild(page, pageStorage, pageCodeHashes, chunkBytes);
        }

        internal static async Task<ulong> FetchAndPublishBytecodesAsync(
            ConsumerContext ctx, ConsumerState state, List<byte[]> pageCodeHashes, Action markProductive, CancellationToken ct)
        {
            var codeResult = await new ClientBytecodeFetch(ctx.Client).RunAsync(pageCodeHashes, state.ReqId, markProductive, ct).ConfigureAwait(false);
            ctx.State.PublishBytecodes(codeResult.Count, codeResult.Bytes);
            if (codeResult.Deferred.Count > 0) ctx.State.PublishDeferredCode(codeResult.Deferred);
            return codeResult.Bytes;
        }

        private bool AccountRangeReachedEnd(
            byte[] lastHash, SnapFragment.AccountRange frag,
            RangeProofResult accountProofResult, bool rangeCompletedByBoundary)
            => rangeCompletedByBoundary
                || !accountProofResult.HasMore
                || ByteArrayComparer.Current.Compare(lastHash, frag.Limit) >= 0;

        private void FlushAccountTrieForCheckpoint()
        {
            if (_client.Sink is TrieSnapSyncSink trieSink)
            {
                trieSink.FlushAccountTrieForCheckpoint();
                _client.Logger.LogDebug("snap.phase2.account_obligation.completed state=account_trie_nodes_written");
            }
        }

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
