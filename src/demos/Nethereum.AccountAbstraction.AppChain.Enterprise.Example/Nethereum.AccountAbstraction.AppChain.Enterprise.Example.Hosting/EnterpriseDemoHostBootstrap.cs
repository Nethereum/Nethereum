using System.Numerics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.AppChain.Configuration;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core;
using Nethereum.AccountAbstraction.AppChain.Services;
using Nethereum.AccountAbstraction.Bundler.InProcess;
using Nethereum.AccountAbstraction.Client;
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
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.RPC;
using Nethereum.Util;
using Nethereum.Web3;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Hosting
{
    public sealed class EnterpriseDemoHostBootstrap
    {
        public const int ChainId = 31337;

        private const string CapRuleName = "value-cap-p1d";

        private const string OwnerPrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string BundlerPrivateKey = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";

        public InProcessBundlerHost Bootstrap { get; }
        public ProvisionedEnterpriseInfra Infra { get; }

        private EnterpriseDemoHostBootstrap(InProcessBundlerHost bootstrap, ProvisionedEnterpriseInfra infra)
        {
            Bootstrap = bootstrap;
            Infra = infra;
        }

        public void PublishInto(SessionState session) => session.PublishInfra(Infra);

        public static async Task<EnterpriseDemoHostBootstrap> StartAsync(Action<string>? log = null)
        {
            log ??= _ => { };
            var operatorAccount = new Web3Account(OwnerPrivateKey, ChainId);
            var bundlerAccount = new Web3Account(BundlerPrivateKey, ChainId);

            log("Starting in-process DevChain...");
            var bootstrap = await InProcessBundlerHost.StartAsync(
                operatorAccount,
                ChainId,
                new[] { operatorAccount.Address, bundlerAccount.Address },
                Web3.Web3.Convert.ToWei(100000));

            log("Deploying the EntryPoint...");
            var entryPoint = await EntryPointService.DeployContractAndGetServiceAsync(
                bootstrap.OperatorWeb3, new EntryPointDeployment()).ConfigureAwait(false);
            log($"EntryPoint deployed at {entryPoint.ContractAddress}");

            log("Starting in-process bundler (ERC-4337 validation ON)...");
            var bundler = bootstrap.StartBundler(entryPoint.ContractAddress, bundlerAccount);

            var infra = await DeployEnterpriseStackAsync(bootstrap.OperatorWeb3, bundler, ChainId, log).ConfigureAwait(false);
            infra = infra with { Resource = bootstrap };

            log("Enterprise AppChain infrastructure ready.");
            return new EnterpriseDemoHostBootstrap(bootstrap, infra);
        }

        public static async Task<ProvisionedEnterpriseInfra> DeployEnterpriseStackAsync(
            IWeb3 funderWeb3, IAccountAbstractionBundlerService bundler, BigInteger chainId, Action<string>? log = null)
        {
            if (funderWeb3 is null) throw new ArgumentNullException(nameof(funderWeb3));
            if (bundler is null) throw new ArgumentNullException(nameof(bundler));
            log ??= _ => { };

            var operatorAddress = funderWeb3.TransactionManager.Account.Address;

            var supportedEntryPoints = await bundler.SupportedEntryPoints.SendRequestAsync().ConfigureAwait(false);
            if (supportedEntryPoints is null || supportedEntryPoints.Length == 0)
                throw new InvalidOperationException(
                    "The bundler reports no supported EntryPoints (eth_supportedEntryPoints) - it must be configured against a deployed EntryPoint before the stack can be deployed.");

            log("Deploying the AppChain AA stack (EntryPoint, AccountRegistry, factory, SponsoredPaymaster, modules)...");
            var config = new AppChainConfig
            {
                Owner = operatorAddress,
                ChainId = chainId,
                EntryPointAddress = supportedEntryPoints[0]
            };
            var deployment = await new AADeployer(funderWeb3).DeployAsync(config).ConfigureAwait(false);
            log($"Using EntryPoint {deployment.EntryPointAddress} (from the bundler's own eth_supportedEntryPoints)");

            log("Deploying the P1-D enterprise rule stack (RuleRegistry, ValueCapRule, ValueCapCombinator, PayableTarget)...");
            var ruleRegistry = await RuleRegistryService.DeployContractAndGetServiceAsync(
                funderWeb3, new RuleRegistryDeployment()).ConfigureAwait(false);
            var valueCapRuleReceipt = await ValueCapRuleService.DeployContractAndWaitForReceiptAsync(
                funderWeb3, new ValueCapRuleDeployment()).ConfigureAwait(false);
            var capRuleId = Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes(CapRuleName));
            await ruleRegistry.RegisterRuleRequestAndWaitForReceiptAsync(capRuleId, valueCapRuleReceipt.ContractAddress).ConfigureAwait(false);
            var valueCapCombinator = await ValueCapCombinatorService.DeployContractAndGetServiceAsync(
                funderWeb3,
                new ValueCapCombinatorDeployment { Registry = ruleRegistry.ContractAddress }).ConfigureAwait(false);
            var payableTarget = await PayableTargetService.DeployContractAndGetServiceAsync(
                funderWeb3, new PayableTargetDeployment()).ConfigureAwait(false);
            log($"RuleRegistry {ruleRegistry.ContractAddress}, ValueCapCombinator {valueCapCombinator.ContractAddress}, PayableTarget {payableTarget.ContractAddress}");

            log("Deploying the tier-2 OwnableValidator (N-of-M member-quorum session validator)...");
            var ownableValidator = await OwnableValidatorService.DeployContractAndGetServiceAsync(
                funderWeb3, new OwnableValidatorDeployment()).ConfigureAwait(false);
            log($"OwnableValidator {ownableValidator.ContractAddress}");

            var adminService = new AppChainAccountAdminService(funderWeb3, deployment);
            log("Building the IAAClient on-ramp...");
            var client = BuildClient(funderWeb3, deployment, bundler);

            return new ProvisionedEnterpriseInfra(
                deployment,
                funderWeb3,
                client,
                adminService,
                bundler,
                capRuleId,
                valueCapCombinator.ContractAddress,
                payableTarget.ContractAddress,
                ownableValidator.ContractAddress,
                Resource: null);
        }

        private static IAAClient BuildClient(IWeb3 operatorWeb3, AppChainDeployment deployment, IAccountAbstractionBundlerService bundler)
        {
            var services = new ServiceCollection();
            services.AddNethereumAccountAbstraction(o => o
                .UseWeb3(operatorWeb3)
                .UseDeploymentAddresses(deployment.ToAADeploymentAddresses())
                .UseBundler(bundler));
            return services.BuildServiceProvider().GetRequiredService<IAAClient>();
        }
    }
}
