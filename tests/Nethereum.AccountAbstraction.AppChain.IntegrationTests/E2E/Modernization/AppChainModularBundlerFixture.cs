using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.AppChain.Configuration;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.AppChain.Services;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.InProcess;
using Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget;
using Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Rules.RuleRegistry;
using Nethereum.AccountAbstraction.Contracts.Rules.RuleRegistry.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Rules.ValueCapCombinator;
using Nethereum.AccountAbstraction.Contracts.Rules.ValueCapCombinator.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Rules.ValueCapRule;
using Nethereum.AccountAbstraction.Contracts.Rules.ValueCapRule.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableValidator;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableValidator.ContractDefinition;
using Nethereum.AccountAbstraction.WebAuthn.Contracts.Modules.Rhinestone.WebAuthnValidator;
using Nethereum.AccountAbstraction.WebAuthn.Contracts.Modules.Rhinestone.WebAuthnValidator.ContractDefinition;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain;
using Nethereum.RPC;
using Nethereum.Util;
using Nethereum.Web3;
using Xunit;
using Web3Account = Nethereum.Web3.Accounts.Account;
using DevChainRpcClient = Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Fixtures.DevChainRpcClient;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Modernization
{
    [CollectionDefinition(COLLECTION_NAME, DisableParallelization = true)]
    public class AppChainModularBundlerCollection : ICollectionFixture<AppChainModularBundlerFixture>
    {
        public const string COLLECTION_NAME = AppChainModularBundlerFixture.COLLECTION_NAME;
    }

    /// <summary>
    /// Deploys the full modernized AppChain AA stack via <see cref="AADeployer"/> (EntryPoint, Registry,
    /// factory, SponsoredPaymaster, and the ERC-7579 module contracts) on a DevChain + in-process bundler,
    /// and exposes the <see cref="AppChainAccountAdminService"/> over it - the fixture the provisioning E2E
    /// uses to prove a provisioned AppChain account is a real modular account whose modules can actually
    /// be installed.
    /// </summary>
    public class AppChainModularBundlerFixture : IAsyncLifetime
    {
        public const string COLLECTION_NAME = "AppChainModularBundler";
        public const int CHAIN_ID = 31337;

        private const string OPERATOR_PRIVATE_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string BUNDLER_PRIVATE_KEY = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";

        public DevChainNode Node { get; private set; } = null!;
        public IWeb3 OperatorWeb3 { get; private set; } = null!;
        public AppChainDeployment Deployment { get; private set; } = null!;
        public AppChainAccountAdminService AdminService { get; private set; } = null!;
        public IAccountAbstractionBundlerService Bundler { get; private set; } = null!;

        public string WebAuthnValidatorAddress { get; private set; } = null!;

        public ValueCapCombinatorService ValueCapCombinator { get; private set; } = null!;
        public byte[] CapRuleId { get; private set; } = null!;
        public PayableTargetService PayableTarget { get; private set; } = null!;

        public OwnableValidatorService OwnableValidator { get; private set; } = null!;

        private BundlerService _bundlerService = null!;

        public async Task InitializeAsync()
        {
            var operatorAccount = new Web3Account(OPERATOR_PRIVATE_KEY, CHAIN_ID);
            var bundlerAccount = new Web3Account(BUNDLER_PRIVATE_KEY, CHAIN_ID);

            Node = new DevChainNode(new DevChainConfig
            {
                ChainId = CHAIN_ID,
                BaseFee = 1_000_000_000,
                BlockGasLimit = 30_000_000,
                AutoMine = true
            });
            await Node.StartAsync(
                new[] { operatorAccount.Address, bundlerAccount.Address },
                Nethereum.Web3.Web3.Convert.ToWei(100000));

            var registry = new RpcHandlerRegistry();
            registry.AddStandardHandlers();
            var dispatcher = new RpcDispatcher(registry, new RpcContext(Node, CHAIN_ID, new EmptyServiceProvider()));
            var rpcClient = new DevChainRpcClient(dispatcher);
            OperatorWeb3 = new Web3.Web3(operatorAccount, rpcClient);

            var config = new AppChainConfig { Owner = operatorAccount.Address, ChainId = CHAIN_ID };
            Deployment = await new AADeployer(OperatorWeb3).DeployAsync(config);

            var webAuthnValidator = await WebAuthnValidatorService.DeployContractAndGetServiceAsync(
                OperatorWeb3, new WebAuthnValidatorDeployment());
            WebAuthnValidatorAddress = webAuthnValidator.ContractAddress;

            var bundlerWeb3 = new Web3.Web3(bundlerAccount, rpcClient);
            _bundlerService = new BundlerService(bundlerWeb3, new BundlerConfig
            {
                SupportedEntryPoints = new[] { Deployment.EntryPointAddress },
                BeneficiaryAddress = bundlerAccount.Address,
                MaxBundleSize = 10,
                MaxMempoolSize = 100,
                AutoBundleIntervalMs = 0,
                StrictValidation = false,
                UnsafeMode = true,
                ChainId = CHAIN_ID
            });
            Bundler = new BundlerServiceAdapter(_bundlerService, CHAIN_ID);

            AdminService = new AppChainAccountAdminService(OperatorWeb3, Deployment);

            var ruleRegistry = await RuleRegistryService.DeployContractAndGetServiceAsync(
                OperatorWeb3, new RuleRegistryDeployment());
            var valueCapRuleReceipt = await ValueCapRuleService.DeployContractAndWaitForReceiptAsync(
                OperatorWeb3, new ValueCapRuleDeployment());
            CapRuleId = Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes("value-cap-p1d"));
            await ruleRegistry.RegisterRuleRequestAndWaitForReceiptAsync(CapRuleId, valueCapRuleReceipt.ContractAddress);
            ValueCapCombinator = await ValueCapCombinatorService.DeployContractAndGetServiceAsync(
                OperatorWeb3, new ValueCapCombinatorDeployment { Registry = ruleRegistry.ContractAddress });
            PayableTarget = await PayableTargetService.DeployContractAndGetServiceAsync(
                OperatorWeb3, new PayableTargetDeployment());
            OwnableValidator = await OwnableValidatorService.DeployContractAndGetServiceAsync(
                OperatorWeb3, new OwnableValidatorDeployment());
        }

        public Task DisposeAsync()
        {
            _bundlerService?.Dispose();
            Node?.Dispose();
            return Task.CompletedTask;
        }
    }
}
