using System;
using System.Numerics;
using Nethereum.AccountAbstraction.AppChain.Configuration;
using Nethereum.AccountAbstraction.AppChain.Contracts.Paymaster.SponsoredPaymaster;
using Nethereum.AccountAbstraction.AppChain.Contracts.Policy.AccountRegistry;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.AccountAbstraction.AppChain.Interfaces;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.AppChain.Services
{
    public class AppChainAccountAdminService
    {
        private readonly IWeb3 _web3;
        private readonly AppChainDeployment _deployment;
        private readonly NethereumAccountFactoryService _factory;
        private readonly AccountRegistryService _registry;
        private readonly SponsoredPaymasterService _paymaster;
        private readonly EntryPointService _entryPoint;

        public AppChainAccountAdminService(IWeb3 web3, AppChainDeployment deployment)
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
            await _factory.CreateAccountRequestAndWaitForReceiptAsync(salt, initData);
            return await _factory.GetAddressQueryAsync(salt, initData);
        }

        public Task<string> GetAccountAddressForConfigAsync(AppChainAccountConfig accountConfig)
        {
            var (salt, initData) = BuildOwnerInitData(accountConfig);
            return _factory.GetAddressQueryAsync(salt, initData);
        }

        public async Task<string> ProvisionAccountAsync(AppChainAccountConfig accountConfig)
        {
            var (salt, initData) = BuildOwnerInitData(accountConfig);
            var address = await _factory.GetAddressQueryAsync(salt, initData);
            await _factory.CreateAccountRequestAndWaitForReceiptAsync(salt, initData);
            return address;
        }

        public async Task<string> EnrollAccountAsync(byte[] salt, byte[] initData)
        {
            var address = await _factory.GetAddressQueryAsync(salt, initData);

            if (await _registry.GetStatusQueryAsync(address) == (byte)AccountStatus.None)
                await _registry.InviteRequestAndWaitForReceiptAsync(address);

            if (!await IsAccountDeployedAsync(address))
                await CreateAccountAsync(salt, initData);

            if (await _registry.GetStatusQueryAsync(address) == (byte)AccountStatus.Invited)
                await _registry.ActivateAccountRequestAndWaitForReceiptAsync(address);

            return address;
        }

        public Task<string> EnrollAccountAsync(AppChainAccountConfig accountConfig)
        {
            var (salt, initData) = BuildOwnerInitData(accountConfig);
            return EnrollAccountAsync(salt, initData);
        }

        private (byte[] salt, byte[] initData) BuildOwnerInitData(AppChainAccountConfig accountConfig)
        {
            if (accountConfig == null) throw new ArgumentNullException(nameof(accountConfig));
            if (string.IsNullOrEmpty(accountConfig.Owner))
                throw new InvalidOperationException("AppChainAccountConfig.Owner is required to provision an account.");
            if (string.IsNullOrEmpty(_deployment.Modules?.EcdsaValidator))
                throw new InvalidOperationException(
                    "The deployment has no ECDSAValidator module - deploy the modules (AADeployer) before provisioning modular accounts.");

            var initData = AccountInitDataBuilder.BuildEcdsa(_deployment.Modules.EcdsaValidator, accountConfig.Owner);
            return (ToSaltBytes(accountConfig.Salt), initData);
        }

        private static byte[] ToSaltBytes(BigInteger salt)
        {
            if (salt.Sign < 0)
                throw new ArgumentOutOfRangeException(nameof(salt), "Salt must be non-negative.");
            var bytes = salt.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (bytes.Length > 32)
                throw new ArgumentOutOfRangeException(nameof(salt), "Salt must fit in 32 bytes.");
            if (bytes.Length == 32) return bytes;
            var padded = new byte[32];
            Array.Copy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
            return padded;
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
