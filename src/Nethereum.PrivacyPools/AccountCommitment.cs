using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.PrivacyPools
{
    public class AccountCommitment
    {
        public PrivacyPoolCommitment Commitment { get; set; } = null!;
        public int LeafIndex { get; set; }
        public BigInteger BlockNumber { get; set; }
        public string TransactionHash { get; set; } = "";
        public BigInteger Timestamp { get; set; }

        public static AccountCommitment FromCommitment(PrivacyPoolCommitment commitment, int leafIndex,
            BigInteger blockNumber, string transactionHash)
        {
            return new AccountCommitment
            {
                Commitment = commitment,
                LeafIndex = leafIndex,
                BlockNumber = blockNumber,
                TransactionHash = transactionHash
            };
        }
    }

    public class PoolAccount
    {
        public BigInteger Scope { get; set; }
        public AccountCommitment Deposit { get; set; } = null!;
        public List<AccountCommitment> Withdrawals { get; set; } = new List<AccountCommitment>();
        public bool IsRagequitted { get; set; }
        public BigInteger RagequitBlockNumber { get; set; }

        public AccountCommitment LatestCommitment =>
            Withdrawals.Count > 0 ? Withdrawals[Withdrawals.Count - 1] : Deposit;

        public BigInteger SpendableValue => LatestCommitment.Commitment.Value;

        public bool IsSpendable => !IsRagequitted && SpendableValue > BigInteger.Zero;
    }
}
