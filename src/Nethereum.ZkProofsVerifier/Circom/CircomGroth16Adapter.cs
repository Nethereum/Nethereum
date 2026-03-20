using System.Runtime.CompilerServices;
using Nethereum.ZkProofsVerifier.Abstractions;
using Nethereum.ZkProofsVerifier.Groth16;

[assembly: InternalsVisibleTo("Nethereum.ZkProofsVerifier.Tests,PublicKey="
    + "0024000004800000940000000602000000240000525341310004000001000100d90181381ce37f"
    + "cd30d5dcbea4eeb9665a845853878b90278cecf8d94965b49c2dfea39e67f397c29719fb6b130d"
    + "b7d23d1fe3639650974c1013c6f18d02a41b820398561cf9b41c923f9f2bbc7efe314e9d36c610"
    + "7df2c31658cd4efce0f9e7ff4a41105b61eb999861cff4f1951b0ff62dc1d707c2b82c1ef8ee63"
    + "5cfbc4b6")]

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
