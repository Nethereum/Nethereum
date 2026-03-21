using System.Numerics;

namespace Nethereum.PrivacyPools
{
    public class PoolDepositEventData
    {
        public BigInteger Commitment { get; set; }
        public BigInteger Label { get; set; }
        public BigInteger Value { get; set; }
        public BigInteger PrecommitmentHash { get; set; }
        public string Depositor { get; set; } = "";
        public BigInteger BlockNumber { get; set; }
        public string TransactionHash { get; set; } = "";
    }

    public class PoolWithdrawalEventData
    {
        public BigInteger SpentNullifier { get; set; }
        public BigInteger NewCommitment { get; set; }
        public BigInteger Value { get; set; }
        public BigInteger BlockNumber { get; set; }
        public string TransactionHash { get; set; } = "";
    }

    public class PoolRagequitEventData
    {
        public BigInteger Commitment { get; set; }
        public BigInteger Label { get; set; }
        public BigInteger Value { get; set; }
        public BigInteger BlockNumber { get; set; }
        public string TransactionHash { get; set; } = "";
    }

    public class PoolLeafEventData
    {
        public BigInteger Leaf { get; set; }
        public BigInteger Index { get; set; }
        public BigInteger Root { get; set; }
    }
}
