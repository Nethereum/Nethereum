using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring.ZiskVerifier;
using Nethereum.AppChain.Anchoring.ZiskVerifier.ContractDefinition;
using Nethereum.AppChain.Anchoring.ZiskVerifierAdapter;
using Nethereum.AppChain.Anchoring.ZiskVerifierAdapter.ContractDefinition;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.Web3;
using Xunit;
using Xunit.Abstractions;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.AppChain.Anchoring.IntegrationTests
{
    public class ZiskAnchorBoundData
    {
        public ZiskPlonkFixtureData Proof { get; set; }
        public List<BigInteger> AnchorInputs { get; set; }

        public static ZiskAnchorBoundData Load()
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "Fixtures");
            var proof = ZiskPlonkFixtureData.Load("zisk_plonk_anchor_fixture.json");
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "zisk_anchor_inputs.json")));
            var inputs = document.RootElement.EnumerateArray()
                .Select(e => new BigInteger(e.GetString().HexToByteArray(), isUnsigned: true, isBigEndian: true))
                .ToList();
            return new ZiskAnchorBoundData { Proof = proof, AnchorInputs = inputs };
        }

        public static List<ulong> Limbs(byte[] key32)
        {
            var limbs = new List<ulong>();
            for (int i = 0; i < 4; i++)
                limbs.Add(System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(key32.AsSpan(i * 8, 8)));
            return limbs;
        }

        public static byte[] AdapterProof(byte[] proofBytes, byte[] publicValues)
        {
            var length = new byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(length, (uint)proofBytes.Length);
            return length.Concat(proofBytes).Concat(publicValues).ToArray();
        }
    }

    public class ZiskAnchorBoundFixture : IAsyncLifetime
    {
        private const int ChainId = 1337;
        private const string DeployerKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";

        private DevChainNode _node;

        public ZiskVerifierAdapterService Adapter { get; private set; }
        public ZiskVerifierService Verifier { get; private set; }

        public async Task InitializeAsync()
        {
            var data = ZiskAnchorBoundData.Load();
            var deployer = new Web3Account(DeployerKey, ChainId);
            _node = new DevChainNode(new DevChainConfig
            {
                ChainId = ChainId,
                BaseFee = 1_000_000_000,
                BlockGasLimit = 30_000_000,
                AutoMine = true
            });
            await _node.StartAsync(new[] { deployer.Address }, Web3.Web3.Convert.ToWei(10000));
            var web3 = _node.CreateWeb3(deployer);

            Verifier = await ZiskVerifierService.DeployContractAndGetServiceAsync(web3, new ZiskVerifierDeployment());
            Adapter = await ZiskVerifierAdapterService.DeployContractAndGetServiceAsync(web3, new ZiskVerifierAdapterDeployment
            {
                ZiskVerifier = Verifier.ContractAddress,
                ProgramVK = ZiskAnchorBoundData.Limbs(data.Proof.ProgramVK),
                RootCVadcopFinal = ZiskAnchorBoundData.Limbs(data.Proof.RootCVadcopFinal)
            });
        }

        public Task DisposeAsync()
        {
            _node?.Dispose();
            return Task.CompletedTask;
        }
    }

    public class ZiskAnchorBoundTests : IClassFixture<ZiskAnchorBoundFixture>
    {
        private readonly ZiskAnchorBoundFixture _fixture;
        private readonly ITestOutputHelper _output;

        public ZiskAnchorBoundTests(ZiskAnchorBoundFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        private Task<bool> Verify(byte[] proofBytes, byte[] publicValues, List<BigInteger> anchorInputs) =>
            _fixture.Adapter.VerifyQueryAsync(ZiskAnchorBoundData.AdapterProof(proofBytes, publicValues), anchorInputs);

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_TheRealAnchorBoundProof_When_TheAdapterVerifiesItForTheAnchorInputsTheGuestCommitted_Then_ItIsAccepted()
        {
            var data = ZiskAnchorBoundData.Load();

            var accepted = await Verify(data.Proof.ProofBytes, data.Proof.PublicValues, data.AnchorInputs);

            Assert.True(accepted);
            var gas = await _fixture.Adapter.ContractHandler.EstimateGasAsync(new VerifyFunction
            {
                Proof = ZiskAnchorBoundData.AdapterProof(data.Proof.ProofBytes, data.Proof.PublicValues),
                PublicInputs = data.AnchorInputs
            });
            _output.WriteLine($"adapter verify gas estimate: {gas.Value}");
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_TheRealAnchorBoundProof_When_TheAdapterVerifiesItForDifferentAnchorInputs_Then_ItIsRejected()
        {
            var data = ZiskAnchorBoundData.Load();
            var other = new List<BigInteger>(data.AnchorInputs);
            other[4] += 1;

            Assert.False(await Verify(data.Proof.ProofBytes, data.Proof.PublicValues, other));
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_TheRealAnchorBoundProof_When_AnAnchorInputWordIsDropped_Then_ItIsRejected()
        {
            var data = ZiskAnchorBoundData.Load();

            await Assert.ThrowsAnyAsync<Exception>(() => Verify(data.Proof.ProofBytes, data.Proof.PublicValues, data.AnchorInputs.Take(10).ToList()));
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_AProofWithOneBitChanged_When_TheAdapterVerifiesIt_Then_ItIsRejected()
        {
            var data = ZiskAnchorBoundData.Load();
            var tampered = (byte[])data.Proof.ProofBytes.Clone();
            tampered[tampered.Length - 1] ^= 0x01;

            Assert.False(await Verify(tampered, data.Proof.PublicValues, data.AnchorInputs));
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_PublicValuesWithAChangedOutputAfterTheCommitment_When_TheAdapterVerifiesIt_Then_ItIsRejected()
        {
            var data = ZiskAnchorBoundData.Load();
            var tampered = (byte[])data.Proof.PublicValues.Clone();
            tampered[9 * 8] ^= 0x01;

            Assert.False(await Verify(data.Proof.ProofBytes, tampered, data.AnchorInputs));
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_PublicValuesWithAChangedCommitmentWord_When_TheAdapterVerifiesIt_Then_ItIsRejected()
        {
            var data = ZiskAnchorBoundData.Load();
            var tampered = (byte[])data.Proof.PublicValues.Clone();
            tampered[0] ^= 0x01;

            Assert.False(await Verify(data.Proof.ProofBytes, tampered, data.AnchorInputs));
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_TheGenericProofWithNoAnchorCommitment_When_TheAdapterVerifiesIt_Then_ItIsRejected()
        {
            var generic = ZiskPlonkFixtureData.Load();
            var data = ZiskAnchorBoundData.Load();

            Assert.False(await Verify(generic.ProofBytes, generic.PublicValues, data.AnchorInputs));
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_TheRealAnchorBoundProof_When_ZiskVerifierVerifiesItDirectly_Then_ItDoesNotRevert()
        {
            var data = ZiskAnchorBoundData.Load();

            var result = await _fixture.Verifier.ContractHandler.QueryRawAsync<VerifySnarkProofFunction>(data.Proof.ToFunction());

            Assert.Empty(result);
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_TheAnchorBoundProof_When_ItsPublicValuesAreRead_Then_TheFirstEightWordsHoldTheKeccakOfTheAnchorInputs()
        {
            var data = ZiskAnchorBoundData.Load();
            var encoded = data.AnchorInputs.SelectMany(w =>
            {
                var bytes = w.ToByteArray(isUnsigned: true, isBigEndian: true);
                return new byte[32 - bytes.Length].Concat(bytes);
            }).ToArray();
            var expected = new Sha3Keccack().CalculateHash(encoded);

            var committed = new byte[32];
            for (int i = 0; i < 8; i++)
                for (int b = 0; b < 4; b++)
                    committed[i * 4 + b] = data.Proof.PublicValues[i * 8 + 3 - b];

            Assert.Equal(expected.ToHex(), committed.ToHex());
            await Task.CompletedTask;
        }
    }
}
