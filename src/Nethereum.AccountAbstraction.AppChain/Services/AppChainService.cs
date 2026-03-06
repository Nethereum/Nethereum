using System.Numerics;
using Nethereum.AccountAbstraction.AppChain.Configuration;
using Nethereum.AccountAbstraction.AppChain.Contracts.Paymaster.SponsoredPaymaster;
using Nethereum.AccountAbstraction.AppChain.Contracts.Policy.AccountRegistry;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.AccountAbstraction.AppChain.Interfaces;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.AppChain.Services
{
    public class AppChainService
    {
        private readonly IWeb3 _web3;
        private readonly AppChainDeployment _deployment;
        private readonly NethereumAccountFactoryService _factory;
        private readonly AccountRegistryService _registry;
        private readonly SponsoredPaymasterService _paymaster;
        private readonly EntryPointService _entryPoint;

        public AppChainService(IWeb3 web3, AppChainDeployment deployment)
        {
            _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            _deployment = deployment ?? throw new ArgumentNullException(nameof(deployment));

            _factory = new NethereumAccountFactoryService(web3, deployment.AccountFactoryAddress);
            _registry = new AccountRegistryService(web3, deployment.AccountRegistryAddress);
            _paymaster = new SponsoredPaymasterService(web3, deployment.SponsoredPaymasterAddress);
            _entryPoint = new EntryPointService(web3, deployment.EntryPointAddress);
        }

        public string EntryPointAddress => _deployment.EntryPointAddress;
        public string AccountFactoryAddress => _deployment.AccountFactoryAddress;
        public string AccountRegistryAddress => _deployment.AccountRegistryAddress;
        public string SponsoredPaymasterAddress => _deployment.SponsoredPaymasterAddress;

        public async Task<string> InviteUserAsync(string userAddress)
        {
            var receipt = await _registry.InviteRequestAndWaitForReceiptAsync(userAddress);
            return receipt.TransactionHash;
        }

        public async Task<string> BanUserAsync(string userAddress, string reason)
        {
            var receipt = await _registry.BanRequestAndWaitForReceiptAsync(userAddress, reason);
            return receipt.TransactionHash;
        }

        public async Task<bool> IsInvitedAsync(string userAddress)
        {
            var status = await _registry.GetStatusQueryAsync(userAddress);
            return status != (byte)AccountStatus.None;
        }

        public async Task<bool> IsActiveAsync(string userAddress)
        {
            var status = await _registry.GetStatusQueryAsync(userAddress);
            return status == (byte)AccountStatus.Active;
        }

        public async Task<string> GetAccountAddressAsync(byte[] salt, byte[] initData)
        {
            return await _factory.GetAddressQueryAsync(salt, initData);
        }

        public async Task<bool> IsAccountDeployedAsync(string accountAddress)
        {
            var code = await _web3.Eth.GetCode.SendRequestAsync(accountAddress);
            return code != null && code != "0x" && code != "0x0";
        }

        public async Task<string> CreateAccountAsync(byte[] salt, byte[] initData)
        {
            var receipt = await _factory.CreateAccountRequestAndWaitForReceiptAsync(salt, initData);
            return receipt.ContractAddress;
        }

        public async Task<string> ActivateAccountAsync(string accountAddress)
        {
            var receipt = await _registry.ActivateAccountRequestAndWaitForReceiptAsync(accountAddress);
            return receipt.TransactionHash;
        }

        public async Task<BigInteger> GetNonceAsync(string sender, BigInteger key = default)
        {
            return await _entryPoint.GetNonceQueryAsync(sender, key);
        }

        public NethereumAccountService GetAccountService(string accountAddress)
        {
            return new NethereumAccountService(_web3, accountAddress);
        }

        public EntryPointService GetEntryPointService()
        {
            return _entryPoint;
        }

        public AccountRegistryService GetAccountRegistryService()
        {
            return _registry;
        }

        public SponsoredPaymasterService GetSponsoredPaymasterService()
        {
            return _paymaster;
        }

        public NethereumAccountFactoryService GetAccountFactoryService()
        {
            return _factory;
        }
    }
}
