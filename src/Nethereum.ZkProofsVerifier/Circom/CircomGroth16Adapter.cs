using System;
using Nethereum.ZkProofsVerifier.Abstractions;
using Nethereum.ZkProofsVerifier.Groth16;

namespace Nethereum.ZkProofsVerifier.Circom
{
    public static class CircomGroth16Adapter
    {
        public static ZkVerificationResult Verify(string proofJson, string vkJson, string publicInputsJson)
        {
            try
            {
                var proof = SnarkjsProofParser.Parse(proofJson);
                var vk = SnarkjsVerificationKeyParser.Parse(vkJson);
                var publicInputs = SnarkjsPublicInputParser.Parse(publicInputsJson, ExpectedInputs(vk));
                return new Groth16Verifier().VerifyParsed(proof, vk, publicInputs);
            }
            catch (Exception ex)
            {
                return Malformed(ex);
            }
        }

        public static ZkVerificationResult Verify(string proofJson, Groth16VerificationKey vk, string publicInputsJson)
        {
            try
            {
                var proof = SnarkjsProofParser.Parse(proofJson);
                var publicInputs = SnarkjsPublicInputParser.Parse(publicInputsJson, ExpectedInputs(vk));
                return new Groth16Verifier().VerifyParsed(proof, vk, publicInputs);
            }
            catch (Exception ex)
            {
                return Malformed(ex);
            }
        }

        private static int ExpectedInputs(Groth16VerificationKey vk)
        {
            return vk?.IC == null || vk.IC.Length == 0 ? -1 : vk.IC.Length - 1;
        }

        internal static ZkVerificationResult Malformed(Exception ex)
        {
            return ZkVerificationResult.Invalid("Malformed proof, verification key or public inputs: " + ex.Message);
        }
    }
}
