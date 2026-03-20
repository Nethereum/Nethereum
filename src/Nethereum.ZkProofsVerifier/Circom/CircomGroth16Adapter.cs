using Nethereum.ZkProofsVerifier.Abstractions;
using Nethereum.ZkProofsVerifier.Groth16;

namespace Nethereum.ZkProofsVerifier.Circom
{
    public static class CircomGroth16Adapter
    {
        public static ZkVerificationResult Verify(string proofJson, string vkJson, string publicInputsJson)
        {
            var proof = SnarkjsProofParser.Parse(proofJson);
            var vk = SnarkjsVerificationKeyParser.Parse(vkJson);
            var publicInputs = SnarkjsPublicInputParser.Parse(publicInputsJson);
            return new Groth16Verifier().Verify(proof, vk, publicInputs);
        }
    }
}
