using System.Linq;
using Nethereum.PrivacyPools.Entrypoint.ContractDefinition;
using Nethereum.PrivacyPools.PrivacyPoolBase;
using Nethereum.ZkProofs.Groth16;
using EntrypointWithdrawProof = Nethereum.PrivacyPools.Entrypoint.ContractDefinition.WithdrawProof;

namespace Nethereum.PrivacyPools
{
    public static class PrivacyPoolProofConverter
    {
        public static RagequitProof ToRagequitProof(Groth16ProofJson proof, RagequitProofSignals signals)
        {
            var (pA, pB, pC) = Groth16ProofConverter.ToSolidityProof(proof);

            return new RagequitProof
            {
                PA = pA,
                PB = pB,
                PC = pC,
                PubSignals = signals.ToArray().ToList()
            };
        }

        public static RagequitProof ToRagequitProof(string proofJson, RagequitProofSignals signals)
        {
            var parsed = Groth16ProofConverter.ParseProofJson(proofJson);
            return ToRagequitProof(parsed, signals);
        }

        public static EntrypointWithdrawProof ToWithdrawProof(Groth16ProofJson proof, WithdrawProofSignals signals)
        {
            var (pA, pB, pC) = Groth16ProofConverter.ToSolidityProof(proof);

            return new EntrypointWithdrawProof
            {
                PA = pA,
                PB = pB,
                PC = pC,
                PubSignals = signals.ToArray().ToList()
            };
        }

        public static EntrypointWithdrawProof ToWithdrawProof(string proofJson, WithdrawProofSignals signals)
        {
            var parsed = Groth16ProofConverter.ParseProofJson(proofJson);
            return ToWithdrawProof(parsed, signals);
        }
    }
}
