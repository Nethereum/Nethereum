using System.Numerics;
using System.Threading.Tasks;
using Nethereum.PrivacyPools;
using Nethereum.ZkProofs.RapidSnark;
using Nethereum.ZkProofsVerifier.Circom;
using Nethereum.ZkProofsVerifier.Groth16;
using Xunit;

namespace Nethereum.PrivacyPools.Circuits.Tests.SecurityAudit71.S12.Round4
{
    public class DirectPathRound4Tests
    {
        [Fact]
        [Trait("Rule", "S12-R4-1")]
        public async Task Given_a_real_ragequit_proof_then_the_adapter_and_the_direct_verifier_both_accept_it()
        {
            var circuitSource = new Circuits.PrivacyPoolCircuitSource();
            Assert.True(circuitSource.HasCircuit("commitment"));

            var (nullifier, secret, _) = PrivacyPoolCommitment.GenerateRandomPrecommitment();
            var commitment = PrivacyPoolCommitment.Create(new BigInteger(1000000), new BigInteger(42), nullifier, secret);

            var provider = new PrivacyPoolProofProvider(new NativeProofProvider(), circuitSource);
            var result = await provider.GenerateRagequitProofAsync(new RagequitWitnessInput
            {
                Nullifier = commitment.Nullifier,
                Secret = commitment.Secret,
                Value = commitment.Value,
                Label = commitment.Label
            });

            var vkJson = circuitSource.GetVerificationKeyJson("commitment");
            var publicJson = "[" + string.Join(",", System.Linq.Enumerable.Select(result.Signals.ToArray(), s => "\"" + s + "\"")) + "]";

            Assert.True(CircomGroth16Adapter.Verify(result.ProofJson, vkJson, publicJson).IsValid);

            var proof = SnarkjsProofParser.Parse(result.ProofJson);
            var vk = SnarkjsVerificationKeyParser.Parse(vkJson);
            var inputs = SnarkjsPublicInputParser.Parse(publicJson);
            Assert.True(new Groth16Verifier().Verify(proof, vk, inputs).IsValid);
            Assert.True(CircomGroth16Adapter.Verify(result.ProofJson, vk, publicJson).IsValid);

            var verifier = new PrivacyPoolProofVerifier("{}", vkJson);
            Assert.True(verifier.VerifyRagequitProof(result.ProofJson, result.Signals).IsValid);
        }

        [Fact]
        [Trait("Rule", "S12-R4-1")]
        public void Given_the_shipped_circuit_verification_keys_then_they_parse_and_validate_all_points()
        {
            var circuitSource = new Circuits.PrivacyPoolCircuitSource();
            foreach (var name in new[] { "commitment", "withdrawal" })
            {
                Assert.True(circuitSource.HasCircuit(name));
                var vk = SnarkjsVerificationKeyParser.Parse(circuitSource.GetVerificationKeyJson(name));
                Assert.NotNull(vk.IC);
                Assert.True(vk.IC.Length > 1);
            }
        }
    }
}
