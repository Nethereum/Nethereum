using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring.AppChainAnchor;
using Nethereum.AppChain.Anchoring.AppChainAnchor.ContractDefinition;
using Nethereum.AppChain.Anchoring.AppChainProofManager;
using Nethereum.AppChain.Anchoring.AppChainProofManager.ContractDefinition;
using Nethereum.AppChain.Anchoring.MockProofVerifier;
using Nethereum.AppChain.Anchoring.MockProofVerifier.ContractDefinition;
using Nethereum.AppChain.Anchoring.SimpleAuthority;
using Nethereum.AppChain.Anchoring.SimpleAuthority.ContractDefinition;
using Nethereum.DevChain;
using Nethereum.Web3;
using Xunit;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.CoreChain.IntegrationTests.Fixtures
{
    [CollectionDefinition(DevChainAnchorFixture.COLLECTION_NAME)]
    public class DevChainAnchorCollection : ICollectionFixture<DevChainAnchorFixture> { }

    public class DevChainAnchorFixture : IAsyncLifetime
    {
        public const string COLLECTION_NAME = "DevChainAnchor";
        public const int CHAIN_ID = 1337;

        public DevChainNode L1Node { get; private set; }
        public IWeb3 OperatorWeb3 { get; private set; }
        public IWeb3 ChallengerWeb3 { get; private set; }

        public SimpleAuthorityService AuthorityService { get; private set; }
        public AppChainAnchorService AnchorService { get; private set; }
        public AppChainProofManagerService ProofManagerService { get; private set; }
        public MockProofVerifierService VerifierService { get; private set; }

        public Web3Account OperatorAccount { get; private set; }
        public Web3Account ChallengerAccount { get; private set; }

        public string OperatorPrivateKey => "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        public string ChallengerPrivateKey => "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";

        public async Task InitializeAsync()
        {
            OperatorAccount = new Web3Account(OperatorPrivateKey, CHAIN_ID);
            ChallengerAccount = new Web3Account(ChallengerPrivateKey, CHAIN_ID);

            L1Node = new DevChainNode(new DevChainConfig
            {
                ChainId = CHAIN_ID, BaseFee = 1_000_000_000,
                BlockGasLimit = 30_000_000, AutoMine = true
            });
            await L1Node.StartAsync(
                new[] { OperatorAccount.Address, ChallengerAccount.Address },
                Web3.Web3.Convert.ToWei(10000));

            OperatorWeb3 = L1Node.CreateWeb3(OperatorAccount);
            ChallengerWeb3 = L1Node.CreateWeb3(ChallengerAccount);

            AuthorityService = await SimpleAuthorityService.DeployContractAndGetServiceAsync(
                OperatorWeb3, new SimpleAuthorityDeployment { Owner = OperatorAccount.Address });

            AnchorService = await AppChainAnchorService.DeployContractAndGetServiceAsync(
                OperatorWeb3, new AppChainAnchorDeployment());

            VerifierService = await MockProofVerifierService.DeployContractAndGetServiceAsync(
                OperatorWeb3, new MockProofVerifierDeployment());

            ProofManagerService = await AppChainProofManagerService.DeployContractAndGetServiceAsync(
                OperatorWeb3, new AppChainProofManagerDeployment
                { AnchorContract = AnchorService.ContractAddress });

            var blake3 = new Nethereum.Util.Sha3Keccack()
                .CalculateHash(System.Text.Encoding.UTF8.GetBytes("blake3"));
            await AnchorService.RegisterSchemaRequestAndWaitForReceiptAsync(
                new RegisterSchemaFunction { Version = 1, HashFunction = blake3, TrieType = 1, StateModel = 0 });
            // ProofSystem enum: 0=NoProof, 1=StarkHashOffChain, 2=SnarkOnChain
            await AnchorService.RegisterProofSystemRequestAndWaitForReceiptAsync(
                new RegisterProofSystemFunction
                { ProofSystem = 0, Verifier = "0x0000000000000000000000000000000000000000", RequiresProof = false });
            await AnchorService.RegisterProofSystemRequestAndWaitForReceiptAsync(
                new RegisterProofSystemFunction
                { ProofSystem = 1, Verifier = "0x0000000000000000000000000000000000000000", RequiresProof = false });
            await AnchorService.RegisterProofSystemRequestAndWaitForReceiptAsync(
                new RegisterProofSystemFunction
                { ProofSystem = 2, Verifier = VerifierService.ContractAddress, RequiresProof = true });
        }

        public AppChainAnchorService CreateAnchorServiceAs(IWeb3 web3)
            => new AppChainAnchorService(web3, AnchorService.ContractAddress);

        public AppChainProofManagerService CreateProofManagerServiceAs(IWeb3 web3)
            => new AppChainProofManagerService(web3, ProofManagerService.ContractAddress);

        public SimpleAuthorityService CreateAuthorityServiceAs(IWeb3 web3)
            => new SimpleAuthorityService(web3, AuthorityService.ContractAddress);

        public Task DisposeAsync()
        {
            L1Node?.Dispose();
            return Task.CompletedTask;
        }
    }
}
