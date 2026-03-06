using Nethereum.AccountAbstraction.AppChain.Configuration;
using Nethereum.AccountAbstraction.AppChain.Contracts.Paymaster.SponsoredPaymaster;
using Nethereum.AccountAbstraction.AppChain.Contracts.Paymaster.SponsoredPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.AppChain.Contracts.Policy.AccountRegistry;
using Nethereum.AccountAbstraction.AppChain.Contracts.Policy.AccountRegistry.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
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

            deployment.EntryPointAddress = await DeployEntryPointAsync();

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

            deployment.Modules = config.DefaultModules.ModuleAddresses ?? new ModuleAddresses();

            return deployment;
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
