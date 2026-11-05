using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring.AppChainAnchor;
using Nethereum.AppChain.Anchoring.AppChainAnchor.ContractDefinition;
using Nethereum.AppChain.Anchoring.SimpleAuthority;
using Nethereum.AppChain.Anchoring.SimpleAuthority.ContractDefinition;
using Nethereum.AppChain.Anchoring.ZiskVerifier;
using Nethereum.AppChain.Anchoring.ZiskVerifier.ContractDefinition;
using Nethereum.AppChain.Anchoring.ZiskVerifierAdapter;
using Nethereum.AppChain.Anchoring.ZiskVerifierAdapter.ContractDefinition;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Web3;
using Xunit;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.AppChain.Anchoring.IntegrationTests
{
    public class ZiskAnchorSubmitFixture : IAsyncLifetime
    {
        public const int ChainId = 1337;
        private const string DeployerKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";

        private DevChainNode _node;

        public IWeb3 Web3 { get; private set; }
        public AppChainAnchorService Anchor { get; private set; }
        public byte[] GenesisHash { get; private set; }

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
            await _node.StartAsync(new[] { deployer.Address }, Nethereum.Web3.Web3.Convert.ToWei(10000));
            Web3 = _node.CreateWeb3(deployer);

            var verifier = await ZiskVerifierService.DeployContractAndGetServiceAsync(Web3, new ZiskVerifierDeployment());
            var adapter = await ZiskVerifierAdapterService.DeployContractAndGetServiceAsync(Web3, new ZiskVerifierAdapterDeployment
            {
                ZiskVerifier = verifier.ContractAddress,
                ProgramVK = ZiskAnchorBoundData.Limbs(data.Proof.ProgramVK),
                RootCVadcopFinal = ZiskAnchorBoundData.Limbs(data.Proof.RootCVadcopFinal)
            });
            var authority = await SimpleAuthorityService.DeployContractAndGetServiceAsync(
                Web3, new SimpleAuthorityDeployment { Owner = deployer.Address });
            Anchor = await AppChainAnchorService.DeployContractAndGetServiceAsync(Web3, new AppChainAnchorDeployment());

            var blake3 = new Nethereum.Util.Sha3Keccack().CalculateHash(System.Text.Encoding.UTF8.GetBytes("blake3"));
            await Anchor.RegisterSchemaRequestAndWaitForReceiptAsync(
                new RegisterSchemaFunction { Version = 1, HashFunction = blake3, TrieType = 1, StateModel = 0 });
            await Anchor.RegisterProofSystemRequestAndWaitForReceiptAsync(
                new RegisterProofSystemFunction { ProofSystem = 2, Verifier = adapter.ContractAddress, RequiresProof = true });

            GenesisHash = new Nethereum.Util.Sha3Keccack().CalculateHash(System.Text.Encoding.UTF8.GetBytes("zisk-anchor-submit"));
            await Anchor.RegisterAppChainRequestAndWaitForReceiptAsync(new RegisterAppChainFunction
            {
                ChainId = ChainId,
                GenesisHash = GenesisHash,
                GenesisBlock = 1,
                GenesisStateRoot = new byte[32],
                MinimumProofSystem = 2,
                MinimumAnchorVersion = 1,
                Authority = authority.ContractAddress
            });
            await authority.SetOperatorRequestAndWaitForReceiptAsync(
                new SetOperatorFunction { ChainId = ChainId, NewOperator = deployer.Address });
        }

        public Task DisposeAsync()
        {
            _node?.Dispose();
            return Task.CompletedTask;
        }
    }

    public class ZiskAnchorSubmitTests : IClassFixture<ZiskAnchorSubmitFixture>
    {
        private readonly ZiskAnchorSubmitFixture _fixture;

        public ZiskAnchorSubmitTests(ZiskAnchorSubmitFixture fixture)
        {
            _fixture = fixture;
        }

        private static byte[] Bytes32(BigInteger value)
        {
            var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            return new byte[32 - bytes.Length].Concat(bytes).ToArray();
        }

        private AppChainAnchorBatchService Service() => new AppChainAnchorBatchService(
            new AnchorConfig { AnchorContractAddress = _fixture.Anchor.ContractAddress },
            _fixture.Web3, ZiskAnchorSubmitFixture.ChainId, _fixture.GenesisHash);

        private static AnchorSubmissionPayload Submission(byte[] proofBytes, byte[] publicValues) => new AnchorSubmissionPayload
        {
            OnChainProofSystem = AnchoringOnChainProofSystem.SnarkOnChain,
            ProofBytes = ZiskAnchorBoundData.AdapterProof(proofBytes, publicValues)
        };

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_ATamperedProof_When_TheAnchoringServiceSubmitsItThroughTheAdapter_Then_NoAnchorIsRecorded()
        {
            var data = ZiskAnchorBoundData.Load();
            var tampered = (byte[])data.Proof.ProofBytes.Clone();
            tampered[tampered.Length - 1] ^= 0x01;

            var info = await Service().AnchorBlockAsync(
                1, Bytes32(data.AnchorInputs[6]), null, null, Bytes32(data.AnchorInputs[8]),
                Submission(tampered, data.Proof.PublicValues));

            Assert.Equal(AnchorStatus.Failed, info.Status);
            var latest = await _fixture.Anchor.GetLatestAnchorQueryAsync(ZiskAnchorSubmitFixture.ChainId);
            Assert.Equal(0UL, latest.EndBlock);
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_TheRealProofForAnotherBlocksStateRoot_When_TheAnchoringServiceSubmitsIt_Then_NoAnchorIsRecorded()
        {
            var data = ZiskAnchorBoundData.Load();
            var otherRoot = Bytes32(data.AnchorInputs[6] + 1);

            var info = await Service().AnchorBlockAsync(
                1, otherRoot, null, null, Bytes32(data.AnchorInputs[8]),
                Submission(data.Proof.ProofBytes, data.Proof.PublicValues));

            Assert.Equal(AnchorStatus.Failed, info.Status);
            var latest = await _fixture.Anchor.GetLatestAnchorQueryAsync(ZiskAnchorSubmitFixture.ChainId);
            Assert.Equal(0UL, latest.EndBlock);
        }

        [Fact]
        [Trait("Category", "ZiskVerifier")]
        public async Task Given_TheRealAnchorBoundProof_When_TheAnchoringServiceSubmitsItThroughTheAdapter_Then_TheAnchorIsRecorded()
        {
            var data = ZiskAnchorBoundData.Load();
            var stateRoot = Bytes32(data.AnchorInputs[6]);
            var blockHash = Bytes32(data.AnchorInputs[8]);

            var info = await Service().AnchorBlockAsync(
                1, stateRoot, null, null, blockHash,
                Submission(data.Proof.ProofBytes, data.Proof.PublicValues));

            Assert.True(info.Status == AnchorStatus.Confirmed, info.ErrorMessage);
            var latest = await _fixture.Anchor.GetLatestAnchorQueryAsync(ZiskAnchorSubmitFixture.ChainId);
            Assert.Equal(1UL, latest.EndBlock);
            Assert.Equal(stateRoot.ToHex(), latest.PostStateRoot.ToHex());
            Assert.Equal(blockHash.ToHex(), latest.EndBlockHash.ToHex());
        }
    }
}
