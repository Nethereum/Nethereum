using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Nethereum.PrivacyPools
{
    public static class PrivacyPoolAccountRecovery
    {
        public static List<PoolAccount> RecoverAccounts(
            PrivacyPoolAccount account,
            BigInteger scope,
            IEnumerable<PoolDepositEventData> deposits,
            IEnumerable<PoolWithdrawalEventData> withdrawals,
            IEnumerable<PoolRagequitEventData> ragequits,
            IEnumerable<PoolLeafEventData> leafInserts,
            int maxConsecutiveMisses = 10)
        {
            var depositsByPrecommitment = new Dictionary<BigInteger, PoolDepositEventData>();
            foreach (var evt in deposits)
                depositsByPrecommitment[evt.PrecommitmentHash] = evt;

            var withdrawalsByNullifier = new Dictionary<BigInteger, List<PoolWithdrawalEventData>>();
            foreach (var evt in withdrawals)
            {
                if (!withdrawalsByNullifier.ContainsKey(evt.SpentNullifier))
                    withdrawalsByNullifier[evt.SpentNullifier] = new List<PoolWithdrawalEventData>();
                withdrawalsByNullifier[evt.SpentNullifier].Add(evt);
            }

            var ragequitsByCommitment = new Dictionary<BigInteger, PoolRagequitEventData>();
            foreach (var evt in ragequits)
                ragequitsByCommitment[evt.Commitment] = evt;

            var leafIndexByCommitment = new Dictionary<BigInteger, int>();
            foreach (var evt in leafInserts)
                leafIndexByCommitment[evt.Leaf] = (int)evt.Index;

            var discovered = new List<PoolAccount>();
            int consecutiveMisses = 0;

            for (BigInteger depositIndex = 0; consecutiveMisses < maxConsecutiveMisses; depositIndex++)
            {
                var (nullifier, secret) = account.CreateDepositSecrets(scope, depositIndex);
                var precommitment = account.ComputePrecommitment(nullifier, secret);

                if (!depositsByPrecommitment.TryGetValue(precommitment, out var depositData))
                {
                    consecutiveMisses++;
                    continue;
                }

                consecutiveMisses = 0;

                var commitment = PrivacyPoolCommitment.Create(
                    depositData.Value, depositData.Label, nullifier, secret);

                int leafIndex = leafIndexByCommitment.ContainsKey(commitment.CommitmentHash)
                    ? leafIndexByCommitment[commitment.CommitmentHash]
                    : -1;

                var poolAccount = new PoolAccount
                {
                    Scope = scope,
                    Deposit = AccountCommitment.FromCommitment(
                        commitment, leafIndex, depositData.BlockNumber, depositData.TransactionHash)
                };

                RecoverWithdrawalChain(account, poolAccount, withdrawalsByNullifier, leafIndexByCommitment);

                if (ragequitsByCommitment.TryGetValue(commitment.CommitmentHash, out var ragequitData))
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
            var current = poolAccount.Deposit;
            var label = current.Commitment.Label;

            for (BigInteger childIndex = 0; ; childIndex++)
            {
                var nullifierHash = current.Commitment.NullifierHash;

                if (!withdrawalsByNullifier.TryGetValue(nullifierHash, out var matchedWithdrawals))
                    break;

                var withdrawal = matchedWithdrawals.FirstOrDefault();
                if (withdrawal == null)
                    break;

                var (wNullifier, wSecret) = account.CreateWithdrawalSecrets(label, childIndex);
                var newCommitment = PrivacyPoolCommitment.Create(
                    current.Commitment.Value - withdrawal.Value,
                    label, wNullifier, wSecret);

                if (newCommitment.CommitmentHash != withdrawal.NewCommitment)
                    break;

                int leafIndex = leafIndexByCommitment.ContainsKey(newCommitment.CommitmentHash)
                    ? leafIndexByCommitment[newCommitment.CommitmentHash]
                    : -1;

                poolAccount.Withdrawals.Add(AccountCommitment.FromCommitment(
                    newCommitment, leafIndex, withdrawal.BlockNumber, withdrawal.TransactionHash));

                current = poolAccount.Withdrawals[poolAccount.Withdrawals.Count - 1];
            }
        }

        public static List<PoolAccount> GetSpendable(IEnumerable<PoolAccount> accounts)
        {
            return accounts.Where(a => a.IsSpendable).ToList();
        }
    }
}
