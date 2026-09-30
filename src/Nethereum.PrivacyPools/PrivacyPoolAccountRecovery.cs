using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Nethereum.PrivacyPools
{
    public static class PrivacyPoolAccountRecovery
    {
        // Pre-built event lookups shared between RecoverAccounts and DiscoverMigratedCommitments
        // to avoid iterating the same event collections twice.
        internal class EventLookups
        {
            public Dictionary<BigInteger, PoolDepositEventData> DepositsByPrecommitment { get; set; }
            public Dictionary<BigInteger, List<PoolWithdrawalEventData>> WithdrawalsByNullifier { get; set; }
            public Dictionary<BigInteger, PoolWithdrawalEventData> WithdrawalsByNewCommitment { get; set; }
            public Dictionary<BigInteger, PoolRagequitEventData> RagequitsByLabel { get; set; }
            public Dictionary<BigInteger, int> LeafIndexByCommitment { get; set; }
        }

        internal static EventLookups BuildLookups(
            IEnumerable<PoolDepositEventData> deposits,
            IEnumerable<PoolWithdrawalEventData> withdrawals,
            IEnumerable<PoolRagequitEventData> ragequits,
            IEnumerable<PoolLeafEventData> leafInserts)
        {
            var lookups = new EventLookups
            {
                DepositsByPrecommitment = new Dictionary<BigInteger, PoolDepositEventData>(),
                WithdrawalsByNullifier = new Dictionary<BigInteger, List<PoolWithdrawalEventData>>(),
                WithdrawalsByNewCommitment = new Dictionary<BigInteger, PoolWithdrawalEventData>(),
                RagequitsByLabel = new Dictionary<BigInteger, PoolRagequitEventData>(),
                LeafIndexByCommitment = new Dictionary<BigInteger, int>()
            };

            foreach (var evt in deposits)
            {
                if (!lookups.DepositsByPrecommitment.TryGetValue(evt.PrecommitmentHash, out var existing) ||
                    evt.BlockNumber < existing.BlockNumber)
                {
                    lookups.DepositsByPrecommitment[evt.PrecommitmentHash] = evt;
                }
            }

            foreach (var evt in withdrawals)
            {
                if (!lookups.WithdrawalsByNullifier.ContainsKey(evt.SpentNullifier))
                    lookups.WithdrawalsByNullifier[evt.SpentNullifier] = new List<PoolWithdrawalEventData>();
                lookups.WithdrawalsByNullifier[evt.SpentNullifier].Add(evt);

                if (!lookups.WithdrawalsByNewCommitment.TryGetValue(evt.NewCommitment, out var existing) ||
                    evt.BlockNumber < existing.BlockNumber)
                {
                    lookups.WithdrawalsByNewCommitment[evt.NewCommitment] = evt;
                }
            }

            // SDK v1.2.0 matches ragequits by label, not by commitment hash.
            foreach (var evt in ragequits)
            {
                if (!lookups.RagequitsByLabel.TryGetValue(evt.Label, out var existing) ||
                    evt.BlockNumber < existing.BlockNumber)
                {
                    lookups.RagequitsByLabel[evt.Label] = evt;
                }
            }

            foreach (var evt in leafInserts)
                lookups.LeafIndexByCommitment[evt.Leaf] = (int)evt.Index;

            return lookups;
        }

        public static List<PoolAccount> RecoverAccounts(
            PrivacyPoolAccount account,
            BigInteger scope,
            IEnumerable<PoolDepositEventData> deposits,
            IEnumerable<PoolWithdrawalEventData> withdrawals,
            IEnumerable<PoolRagequitEventData> ragequits,
            IEnumerable<PoolLeafEventData> leafInserts,
            int maxConsecutiveMisses = 10,
            int startIndex = 0)
        {
            var lookups = BuildLookups(deposits, withdrawals, ragequits, leafInserts);
            return RecoverAccountsFromLookups(account, scope, lookups, maxConsecutiveMisses, startIndex);
        }

        internal static List<PoolAccount> RecoverAccountsFromLookups(
            PrivacyPoolAccount account,
            BigInteger scope,
            EventLookups lookups,
            int maxConsecutiveMisses,
            int startIndex = 0)
        {
            var discovered = new List<PoolAccount>();
            int consecutiveMisses = 0;

            for (BigInteger depositIndex = startIndex; consecutiveMisses < maxConsecutiveMisses; depositIndex++)
            {
                var (nullifier, secret) = account.CreateDepositSecrets(scope, depositIndex);
                var precommitment = account.ComputePrecommitment(nullifier, secret);

                if (!lookups.DepositsByPrecommitment.TryGetValue(precommitment, out var depositData))
                {
                    consecutiveMisses++;
                    continue;
                }

                consecutiveMisses = 0;

                var commitment = PrivacyPoolCommitment.Create(
                    depositData.Value, depositData.Label, nullifier, secret);

                int leafIndex = lookups.LeafIndexByCommitment.ContainsKey(commitment.CommitmentHash)
                    ? lookups.LeafIndexByCommitment[commitment.CommitmentHash]
                    : -1;

                var poolAccount = new PoolAccount
                {
                    Scope = scope,
                    Deposit = AccountCommitment.FromCommitment(
                        commitment, leafIndex, depositData.BlockNumber, depositData.TransactionHash)
                };

                RecoverWithdrawalChain(account, poolAccount, lookups.WithdrawalsByNullifier, lookups.LeafIndexByCommitment);

                if (lookups.RagequitsByLabel.TryGetValue(poolAccount.Deposit.Commitment.Label, out var ragequitData))
                {
                    poolAccount.IsRagequitted = true;
                    poolAccount.RagequitBlockNumber = ragequitData.BlockNumber;
                }

                discovered.Add(poolAccount);
            }

            return discovered;
        }

        private static void RecoverWithdrawalChain(
            PrivacyPoolAccount account,
            PoolAccount poolAccount,
            Dictionary<BigInteger, List<PoolWithdrawalEventData>> withdrawalsByNullifier,
            Dictionary<BigInteger, int> leafIndexByCommitment)
        {
            var current = poolAccount.LatestCommitment;
            var label = poolAccount.Deposit.Commitment.Label;

            for (BigInteger childIndex = poolAccount.Withdrawals.Count; ; childIndex++)
            {
                var nullifierHash = current.Commitment.NullifierHash;

                if (!withdrawalsByNullifier.TryGetValue(nullifierHash, out var matchedWithdrawals) ||
                    matchedWithdrawals.Count == 0)
                    break;

                var withdrawal = matchedWithdrawals.First();

                var (wNullifier, wSecret) = account.CreateWithdrawalSecrets(label, childIndex);
                var newCommitment = PrivacyPoolCommitment.Create(
                    current.Commitment.Value - withdrawal.Value,
                    label, wNullifier, wSecret);

                if (newCommitment.CommitmentHash != withdrawal.NewCommitment)
                {
                    poolAccount.Withdrawals.Add(AccountCommitment.FromCommitment(
                        newCommitment, -1, withdrawal.BlockNumber, withdrawal.TransactionHash,
                        isMigration: true));
                    poolAccount.IsMigrated = true;
                    current = poolAccount.Withdrawals[poolAccount.Withdrawals.Count - 1];
                    continue;
                }

                int leafIndex = leafIndexByCommitment.ContainsKey(newCommitment.CommitmentHash)
                    ? leafIndexByCommitment[newCommitment.CommitmentHash]
                    : -1;

                poolAccount.Withdrawals.Add(AccountCommitment.FromCommitment(
                    newCommitment, leafIndex, withdrawal.BlockNumber, withdrawal.TransactionHash));

                current = poolAccount.Withdrawals[poolAccount.Withdrawals.Count - 1];
            }
        }

        // Finds legacy accounts whose last commitment was spent via a key-rotation withdrawal
        // (migration from legacy to safe keys). Mirrors the TS SDK v1.2.0 migration discovery.
        internal static List<PoolAccount> DiscoverMigratedCommitments(
            PrivacyPoolAccount safeAccount,
            BigInteger scope,
            IEnumerable<PoolAccount> legacyAccounts,
            EventLookups lookups)
        {
            var migrated = new List<PoolAccount>();

            foreach (var legacy in legacyAccounts)
            {
                if (!legacy.IsMigrated)
                    continue;

                var migrationCommitment = legacy.Withdrawals.FirstOrDefault(w => w.IsMigration);
                if (migrationCommitment == null)
                    continue;

                var label = legacy.Deposit.Commitment.Label;
                var remainingValue = migrationCommitment.Commitment.Value;
                var (nullifier, secret) = safeAccount.CreateWithdrawalSecrets(label, BigInteger.Zero);
                var commitment = PrivacyPoolCommitment.Create(remainingValue, label, nullifier, secret);

                if (!lookups.WithdrawalsByNewCommitment.TryGetValue(commitment.CommitmentHash, out var spendingWithdrawal))
                    continue;

                int leafIndex = lookups.LeafIndexByCommitment.ContainsKey(commitment.CommitmentHash)
                    ? lookups.LeafIndexByCommitment[commitment.CommitmentHash]
                    : -1;

                var poolAccount = new PoolAccount
                {
                    Scope = scope,
                    Deposit = AccountCommitment.FromCommitment(
                        commitment, leafIndex, spendingWithdrawal.BlockNumber, spendingWithdrawal.TransactionHash)
                };

                // Reserve withdrawal index 0 for the migration itself so subsequent
                // safe-key withdrawals derive the same child indices as the v1.2.0 SDK.
                poolAccount.Withdrawals.Add(AccountCommitment.FromCommitment(
                    commitment, leafIndex, spendingWithdrawal.BlockNumber, spendingWithdrawal.TransactionHash));

                RecoverWithdrawalChain(safeAccount, poolAccount, lookups.WithdrawalsByNullifier, lookups.LeafIndexByCommitment);

                if (lookups.RagequitsByLabel.TryGetValue(label, out var ragequitData))
                {
                    poolAccount.IsRagequitted = true;
                    poolAccount.RagequitBlockNumber = ragequitData.BlockNumber;
                }

                migrated.Add(poolAccount);
            }

            return migrated;
        }

        public static List<PoolAccount> DiscoverMigratedCommitments(
            PrivacyPoolAccount safeAccount,
            BigInteger scope,
            IEnumerable<PoolAccount> legacyAccounts,
            IEnumerable<PoolWithdrawalEventData> withdrawals,
            IEnumerable<PoolLeafEventData> leafInserts,
            IEnumerable<PoolRagequitEventData> ragequits)
        {
            var lookups = BuildLookups(
                new List<PoolDepositEventData>(), withdrawals, ragequits, leafInserts);
            return DiscoverMigratedCommitments(safeAccount, scope, legacyAccounts, lookups);
        }

        public static List<PoolAccount> GetSpendable(IEnumerable<PoolAccount> accounts)
        {
            return accounts.Where(a => a.IsSpendable).ToList();
        }
    }
}
