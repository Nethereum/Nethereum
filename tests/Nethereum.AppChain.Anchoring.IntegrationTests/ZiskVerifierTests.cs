using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring.ZiskVerifier;
using Nethereum.AppChain.Anchoring.ZiskVerifier.ContractDefinition;
using Nethereum.Contracts;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Web3;
using Xunit;
using Xunit.Abstractions;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.AppChain.Anchoring.IntegrationTests
{
    public class ZiskPlonkFixtureData
    {
        public byte[] ProgramVK { get; set; }
        public byte[] RootCVadcopFinal { get; set; }
        public byte[] PublicValues { get; set; }
        public byte[] ProofBytes { get; set; }

        public static ZiskPlonkFixtureData Load(string fileName = "zisk_plonk_fixture.json")
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            return new ZiskPlonkFixtureData
            {
                ProgramVK = root.GetProperty("programVK").GetString().HexToByteArray(),
                RootCVadcopFinal = root.GetProperty("rootCVadcopFinal").GetString().HexToByteArray(),
                PublicValues = root.GetProperty("publicValues").GetString().HexToByteArray(),
                ProofBytes = root.GetProperty("proofBytes").GetString().HexToByteArray()
            };
        }

        public VerifySnarkProofFunction ToFunction() => new VerifySnarkProofFunction
        {
            ProgramVK = ProgramVK,
            RootCVadcopFinal = RootCVadcopFinal,
            PublicValues = PublicValues,
            ProofBytes = ProofBytes
        };
    }

    public class ZiskVerifierFixture : IAsyncLifetime
    {
        private const int ChainId = 1337;
        private const string DeployerKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";

        private DevChainNode _node;

        public ZiskVerifierService Verifier { get; private set; }

        public async Task InitializeAsync()
        {
            var deployer = new Web3Account(DeployerKey, ChainId);
            _node = new DevChainNode(new DevChainConfig
            {
                ChainId = ChainId,
                BaseFee = 1_000_000_000,
                BlockGasLimit = 30_000_000,
                AutoMine = true
            });
            await _node.StartAsync(new[] { deployer.Address }, Web3.Web3.Convert.ToWei(10000));
            Verifier = await ZiskVerifierService.DeployContractAndGetServiceAsync(
                _node.CreateWeb3(deployer), new ZiskVerifierDeployment());
        }

        public Task DisposeAsync()
        {
            _node?.Dispose();
            return Task.CompletedTask;
        }
    }

    public class ZiskVerifierTests : IClassFixture<ZiskVerifierFixture>
    {
        private readonly ZiskVerifierFixture _fixture;
        private readonly ITestOutputHelper _output;

        public ZiskVerifierTests(ZiskVerifierFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        private async Task AssertRejectedAsync(VerifySnarkProofFunction function)
        {
            var exception = await Assert.ThrowsAnyAsync<Exception>(
                () => _fixture.Verifier.ContractHandler.QueryRawAsync<VerifySnarkProofFunction>(function));
            var rejected = exception is SmartContractCustomErrorRevertException custom
                ? custom.IsCustomErrorFor<InvalidProofError>()
                : exception.ToString().Contains("InvalidProof");
            Assert.True(rejected, exception.ToString());
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_TheRealPlonkProof_When_TheVerifierIsDeployedOnTheDevChain_Then_ItReportsTheRootTheProofWasWrappedUnder()
        {
            var data = ZiskPlonkFixtureData.Load();

            var root = await _fixture.Verifier.GetRootCVadcopFinalQueryAsync();

            Assert.Equal(data.RootCVadcopFinal.ToHex(), root.ToHex());
            Assert.Equal("v1.3.1-alpha", await _fixture.Verifier.VersionQueryAsync());
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_TheRealPlonkProof_When_VerifySnarkProofIsCalled_Then_ItDoesNotRevert()
        {
            var data = ZiskPlonkFixtureData.Load();

            var result = await _fixture.Verifier.ContractHandler.QueryRawAsync<VerifySnarkProofFunction>(data.ToFunction());
            var gas = await _fixture.Verifier.ContractHandler.EstimateGasAsync(data.ToFunction());

            Assert.Empty(result);
            _output.WriteLine($"verifySnarkProof gas estimate: {gas.Value}");
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_TheRealPlonkProof_When_TheDigestOfThePublicValuesIsComputed_Then_ItIsInTheBn254Field()
        {
            var data = ZiskPlonkFixtureData.Load();
            var field = System.Numerics.BigInteger.Parse(
                "21888242871839275222246405745257275088548364400416034343698204186575808495617");

            var digest = await _fixture.Verifier.HashPublicValuesQueryAsync(
                data.ProgramVK, data.RootCVadcopFinal, data.PublicValues);

            Assert.True(digest > 0 && digest < field);
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_AProofWithOneWordChanged_When_VerifySnarkProofIsCalled_Then_ItIsRejected()
        {
            var data = ZiskPlonkFixtureData.Load();
            var tampered = (byte[])data.ProofBytes.Clone();
            tampered[tampered.Length - 1] ^= 0x01;
            var function = data.ToFunction();
            function.ProofBytes = tampered;

            await AssertRejectedAsync(function);
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_AProofWithAFlippedBitInTheFirstCommitment_When_VerifySnarkProofIsCalled_Then_ItIsRejected()
        {
            var data = ZiskPlonkFixtureData.Load();
            var tampered = (byte[])data.ProofBytes.Clone();
            tampered[31] ^= 0x01;
            var function = data.ToFunction();
            function.ProofBytes = tampered;

            await AssertRejectedAsync(function);
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_PublicValuesWithOneByteChanged_When_VerifySnarkProofIsCalled_Then_ItIsRejected()
        {
            var data = ZiskPlonkFixtureData.Load();
            var tampered = (byte[])data.PublicValues.Clone();
            tampered[tampered.Length - 1] ^= 0x01;
            var function = data.ToFunction();
            function.PublicValues = tampered;

            await AssertRejectedAsync(function);
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_AnotherProgramVerificationKey_When_VerifySnarkProofIsCalled_Then_ItIsRejected()
        {
            var data = ZiskPlonkFixtureData.Load();
            var other = (byte[])data.ProgramVK.Clone();
            other[0] ^= 0x01;
            var function = data.ToFunction();
            function.ProgramVK = other;

            await AssertRejectedAsync(function);
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_AnotherRootCVadcopFinal_When_VerifySnarkProofIsCalled_Then_ItIsRejected()
        {
            var data = ZiskPlonkFixtureData.Load();
            var other = (byte[])data.RootCVadcopFinal.Clone();
            other[0] ^= 0x01;
            var function = data.ToFunction();
            function.RootCVadcopFinal = other;

            await AssertRejectedAsync(function);
        }
    }
}
