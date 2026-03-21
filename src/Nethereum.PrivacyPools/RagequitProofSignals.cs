using System.Numerics;

namespace Nethereum.PrivacyPools
{
    public class RagequitProofSignals
    {
        public BigInteger CommitmentHash { get; set; }
        public BigInteger NullifierHash { get; set; }
        public BigInteger Value { get; set; }
        public BigInteger Label { get; set; }

        public BigInteger[] ToArray()
        {
            return new[]
            {
                CommitmentHash,
                NullifierHash,
                Value,
                Label
            };
        }

        public static RagequitProofSignals FromArray(BigInteger[] signals)
        {
            if (signals == null || signals.Length != 4)
                throw new System.ArgumentException("Ragequit proof requires exactly 4 public signals");

            return new RagequitProofSignals
            {
                CommitmentHash = signals[0],
                NullifierHash = signals[1],
                Value = signals[2],
                Label = signals[3]
            };
        }
    }
}
