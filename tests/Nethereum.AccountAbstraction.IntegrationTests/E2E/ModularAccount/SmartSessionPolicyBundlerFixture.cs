using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler.InProcess;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableValidator;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableValidator.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.SudoPolicy;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.SudoPolicy.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.UniActionPolicy;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.UniActionPolicy.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Validators.ECDSASessionValidator;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Validators.ECDSASessionValidator.ContractDefinition;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.RPC;
using Xunit;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.ModularAccount
{
    [CollectionDefinition(COLLECTION_NAME)]
    public class SmartSessionPolicyBundlerCollection : ICollectionFixture<SmartSessionPolicyBundlerFixture>
    {
        public const string COLLECTION_NAME = SmartSessionPolicyBundlerFixture.COLLECTION_NAME;
    }

    public class SmartSessionPolicyBundlerFixture : IAsyncLifetime
    {
        public const string COLLECTION_NAME = "SmartSessionPolicyBundler";
        public const int CHAIN_ID = 31337;

        private const string OWNER_PRIVATE_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string BUNDLER_PRIVATE_KEY = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";

        public InProcessBundlerHost Bootstrap { get; private set; } = null!;
        public Web3Account OwnerAccount { get; private set; } = null!;
        public EntryPointService EntryPointService { get; private set; } = null!;
        public NethereumAccountFactoryService FactoryService { get; private set; } = null!;
        public ECDSAValidatorService EcdsaValidatorService { get; private set; } = null!;
        public SmartSessionService SmartSessionService { get; private set; } = null!;
        public ECDSASessionValidatorService SessionValidatorService { get; private set; } = null!;
        public OwnableValidatorService OwnableValidatorService { get; private set; } = null!;
        public SudoPolicyService SudoPolicyService { get; private set; } = null!;
        public UniActionPolicyService UniActionPolicyService { get; private set; } = null!;
        public TestCounterService TestCounterService { get; private set; } = null!;
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

            EcdsaValidatorService = await ECDSAValidatorService.DeployContractAndGetServiceAsync(
                Bootstrap.OperatorWeb3, new ECDSAValidatorDeployment());

            FactoryService = await NethereumAccountFactoryService.DeployContractAndGetServiceAsync(
                Bootstrap.OperatorWeb3,
                new NethereumAccountFactoryDeployment { EntryPoint = EntryPointService.ContractAddress });

            SmartSessionService = await SmartSessionService.DeployContractAndGetServiceAsync(
                Bootstrap.OperatorWeb3, new SmartSessionDeployment());

            SessionValidatorService = await ECDSASessionValidatorService.DeployContractAndGetServiceAsync(
                Bootstrap.OperatorWeb3, new ECDSASessionValidatorDeployment());

            OwnableValidatorService = await OwnableValidatorService.DeployContractAndGetServiceAsync(
                Bootstrap.OperatorWeb3, new OwnableValidatorDeployment());

            SudoPolicyService = await SudoPolicyService.DeployContractAndGetServiceAsync(
                Bootstrap.OperatorWeb3, new SudoPolicyDeployment());

            UniActionPolicyService = await UniActionPolicyService.DeployContractAndGetServiceAsync(
                Bootstrap.OperatorWeb3, new UniActionPolicyDeployment());

            TestCounterService = await TestCounterService.DeployContractAndGetServiceAsync(
                Bootstrap.OperatorWeb3, new TestCounterDeployment());

            Bundler = Bootstrap.StartBundler(EntryPointService.ContractAddress, bundlerAccount);
        }

        public async Task DisposeAsync()
        {
            await Bootstrap.DisposeAsync();
        }
    }
}
