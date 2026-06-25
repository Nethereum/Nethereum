using System;
using System.Linq;
using System.Reflection;
using Nethereum.Consensus.LightClient;
using Nethereum.Consensus.Ssz;
using Xunit;

namespace Nethereum.Consensus.LightClient.Tests
{
    /// <summary>
    /// Validates the signing-domain derivation rewrite per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
    /// specs/altair/light-client/sync-protocol.md</see> lines 451–454:
    /// <c>fork_version_slot = max(signature_slot, 1) - 1</c> selects the fork-version row used
    /// to seed <c>compute_domain</c>. Also covers <c>ComputeForkDataRoot</c> /
    /// <c>ComputeSigningRoot</c> strict input-length asserts (H8 + M6 absorbed) and
    /// <see cref="LightClientConfig"/> root-length validation (F1 + F2).
    /// </summary>
    public class SigningDomainForkVersionTests
    {
        private const ulong AltairActivationSlot = 2_375_680UL;
        private const ulong BellatrixActivationSlot = 4_636_672UL;
        private const ulong CapellaActivationSlot = 6_209_536UL;
        private const ulong DenebActivationSlot = 8_626_176UL;
        private const ulong ElectraActivationSlot = 11_649_024UL;
        private const ulong FuluActivationSlot = 13_164_544UL;

        private static readonly byte[] Phase0ForkVersion = new byte[] { 0x00, 0x00, 0x00, 0x00 };
        private static readonly byte[] AltairForkVersion = new byte[] { 0x01, 0x00, 0x00, 0x00 };
        private static readonly byte[] BellatrixForkVersion = new byte[] { 0x02, 0x00, 0x00, 0x00 };
        private static readonly byte[] CapellaForkVersion = new byte[] { 0x03, 0x00, 0x00, 0x00 };
        private static readonly byte[] DenebForkVersion = new byte[] { 0x04, 0x00, 0x00, 0x00 };
        private static readonly byte[] ElectraForkVersion = new byte[] { 0x05, 0x00, 0x00, 0x00 };
        private static readonly byte[] FuluForkVersion = new byte[] { 0x06, 0x00, 0x00, 0x00 };

        [Theory]
        [Trait("Category", "ConsensusSpec")]
        [InlineData(0UL, new byte[] { 0x00, 0x00, 0x00, 0x00 })]
        [InlineData(1UL, new byte[] { 0x00, 0x00, 0x00, 0x00 })]
        [InlineData(AltairActivationSlot, new byte[] { 0x00, 0x00, 0x00, 0x00 })]
        [InlineData(AltairActivationSlot + 1UL, new byte[] { 0x01, 0x00, 0x00, 0x00 })]
        [InlineData(BellatrixActivationSlot, new byte[] { 0x01, 0x00, 0x00, 0x00 })]
        [InlineData(BellatrixActivationSlot + 1UL, new byte[] { 0x02, 0x00, 0x00, 0x00 })]
        [InlineData(CapellaActivationSlot, new byte[] { 0x02, 0x00, 0x00, 0x00 })]
        [InlineData(CapellaActivationSlot + 1UL, new byte[] { 0x03, 0x00, 0x00, 0x00 })]
        [InlineData(DenebActivationSlot, new byte[] { 0x03, 0x00, 0x00, 0x00 })]
        [InlineData(DenebActivationSlot + 1UL, new byte[] { 0x04, 0x00, 0x00, 0x00 })]
        [InlineData(ElectraActivationSlot, new byte[] { 0x04, 0x00, 0x00, 0x00 })]
        [InlineData(ElectraActivationSlot + 1UL, new byte[] { 0x05, 0x00, 0x00, 0x00 })]
        [InlineData(FuluActivationSlot, new byte[] { 0x05, 0x00, 0x00, 0x00 })]
        [InlineData(FuluActivationSlot + 1UL, new byte[] { 0x06, 0x00, 0x00, 0x00 })]
        public void Given_SignatureSlot_When_ComputeSyncCommitteeDomain_Then_UsesSlotMinusOneForkVersion(
            ulong signatureSlot, byte[] expectedForkVersion)
        {
            var domain = InvokeComputeSyncCommitteeDomain(CreateConfig(), signatureSlot);

            Assert.Equal(32, domain.Length);
            Assert.Equal(0x07, domain[0]);
            Assert.Equal(0x00, domain[1]);
            Assert.Equal(0x00, domain[2]);
            Assert.Equal(0x00, domain[3]);

            var forkVersionFromTable = ChainSpec.Mainnet.GetForkVersionAtSlot(
                signatureSlot == 0UL ? 0UL : signatureSlot - 1UL);
            Assert.Equal(expectedForkVersion, forkVersionFromTable);
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void Given_SignatureSlotAtElectraActivation_When_DeriveForkVersion_Then_ReturnsDenebForkVersion()
        {
            var forkVersion = ChainSpec.Mainnet.GetForkVersionAtSlot(ElectraActivationSlot - 1UL);
            Assert.Equal(DenebForkVersion, forkVersion);
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void Given_SignatureSlotJustAfterElectraActivation_When_DeriveForkVersion_Then_ReturnsElectraForkVersion()
        {
            var signatureSlot = ElectraActivationSlot + 1UL;
            var forkVersion = ChainSpec.Mainnet.GetForkVersionAtSlot(signatureSlot - 1UL);
            Assert.Equal(ElectraForkVersion, forkVersion);
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void Given_DistinctSignatureSlotsAcrossForkBoundary_When_ComputeSyncCommitteeDomain_Then_DomainsDiffer()
        {
            var config = CreateConfig();

            var preElectraDomain = InvokeComputeSyncCommitteeDomain(config, ElectraActivationSlot);
            var postElectraDomain = InvokeComputeSyncCommitteeDomain(config, ElectraActivationSlot + 1UL);

            Assert.NotEqual(preElectraDomain, postElectraDomain);
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void Given_EmptyWeakSubjectivityRoot_When_AssignedToConfig_Then_ThrowsInvalidOperationException()
        {
            var config = new LightClientConfig();
            Assert.Throws<InvalidOperationException>(() => config.WeakSubjectivityRoot = Array.Empty<byte>());
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void Given_EmptyGenesisValidatorsRoot_When_AssignedToConfig_Then_ThrowsInvalidOperationException()
        {
            var config = new LightClientConfig();
            Assert.Throws<InvalidOperationException>(() => config.GenesisValidatorsRoot = Array.Empty<byte>());
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void Given_WrongLengthWeakSubjectivityRoot_When_AssignedToConfig_Then_ThrowsInvalidOperationException()
        {
            var config = new LightClientConfig();
            Assert.Throws<InvalidOperationException>(() => config.WeakSubjectivityRoot = new byte[16]);
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void Given_WrongLengthGenesisValidatorsRoot_When_AssignedToConfig_Then_ThrowsInvalidOperationException()
        {
            var config = new LightClientConfig();
            Assert.Throws<InvalidOperationException>(() => config.GenesisValidatorsRoot = new byte[16]);
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void Given_WrongLengthForkVersion_When_ComputeForkDataRoot_Then_ThrowsInvalidOperationException()
        {
            var ex = Assert.Throws<TargetInvocationException>(() =>
                InvokeComputeForkDataRoot(new byte[3], new byte[Nethereum.Consensus.Ssz.SszBasicTypes.RootLength]));
            Assert.IsType<InvalidOperationException>(ex.InnerException);
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void Given_WrongLengthGenesisValidatorsRoot_When_ComputeForkDataRoot_Then_ThrowsInvalidOperationException()
        {
            var ex = Assert.Throws<TargetInvocationException>(() =>
                InvokeComputeForkDataRoot(new byte[4], new byte[16]));
            Assert.IsType<InvalidOperationException>(ex.InnerException);
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void Given_NullForkVersion_When_ComputeForkDataRoot_Then_ThrowsArgumentNullException()
        {
            var ex = Assert.Throws<TargetInvocationException>(() =>
                InvokeComputeForkDataRoot(null, new byte[Nethereum.Consensus.Ssz.SszBasicTypes.RootLength]));
            Assert.IsType<ArgumentNullException>(ex.InnerException);
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void Given_WrongLengthDomain_When_ComputeSigningRoot_Then_ThrowsInvalidOperationException()
        {
            var ex = Assert.Throws<TargetInvocationException>(() =>
                InvokeComputeSigningRoot(new byte[Nethereum.Consensus.Ssz.SszBasicTypes.RootLength], new byte[16]));
            Assert.IsType<InvalidOperationException>(ex.InnerException);
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void Given_WrongLengthObjectRoot_When_ComputeSigningRoot_Then_ThrowsInvalidOperationException()
        {
            var ex = Assert.Throws<TargetInvocationException>(() =>
                InvokeComputeSigningRoot(new byte[16], new byte[32]));
            Assert.IsType<InvalidOperationException>(ex.InnerException);
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void Given_NullObjectRoot_When_ComputeSigningRoot_Then_ThrowsArgumentNullException()
        {
            var ex = Assert.Throws<TargetInvocationException>(() =>
                InvokeComputeSigningRoot(null, new byte[32]));
            Assert.IsType<ArgumentNullException>(ex.InnerException);
        }

        private static LightClientConfig CreateConfig()
        {
            return new LightClientConfig
            {
                GenesisValidatorsRoot = Enumerable.Repeat((byte)0xAA, Nethereum.Consensus.Ssz.SszBasicTypes.RootLength).ToArray(),
                SecondsPerSlot = 12,
                WeakSubjectivityRoot = Enumerable.Repeat((byte)0xBB, Nethereum.Consensus.Ssz.SszBasicTypes.RootLength).ToArray(),
                WeakSubjectivityPeriod = 256 * 32UL,
                ChainSpec = ChainSpec.Mainnet
            };
        }

        private static byte[] InvokeComputeSyncCommitteeDomain(LightClientConfig config, ulong signatureSlot)
        {
            var service = new LightClientService(new NullLightClientApi(), new NullBls(), config, new InMemoryLightClientStore());
            var method = typeof(LightClientService).GetMethod(
                "ComputeSyncCommitteeDomain", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            return (byte[])method.Invoke(service, new object[] { signatureSlot });
        }

        private static byte[] InvokeComputeForkDataRoot(byte[] forkVersion, byte[] genesisValidatorsRoot)
        {
            var method = typeof(LightClientService).GetMethod(
                "ComputeForkDataRoot", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (byte[])method.Invoke(null, new object[] { forkVersion, genesisValidatorsRoot });
        }

        private static byte[] InvokeComputeSigningRoot(byte[] objectRoot, byte[] domain)
        {
            var method = typeof(LightClientService).GetMethod(
                "ComputeSigningRoot", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            return (byte[])method.Invoke(null, new object[] { objectRoot, domain });
        }

        private sealed class NullLightClientApi : Nethereum.Beaconchain.LightClient.ILightClientApi
        {
            public System.Threading.Tasks.Task<Nethereum.Beaconchain.LightClient.Responses.LightClientBootstrapResponse> GetBootstrapAsync(string blockRoot) =>
                System.Threading.Tasks.Task.FromResult<Nethereum.Beaconchain.LightClient.Responses.LightClientBootstrapResponse>(null);
            public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<Nethereum.Beaconchain.LightClient.Responses.LightClientUpdateResponse>> GetUpdatesAsync(ulong fromPeriod, ulong count) =>
                System.Threading.Tasks.Task.FromResult<System.Collections.Generic.IReadOnlyList<Nethereum.Beaconchain.LightClient.Responses.LightClientUpdateResponse>>(Array.Empty<Nethereum.Beaconchain.LightClient.Responses.LightClientUpdateResponse>());
            public System.Threading.Tasks.Task<Nethereum.Beaconchain.LightClient.Responses.LightClientFinalityUpdateResponse> GetFinalityUpdateAsync() =>
                System.Threading.Tasks.Task.FromResult<Nethereum.Beaconchain.LightClient.Responses.LightClientFinalityUpdateResponse>(null);
            public System.Threading.Tasks.Task<Nethereum.Beaconchain.LightClient.Responses.LightClientOptimisticUpdateResponse> GetOptimisticUpdateAsync() =>
                System.Threading.Tasks.Task.FromResult<Nethereum.Beaconchain.LightClient.Responses.LightClientOptimisticUpdateResponse>(null);
        }

        private sealed class NullBls : Nethereum.Signer.Bls.IBls
        {
            public bool VerifyAggregate(byte[] aggregateSignature, byte[][] publicKeys, byte[][] messages, byte[] domain) => false;
            public byte[] AggregateSignatures(byte[][] signatures) => throw new NotSupportedException();
            public bool Verify(byte[] signature, byte[] publicKey, byte[] message) => throw new NotSupportedException();
            public (byte[] Signature, byte[] PublicKey) ExtractSignatureAndPublicKey(byte[] signatureWithPubKey) => throw new NotSupportedException();
        }
    }
}
