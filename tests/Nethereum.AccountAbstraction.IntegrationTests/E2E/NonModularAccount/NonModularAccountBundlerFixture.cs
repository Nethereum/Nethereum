using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler.InProcess;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition;
using Nethereum.RPC;
using Nethereum.Signer;
using Xunit;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.NonModularAccount
{
    [CollectionDefinition(COLLECTION_NAME)]
    public class NonModularAccountBundlerCollection : ICollectionFixture<NonModularAccountBundlerFixture>
    {
        public const string COLLECTION_NAME = NonModularAccountBundlerFixture.COLLECTION_NAME;
    }

    public class NonModularAccountBundlerFixture : IAsyncLifetime
    {
        public const string COLLECTION_NAME = "NonModularAccountBundler";
        public const int CHAIN_ID = 31337;

        private const string OWNER_PRIVATE_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string BUNDLER_PRIVATE_KEY = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";

        public InProcessBundlerHost Bootstrap { get; private set; } = null!;
        public Web3Account OwnerAccount { get; private set; } = null!;
        public EntryPointService EntryPointService { get; private set; } = null!;
        public SimpleAccountFactoryService FactoryService { get; private set; } = null!;
        public IAccountAbstractionBundlerService Bundler { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            OwnerAccount = new Web3Account(OWNER_PRIVATE_KEY, CHAIN_ID);
            var bundlerAccount = new Web3Account(BUNDLER_PRIVATE_KEY, CHAIN_ID);

            Bootstrap = await InProcessBundlerHost.StartAsync(
                OwnerAccount,
                CHAIN_ID,
                new[] { OwnerAccount.Address, bundlerAccount.Address },
                Nethereum.Web3.Web3.Convert.ToWei(10000));

            EntryPointService = await EntryPointService.DeployContractAndGetServiceAsync(
                Bootstrap.OperatorWeb3, new EntryPointDeployment());

            FactoryService = await SimpleAccountFactoryService.DeployContractAndGetServiceAsync(
                Bootstrap.OperatorWeb3,
                new SimpleAccountFactoryDeployment { EntryPoint = EntryPointService.ContractAddress });

            Bundler = Bootstrap.StartBundler(EntryPointService.ContractAddress, bundlerAccount);
        }

        public async Task DisposeAsync()
        {
            await Bootstrap.DisposeAsync();
        }

        public async Task<(string accountAddress, EthECKey ownerKey)> DeployFundedSimpleAccountAsync(
            ulong salt, decimal ethAmount = 2m)
        {
            var ownerKey = EthECKey.GenerateKey();
            var ownerAddress = ownerKey.GetPublicAddress();

            var result = await FactoryService.CreateAndDeployAccountAsync(
                ownerAddress,
                ownerAddress,
                EntryPointService.ContractAddress,
                ownerKey,
                ethAmount,
                salt);

            return (result.AccountAddress, ownerKey);
        }
    }
}
