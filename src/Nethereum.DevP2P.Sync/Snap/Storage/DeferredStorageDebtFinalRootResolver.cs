using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.Snap.Storage
{
    public enum AccountStorageResolutionStatus { Found, AbsentOrEmpty, Indeterminate }

    public readonly struct AccountStorageResolution
    {
        public AccountStorageResolutionStatus Status { get; }
        public byte[] StorageRoot { get; }

        private AccountStorageResolution(AccountStorageResolutionStatus status, byte[] storageRoot)
        {
            Status = status;
            StorageRoot = storageRoot;
        }

        public static AccountStorageResolution Found(byte[] storageRoot) =>
            new AccountStorageResolution(AccountStorageResolutionStatus.Found, storageRoot);

        public static readonly AccountStorageResolution AbsentOrEmpty =
            new AccountStorageResolution(AccountStorageResolutionStatus.AbsentOrEmpty, null);

        public static readonly AccountStorageResolution Indeterminate =
            new AccountStorageResolution(AccountStorageResolutionStatus.Indeterminate, null);
    }

    public static class DeferredStorageDebtFinalRootResolver
    {
        private const ulong AccountProofResponseBytes = 16_384;

        public static async Task<AccountStorageResolution> ResolveAccountStorageRootAsync(
            byte[] stateRoot,
            byte[] accountHash,
            Func<byte[], byte[], byte[], ulong, CancellationToken, Task<AccountRangeMessage>> fetchAccountRange,
            CancellationToken ct)
        {
            if (stateRoot == null || stateRoot.Length != 32)
                throw new ArgumentException("stateRoot must be 32 bytes", nameof(stateRoot));
            if (accountHash == null || accountHash.Length != 32)
                throw new ArgumentException("accountHash must be 32 bytes", nameof(accountHash));
            if (fetchAccountRange == null) throw new ArgumentNullException(nameof(fetchAccountRange));

            try
            {
                AccountRangeMessage response = await fetchAccountRange(
                        stateRoot, accountHash, accountHash, AccountProofResponseBytes, ct)
                    .ConfigureAwait(false);

                var keys = new List<byte[]>(response.Accounts?.Count ?? 0);
                var values = new List<byte[]>(response.Accounts?.Count ?? 0);
                if (response.Accounts != null)
                {
                    foreach (var entry in response.Accounts)
                    {
                        if (entry.Hash == null || entry.Hash.Length != 32 || entry.Body == null)
                            continue;
                        keys.Add(entry.Hash);
                        values.Add(SlimAccountEncoder.FromSlim(entry.Body));
                    }
                }

                var proof = (IList<byte[]>)(response.Proof ?? new List<byte[]>());
                var proofResult = ProofVerification.Current.Range.Verify(stateRoot, accountHash, keys, values, proof);
                if (!proofResult.Valid) return AccountStorageResolution.Indeterminate;

                var match = -1;
                for (int i = 0; i < keys.Count; i++)
                {
                    if (ByteUtil.AreEqual(keys[i], accountHash))
                    {
                        match = i;
                        break;
                    }
                }
                if (match < 0) return AccountStorageResolution.AbsentOrEmpty;

                var account = new AccountEncoder().Decode(values[match]);
                if (account.StateRoot == null
                    || account.StateRoot.Length != 32
                    || ByteUtil.AreEqual(account.StateRoot, DefaultValues.EMPTY_TRIE_HASH))
                    return AccountStorageResolution.AbsentOrEmpty;

                return AccountStorageResolution.Found((byte[])account.StateRoot.Clone());
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                return AccountStorageResolution.Indeterminate;
            }
        }

        public static async Task ResolveAsync(
            byte[] finalStateRoot,
            ulong finalPivotBlock,
            IChainMetadataStore metadata,
            IFetchRequestScheduler scheduler,
            ILogger logger = null,
            CancellationToken ct = default)
        {
            if (finalStateRoot == null || finalStateRoot.Length != 32)
                throw new ArgumentException("finalStateRoot must be 32 bytes", nameof(finalStateRoot));
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));

            var debts = metadata.ListOpenDeferredStorageDebts();
            foreach (var debt in debts)
            {
                ct.ThrowIfCancellationRequested();

                var resolution = await ResolveAccountStorageRootAsync(
                        finalStateRoot, debt.AccountHash, scheduler.FetchAccountRangeAsync, ct)
                    .ConfigureAwait(false);

                if (resolution.Status == AccountStorageResolutionStatus.Indeterminate)
                {
                    logger?.LogWarning(
                        "snap.deferred.resolve account=0x{Account} final_root=0x{Root} unresolved (fetch failed or proof invalid); debt remains open",
                        debt.AccountHash.ToHex(), finalStateRoot.ToHex());
                    continue;
                }

                var updated = debt.Clone();
                updated.FinalStateRoot = (byte[])finalStateRoot.Clone();

                if (resolution.Status == AccountStorageResolutionStatus.AbsentOrEmpty)
                {
                    updated.Status = StorageCompleteness.ProofDropped;
                    updated.FinalStorageRoot = null;
                    metadata.UpsertDeferredStorageDebt(updated);
                    continue;
                }

                updated.Status = StorageCompleteness.FinalRootReResolved;
                updated.FinalStorageRoot = resolution.StorageRoot;
                metadata.UpsertDeferredStorageDebt(updated);
            }
        }

        public static IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> BuildFinalStorageSeeds(
            IChainMetadataStore metadata)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            var debts = metadata.ListOpenDeferredStorageDebts();
            var seeds = new List<(byte[] AccountHash, byte[] StorageRoot)>();
            foreach (var debt in debts)
            {
                if (debt.Status != StorageCompleteness.FinalRootReResolved
                    || debt.FinalStorageRoot == null
                    || debt.FinalStorageRoot.Length != 32)
                    continue;
                seeds.Add(((byte[])debt.AccountHash.Clone(), (byte[])debt.FinalStorageRoot.Clone()));
            }
            return seeds;
        }

        public static void MarkFinalStorageSeedsDeepComplete(
            IChainMetadataStore metadata,
            IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> completedSeeds)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            if (completedSeeds == null || completedSeeds.Count == 0) return;

            var openDebts = metadata.ListOpenDeferredStorageDebts();
            foreach (var debt in openDebts)
            {
                if (debt.Status != StorageCompleteness.FinalRootReResolved
                    || debt.FinalStorageRoot == null
                    || debt.FinalStorageRoot.Length != 32)
                    continue;

                var completed = false;
                foreach (var seed in completedSeeds)
                {
                    if (ByteUtil.AreEqual(debt.AccountHash, seed.AccountHash)
                        && ByteUtil.AreEqual(debt.FinalStorageRoot, seed.StorageRoot))
                    {
                        completed = true;
                        break;
                    }
                }
                if (!completed) continue;

                var updated = debt.Clone();
                updated.Status = StorageCompleteness.DeepComplete;
                metadata.UpsertDeferredStorageDebt(updated);
            }
        }
        public static void EnsureDebtRowsForDeferredHealAccounts(
            IChainMetadataStore metadata,
            IReadOnlyList<SnapSyncClient.AccountNeedingHeal> accounts,
            byte[] fetchStateRoot,
            ulong? fetchPivotBlock)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            if (accounts == null || accounts.Count == 0) return;
            if (fetchStateRoot == null || fetchStateRoot.Length != 32)
                throw new ArgumentException("fetchStateRoot must be 32 bytes", nameof(fetchStateRoot));

            var openDebts = metadata.ListOpenDeferredStorageDebts();
            foreach (var account in accounts)
            {
                var alreadyTracked = false;
                foreach (var existing in openDebts)
                {
                    if (ByteUtil.AreEqual(existing.AccountHash, account.AccountHash)
                        && ByteUtil.AreEqual(existing.DiscoveredStorageRoot, account.ExpectedStorageRoot))
                    {
                        alreadyTracked = true;
                        break;
                    }
                }
                if (alreadyTracked)
                    continue;

                metadata.UpsertDeferredStorageDebt(new DeferredStorageDebt
                {
                    AccountHash = (byte[])account.AccountHash.Clone(),
                    DiscoveredStorageRoot = (byte[])account.ExpectedStorageRoot.Clone(),
                    FetchStateRoot = (byte[])fetchStateRoot.Clone(),
                    FetchPivotBlock = fetchPivotBlock,
                    Reason = DeferredStorageReason.BigAccountSubrangeFailed,
                    Status = StorageCompleteness.DeferredBigAccount,
                });
            }
        }
    }
}
