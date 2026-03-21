using System.Numerics;

namespace Nethereum.PrivacyPools
{
    public class WithdrawProofSignals
    {
        public BigInteger NewCommitmentHash { get; set; }
        public BigInteger ExistingNullifierHash { get; set; }
        public BigInteger WithdrawnValue { get; set; }
        public BigInteger StateRoot { get; set; }
        public BigInteger StateTreeDepth { get; set; }
        public BigInteger ASPRoot { get; set; }
        public BigInteger ASPTreeDepth { get; set; }
        public BigInteger Context { get; set; }

        public BigInteger[] ToArray()
        {
            return new[]
            {
                NewCommitmentHash,
                ExistingNullifierHash,
                WithdrawnValue,
                StateRoot,
                StateTreeDepth,
                ASPRoot,
                ASPTreeDepth,
                Context
            };
        }

        public static WithdrawProofSignals FromArray(BigInteger[] signals)
        {
            if (signals == null || signals.Length != 8)
                throw new System.ArgumentException("Withdrawal proof requires exactly 8 public signals");

            return new WithdrawProofSignals
            {
                NewCommitmentHash = signals[0],
                ExistingNullifierHash = signals[1],
                WithdrawnValue = signals[2],
                StateRoot = signals[3],
                StateTreeDepth = signals[4],
                ASPRoot = signals[5],
                ASPTreeDepth = signals[6],
                Context = signals[7]
            };
        }
    }
}
