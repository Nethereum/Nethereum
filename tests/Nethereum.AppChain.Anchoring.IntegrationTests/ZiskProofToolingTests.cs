using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Nethereum.Testing;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.Anchoring.IntegrationTests
{
    [Trait("Category", "Zisk")]
    [Trait("Requires", "Externals")]
    public class ZiskProofToolingTests
    {
        private static readonly string Proofs = Path.Combine(TestEnvironment.RepoRoot, "zisk", "test-artifacts", "proofs");

        private readonly ITestOutputHelper _output;

        public ZiskProofToolingTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private string Run(string commandLine)
        {
            var psi = ZiskHost.Command(commandLine + " 2>&1");
            using var process = Process.Start(psi);
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(300000);
            _output.WriteLine(output);
            return output;
        }

        [Fact]
        public void Given_TheCommittedStarkProofs_When_CargoZiskVerifies_Then_EveryProofIsAccepted()
        {
            var proofs = Directory.GetFiles(Proofs, "proof_*");
            Assert.NotEmpty(proofs);

            foreach (var proof in proofs)
            {
                var output = Run($"cargo-zisk verify -p {ZiskHost.Path(proof)}");
                Assert.Contains("STARK proof was verified", output);
            }
        }

        [Theory]
        [InlineData("plonk_eth_transfer_noroot")]
        [InlineData("plonk_eth_transfer_anchor")]
        public void Given_ACommittedPlonkProof_When_CargoZiskVerifies_Then_ItIsAccepted(string proof)
        {
            var output = Run($"cargo-zisk verify -p {ZiskHost.Path(Path.Combine(Proofs, proof))}");

            Assert.Contains("PLONK proof was verified", output);
        }

        [Theory]
        [InlineData("plonk_eth_transfer_noroot", "zisk_plonk_fixture.json")]
        [InlineData("plonk_eth_transfer_anchor", "zisk_plonk_anchor_fixture.json")]
        public void Given_ACommittedPlonkProof_When_TheSolidityCalldataIsExported_Then_ItEqualsTheFixtureTheVerifierTestUses(string proof, string fixture)
        {
            var exported = Path.Combine(Path.GetTempPath(), $"zisk-fixture-{System.Guid.NewGuid():N}.json");
            try
            {
                Run($"cargo-zisk-dev export-solidity-calldata -p {ZiskHost.Path(Path.Combine(Proofs, proof))} -o {ZiskHost.Path(exported)}");

                var committed = Path.Combine(TestEnvironment.RepoRoot, "tests", "Nethereum.AppChain.Anchoring.IntegrationTests", "Fixtures", fixture);
                using var actualDocument = JsonDocument.Parse(File.ReadAllText(exported));
                using var expectedDocument = JsonDocument.Parse(File.ReadAllText(committed));
                foreach (var field in new[] { "programVK", "rootCVadcopFinal", "publicValues", "proofBytes" })
                    Assert.Equal(expectedDocument.RootElement.GetProperty(field).GetString(), actualDocument.RootElement.GetProperty(field).GetString());
            }
            finally
            {
                File.Delete(exported);
            }
        }
    }
}
