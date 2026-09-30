using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.InProcess;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.WebAuthn.Contracts.Modules.Rhinestone.WebAuthnValidator;
using Nethereum.AccountAbstraction.WebAuthn.Contracts.Modules.Rhinestone.WebAuthnValidator.ContractDefinition;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.DevChain;
using Nethereum.RPC;
using Xunit;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.ModularAccount
{
    [CollectionDefinition(COLLECTION_NAME)]
    public class WebAuthnPrecompileModularAccountBundlerCollection : ICollectionFixture<WebAuthnPrecompileModularAccountBundlerFixture>
    {
        public const string COLLECTION_NAME = WebAuthnPrecompileModularAccountBundlerFixture.COLLECTION_NAME;
    }

    public class WebAuthnPrecompileModularAccountBundlerFixture : IAsyncLifetime
    {
        public const string COLLECTION_NAME = "WebAuthnPrecompileModularAccountBundler";
        public const int CHAIN_ID = 31337;

        private const string OWNER_PRIVATE_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string BUNDLER_PRIVATE_KEY = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";

        public InProcessBundlerHost Bootstrap { get; private set; } = null!;
        public Web3Account OwnerAccount { get; private set; } = null!;
        public EntryPointService EntryPointService { get; private set; } = null!;
        public NethereumAccountFactoryService FactoryService { get; private set; } = null!;
        public WebAuthnValidatorService WebAuthnValidatorService { get; private set; } = null!;
        public TestCounterService TestCounterService { get; private set; } = null!;
        public IAccountAbstractionBundlerService Bundler { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            OwnerAccount = new Web3Account(OWNER_PRIVATE_KEY, CHAIN_ID);
            var bundlerAccount = new Web3Account(BUNDLER_PRIVATE_KEY, CHAIN_ID);

            var devChainConfig = new DevChainConfig
            {
                ChainId = CHAIN_ID,
                BaseFee = 1_000_000_000,
                BlockGasLimit = 30_000_000,
                AutoMine = true,
                Hardfork = "osaka"
            };

            Bootstrap = await InProcessBundlerHost.StartAsync(
                OwnerAccount,
                CHAIN_ID,
                new[] { OwnerAccount.Address, bundlerAccount.Address },
                Nethereum.Web3.Web3.Convert.ToWei(10000),
                devChainConfig);

            EntryPointService = await EntryPointService.DeployContractAndGetServiceAsync(
                Bootstrap.OperatorWeb3, new EntryPointDeployment());

            WebAuthnValidatorService = await WebAuthnValidatorService.DeployContractAndGetServiceAsync(
                Bootstrap.OperatorWeb3, new WebAuthnValidatorDeployment());

            FactoryService = await NethereumAccountFactoryService.DeployContractAndGetServiceAsync(
                Bootstrap.OperatorWeb3,
                new NethereumAccountFactoryDeployment { EntryPoint = EntryPointService.ContractAddress });

            TestCounterService = await TestCounterService.DeployContractAndGetServiceAsync(
                Bootstrap.OperatorWeb3, new TestCounterDeployment());

            Bundler = Bootstrap.StartBundler(
                EntryPointService.ContractAddress,
                bundlerAccount,
                configureOverrides: c => c.Hardfork = "osaka");
        }

        public async Task DisposeAsync()
        {
            await Bootstrap.DisposeAsync();
        }
    }
}
