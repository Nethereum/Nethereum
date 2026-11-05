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
        internal const ulong AccountProofResponseBytes = 16_384;

        public static async Task<AccountStorageResolution> ResolveAccountStorageRootAsync(
            byte[] stateRoot,
            byte[] accountHash,
            Func<byte[], byte[], byte[], ulong, CancellationToken, Task<AccountRangeMessage>> fetchAccountRange,
            CancellationToken ct)
        {
            Guard(stateRoot, accountHash, fetchAccountRange);

            try
            {
                AccountRangeMessage response = await fetchAccountRange(
                        stateRoot, accountHash, accountHash, AccountProofResponseBytes, ct)
                    .ConfigureAwait(false);

                var (keys, values) = ReadLeaves(response);

                var proof = (IList<byte[]>)(response.Proof ?? new List<byte[]>());
                var proofResult = ProofVerification.Current.Range.Verify(stateRoot, accountHash, keys, values, proof);
                if (!proofResult.Valid) return AccountStorageResolution.Indeterminate;

                return FindStorageRoot(keys, values, accountHash);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                return AccountStorageResolution.Indeterminate;
            }
        }

        private static void Guard(
            byte[] stateRoot,
            byte[] accountHash,
            Func<byte[], byte[], byte[], ulong, CancellationToken, Task<AccountRangeMessage>> fetchAccountRange)
        {
            if (stateRoot == null || stateRoot.Length != 32)
                throw new ArgumentException("stateRoot must be 32 bytes", nameof(stateRoot));
            if (accountHash == null || accountHash.Length != 32)
                throw new ArgumentException("accountHash must be 32 bytes", nameof(accountHash));
            if (fetchAccountRange == null) throw new ArgumentNullException(nameof(fetchAccountRange));
        }

        private static (List<byte[]> Keys, List<byte[]> Values) ReadLeaves(AccountRangeMessage response)
        {
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
            return (keys, values);
        }

        private static AccountStorageResolution FindStorageRoot(
            List<byte[]> keys, List<byte[]> values, byte[] accountHash)
        {
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

        [Obsolete("No longer used: snap sync records no deferred storage debt; accounts that need heal are healed at the live pivot. Kept for 7.0 compatibility.")]
        public static Task ResolveAsync(
            byte[] finalStateRoot,
            ulong finalPivotBlock,
            IChainMetadataStore metadata,
            IFetchRequestScheduler scheduler,
            ILogger logger = null,
            CancellationToken ct = default)
            => Task.CompletedTask;

        [Obsolete("No longer used: snap sync records no deferred storage debt; accounts that need heal are healed at the live pivot. Kept for 7.0 compatibility.")]
        public static IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> BuildFinalStorageSeeds(
            IChainMetadataStore metadata)
            => Array.Empty<(byte[] AccountHash, byte[] StorageRoot)>();

        [Obsolete("No longer used: snap sync records no deferred storage debt; accounts that need heal are healed at the live pivot. Kept for 7.0 compatibility.")]
        public static void MarkFinalStorageSeedsDeepComplete(
            IChainMetadataStore metadata,
            IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> completedSeeds)
        {
        }

        [Obsolete("No longer used: snap sync records no deferred storage debt; accounts that need heal are healed at the live pivot. Kept for 7.0 compatibility.")]
        public static void EnsureDebtRowsForDeferredHealAccounts(
            IChainMetadataStore metadata,
            IReadOnlyList<SnapSyncClient.AccountNeedingHeal> accounts,
            byte[] fetchStateRoot,
            ulong? fetchPivotBlock)
        {
        }
    }
}
