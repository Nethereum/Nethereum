using System;
using System.Numerics;

namespace Nethereum.PrivacyPools
{
    public class RagequitWitnessInput
    {
        public BigInteger Nullifier { get; set; }
        public BigInteger Secret { get; set; }
        public BigInteger Value { get; set; }
        public BigInteger Label { get; set; }
    }

    public class RagequitProofResult
    {
        public string ProofJson { get; set; } = "";
        public string PublicJson { get; set; } = "";
        public RagequitProofSignals Signals { get; set; } = null!;
    }

    public class WithdrawalWitnessInput
    {
        public BigInteger ExistingValue { get; set; }
        public BigInteger ExistingNullifier { get; set; }
        public BigInteger ExistingSecret { get; set; }
        public BigInteger Label { get; set; }
        public BigInteger NewNullifier { get; set; }
        public BigInteger NewSecret { get; set; }
        public BigInteger WithdrawnValue { get; set; }
        public BigInteger StateRoot { get; set; }
        public BigInteger StateTreeDepth { get; set; }
        public BigInteger[] StateSiblings { get; set; } = Array.Empty<BigInteger>();
        public BigInteger StateIndex { get; set; }
        public BigInteger ASPRoot { get; set; }
        public BigInteger ASPTreeDepth { get; set; }
        public BigInteger[] ASPSiblings { get; set; } = Array.Empty<BigInteger>();
        public BigInteger ASPIndex { get; set; }
        public BigInteger Context { get; set; }
    }

    public class WithdrawalProofResult
    {
        public string ProofJson { get; set; } = "";
        public string PublicJson { get; set; } = "";
        public WithdrawProofSignals Signals { get; set; } = null!;
    }
}
