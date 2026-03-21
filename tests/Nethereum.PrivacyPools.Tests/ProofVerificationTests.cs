using System.IO;
using System.Numerics;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.PrivacyPools.Tests
{
    public class ProofVerificationTests
    {
        [Fact]
        [Trait("Category", "PrivacyPools-ProofSignals")]
        public void WithdrawProofSignals_RoundTrip()
        {
            var signals = new WithdrawProofSignals
            {
                NewCommitmentHash = new BigInteger(1),
                ExistingNullifierHash = new BigInteger(2),
                WithdrawnValue = new BigInteger(3),
                StateRoot = new BigInteger(4),
                StateTreeDepth = new BigInteger(5),
                ASPRoot = new BigInteger(6),
                ASPTreeDepth = new BigInteger(7),
                Context = new BigInteger(8)
            };

            var array = signals.ToArray();
            var restored = WithdrawProofSignals.FromArray(array);

            Assert.Equal(signals.NewCommitmentHash, restored.NewCommitmentHash);
            Assert.Equal(signals.ExistingNullifierHash, restored.ExistingNullifierHash);
            Assert.Equal(signals.WithdrawnValue, restored.WithdrawnValue);
            Assert.Equal(signals.StateRoot, restored.StateRoot);
            Assert.Equal(signals.StateTreeDepth, restored.StateTreeDepth);
            Assert.Equal(signals.ASPRoot, restored.ASPRoot);
            Assert.Equal(signals.ASPTreeDepth, restored.ASPTreeDepth);
            Assert.Equal(signals.Context, restored.Context);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-ProofSignals")]
        public void RagequitProofSignals_RoundTrip()
        {
            var signals = new RagequitProofSignals
            {
                CommitmentHash = new BigInteger(100),
                NullifierHash = new BigInteger(200),
                Value = new BigInteger(300),
                Label = new BigInteger(400)
            };

            var array = signals.ToArray();
            var restored = RagequitProofSignals.FromArray(array);

            Assert.Equal(signals.CommitmentHash, restored.CommitmentHash);
            Assert.Equal(signals.NullifierHash, restored.NullifierHash);
            Assert.Equal(signals.Value, restored.Value);
            Assert.Equal(signals.Label, restored.Label);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-ProofSignals")]
        [NethereumDocExample(DocSection.Protocols, "proof-verification", "Load and use proof verifier")]
        public void PrivacyPoolProofVerifier_LoadsRealWithdrawalVk()
        {
            var vkPath = Path.Combine("TestData", "withdrawal_vk.json");
            if (!File.Exists(vkPath)) return;

            var vkJson = File.ReadAllText(vkPath);
            var verifier = new PrivacyPoolProofVerifier(vkJson);
            Assert.NotNull(verifier);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-ProofSignals")]
        public void PrivacyPoolProofVerifier_LoadsBothRealVkeys()
        {
            var withdrawalVkPath = Path.Combine("TestData", "withdrawal_vk.json");
            var commitmentVkPath = Path.Combine("TestData", "commitment_vk.json");
            if (!File.Exists(withdrawalVkPath) || !File.Exists(commitmentVkPath)) return;

            var withdrawalVk = File.ReadAllText(withdrawalVkPath);
            var commitmentVk = File.ReadAllText(commitmentVkPath);
            var verifier = new PrivacyPoolProofVerifier(withdrawalVk, commitmentVk);
            Assert.NotNull(verifier);
        }
    }
}
