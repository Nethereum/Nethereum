using Nethereum.AccountAbstraction.AppChain.Configuration;
using Nethereum.AccountAbstraction.AppChain.Contracts.Paymaster.SponsoredPaymaster;
using Nethereum.AccountAbstraction.AppChain.Contracts.Paymaster.SponsoredPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.AppChain.Contracts.Policy.AccountRegistry;
using Nethereum.AccountAbstraction.AppChain.Contracts.Policy.AccountRegistry.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.Deployment;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.Util;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.AppChain.Deployment
{
    public class AADeployer
    {
        private readonly IWeb3 _web3;

        public AADeployer(IWeb3 web3)
        {
            _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
        }

        public async Task<AppChainDeployment> DeployAsync(AppChainConfig config)
        {
            var deployment = new AppChainDeployment();

            deployment.EntryPointAddress = await ResolveEntryPointAsync(config.EntryPointAddress);

            deployment.AccountFactoryAddress = config.AccountFactoryAddress
                ?? await DeployAccountFactoryAsync(deployment.EntryPointAddress);

            deployment.AccountRegistryAddress = await DeployAccountRegistryAsync(config.Owner);

            deployment.SponsoredPaymasterAddress = await DeploySponsoredPaymasterAsync(
                deployment.EntryPointAddress,
                deployment.AccountRegistryAddress,
                config.Owner);

            if (config.InitialPaymasterDeposit > 0)
            {
                await DepositToPaymasterAsync(
                    deployment.EntryPointAddress,
                    deployment.SponsoredPaymasterAddress,
                    config.InitialPaymasterDeposit);
            }

            foreach (var admin in config.Admins)
            {
                await GrantAdminRoleAsync(deployment.AccountRegistryAddress, admin);
            }

            deployment.Modules = await ResolveModulesAsync(config.DefaultModules);

            return deployment;
        }

        private Task<AAModuleAddresses> ResolveModulesAsync(DefaultModulesConfig modules)
        {
            if (modules.ModuleAddresses != null)
            {
                ValidateSuppliedModules(modules);
                return Task.FromResult(modules.ModuleAddresses);
            }

            var options = new AAModuleDeploymentOptions
            {
                EcdsaValidator = modules.InstallOwnerValidator,
                SmartSession = modules.InstallSessionKeys,
                SocialRecovery = modules.InstallSocialRecovery,
                OwnableExecutor = false
            };
            return new AAModuleDeployer(_web3).DeployAsync(options);
        }

        private static void ValidateSuppliedModules(DefaultModulesConfig modules)
        {
            var supplied = modules.ModuleAddresses!;

            if (modules.InstallOwnerValidator && string.IsNullOrEmpty(supplied.EcdsaValidator))
                throw new InvalidOperationException(
                    "DefaultModules.ModuleAddresses was supplied but InstallOwnerValidator is set and EcdsaValidator is missing.");

            if (modules.InstallSessionKeys && (
                    string.IsNullOrEmpty(supplied.SmartSession) ||
                    string.IsNullOrEmpty(supplied.SudoPolicy) ||
                    string.IsNullOrEmpty(supplied.UniActionPolicy) ||
                    string.IsNullOrEmpty(supplied.EcdsaSessionValidator)))
                throw new InvalidOperationException(
                    "DefaultModules.ModuleAddresses was supplied but InstallSessionKeys is set and the SmartSession stack " +
                    "(SmartSession, SudoPolicy, UniActionPolicy, EcdsaSessionValidator) is incomplete.");

            if (modules.InstallSocialRecovery && string.IsNullOrEmpty(supplied.SocialRecovery))
                throw new InvalidOperationException(
                    "DefaultModules.ModuleAddresses was supplied but InstallSocialRecovery is set and SocialRecovery is missing.");
        }

        public AppChainDeployment GetDeployment(
            string entryPointAddress,
            string accountFactoryAddress,
            string accountRegistryAddress,
            string sponsoredPaymasterAddress)
        {
            return new AppChainDeployment
            {
                EntryPointAddress = entryPointAddress,
                AccountFactoryAddress = accountFactoryAddress,
                AccountRegistryAddress = accountRegistryAddress,
                SponsoredPaymasterAddress = sponsoredPaymasterAddress
            };
        }

        private async Task<string> ResolveEntryPointAsync(string configuredEntryPointAddress)
        {
            if (!string.IsNullOrEmpty(configuredEntryPointAddress) &&
                !AddressUtil.Current.IsNullEmptyOrZeroAddress(configuredEntryPointAddress) &&
                await IsDeployedAsync(configuredEntryPointAddress))
            {
                await ValidateIsEntryPointAsync(configuredEntryPointAddress);
                return configuredEntryPointAddress;
            }

            return await DeployEntryPointAsync();
        }

        private async Task ValidateIsEntryPointAsync(string address)
        {
            try
            {
                var entryPoint = new EntryPointService(_web3, address);
                await entryPoint.GetNonceQueryAsync(AddressUtil.ZERO_ADDRESS, 0);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Configured EntryPoint address {address} has code but does not respond as an ERC-4337 " +
                    $"EntryPoint (getNonce failed): {ex.Message}", ex);
            }
        }


        private async Task<string> DeployEntryPointAsync()
        {
            var deployment = new EntryPointDeployment();
            var receipt = await EntryPointService.DeployContractAndWaitForReceiptAsync(_web3, deployment);
            return receipt.ContractAddress;
        }

        private async Task<string> DeployAccountFactoryAsync(string entryPointAddress)
        {
            var deployment = new NethereumAccountFactoryDeployment
            {
                EntryPoint = entryPointAddress
            };
            var receipt = await NethereumAccountFactoryService.DeployContractAndWaitForReceiptAsync(_web3, deployment);
            return receipt.ContractAddress;
        }

        private async Task<string> DeployAccountRegistryAsync(string initialAdmin)
        {
            var deployment = new AccountRegistryDeployment { InitialAdmin = initialAdmin };
            var receipt = await AccountRegistryService.DeployContractAndWaitForReceiptAsync(_web3, deployment);
            return receipt.ContractAddress;
        }

        private async Task<string> DeploySponsoredPaymasterAsync(
            string entryPointAddress,
            string registryAddress,
            string owner)
        {
            var deployment = new SponsoredPaymasterDeployment
            {
                EntryPoint = entryPointAddress,
                Registry = registryAddress,
                Owner = owner,
                MaxPerUser = Web3.Web3.Convert.ToWei(1),
                MaxTotal = Web3.Web3.Convert.ToWei(100)
            };
            var receipt = await SponsoredPaymasterService.DeployContractAndWaitForReceiptAsync(_web3, deployment);
            return receipt.ContractAddress;
        }

        private async Task DepositToPaymasterAsync(
            string entryPointAddress,
            string paymasterAddress,
            decimal ethAmount)
        {
            var entryPoint = new EntryPointService(_web3, entryPointAddress);
            var weiAmount = Web3.Web3.Convert.ToWei(ethAmount);
            var depositFunction = new DepositToFunction
            {
                Account = paymasterAddress,
                AmountToSend = weiAmount
            };
            await entryPoint.DepositToRequestAndWaitForReceiptAsync(depositFunction);
        }

        private async Task GrantAdminRoleAsync(string registryAddress, string admin)
        {
            var registry = new AccountRegistryService(_web3, registryAddress);
            var adminRole = await registry.AdminRoleQueryAsync();
            await registry.GrantRoleRequestAndWaitForReceiptAsync(adminRole, admin);
        }

        public async Task<bool> IsDeployedAsync(string address)
        {
            var code = await _web3.Eth.GetCode.SendRequestAsync(address);
            return !string.IsNullOrEmpty(code) && code != "0x" && code.Length > 2;
        }
    }
}
