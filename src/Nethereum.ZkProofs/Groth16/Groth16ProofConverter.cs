using System;
using System.Collections.Generic;
using System.Numerics;
#if NET6_0_OR_GREATER
using System.Text.Json;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.ZkProofs.Groth16
{
    public static class Groth16ProofConverter
    {
        public static Groth16ProofJson ParseProofJson(string proofJson)
        {
            if (string.IsNullOrEmpty(proofJson))
                throw new ArgumentException("Proof JSON is null or empty", nameof(proofJson));

#if NET6_0_OR_GREATER
            return JsonSerializer.Deserialize<Groth16ProofJson>(proofJson)
                ?? throw new InvalidOperationException("Failed to parse Groth16 proof JSON");
#else
            return JsonConvert.DeserializeObject<Groth16ProofJson>(proofJson)
                ?? throw new InvalidOperationException("Failed to parse Groth16 proof JSON");
#endif
        }

        public static BigInteger[] ParsePublicSignals(string publicJson)
        {
            return ZkProofResult.ParsePublicSignals(publicJson);
        }

        public static (List<BigInteger> pA, List<List<BigInteger>> pB, List<BigInteger> pC) ToSolidityProof(Groth16ProofJson proof)
        {
            if (proof == null) throw new ArgumentNullException(nameof(proof));

            var pA = new List<BigInteger>
            {
                ParseFieldElement(proof.PiA[0]),
                ParseFieldElement(proof.PiA[1])
            };

            var pB = new List<List<BigInteger>>
            {
                new List<BigInteger> { ParseFieldElement(proof.PiB[0][1]), ParseFieldElement(proof.PiB[0][0]) },
                new List<BigInteger> { ParseFieldElement(proof.PiB[1][1]), ParseFieldElement(proof.PiB[1][0]) }
            };

            var pC = new List<BigInteger>
            {
                ParseFieldElement(proof.PiC[0]),
                ParseFieldElement(proof.PiC[1])
            };

            return (pA, pB, pC);
        }

        private static BigInteger ParseFieldElement(string text)
        {
            return BigInteger.Parse(Nethereum.ZkProofsVerifier.Circom.FieldElementText.RequireBoundedDecimal(text), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);
        }

        public static ZkProofResult BuildResult(string proofJson, string publicJson)
        {
            return ZkProofResult.BuildFromJson(ZkProofScheme.Groth16, proofJson, publicJson);
        }
    }
}
