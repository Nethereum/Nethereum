using Nethereum.AccountAbstraction.AppChain.Contracts.Paymaster.SponsoredPaymaster;
using Nethereum.AccountAbstraction.AppChain.Contracts.Paymaster.SponsoredPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.AppChain.Contracts.Policy.AccountRegistry;
using Nethereum.AccountAbstraction.AppChain.Contracts.Policy.AccountRegistry.ContractDefinition;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.XUnitEthereumClients;
using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.Fixtures
{
    public partial class AAIntegrationFixture : IAsyncLifetime
    {
        public const string COLLECTION_NAME = "AAIntegration";
    }

    [CollectionDefinition(AAIntegrationFixture.COLLECTION_NAME, DisableParallelization = true)]
    public class AAIntegrationCollection : ICollectionFixture<AAIntegrationFixture> { }

    public partial class AAIntegrationFixture
    {

        public IWeb3 Web3 { get; private set; } = null!;
        public AppChainDeployment Contracts { get; private set; } = null!;
        public EntryPointService EntryPointService { get; private set; } = null!;
        public AccountRegistryService AccountRegistryService { get; private set; } = null!;
        public SponsoredPaymasterService SponsoredPaymasterService { get; private set; } = null!;

        public Account OperatorAccount { get; private set; } = null!;
        public Account BundlerAccount { get; private set; } = null!;
        public Account[] UserAccounts { get; private set; } = null!;

        private EthereumClientIntegrationFixture _clientFixture = null!;

        public async Task InitializeAsync()
        {
            _clientFixture = new EthereumClientIntegrationFixture();

            OperatorAccount = new Account(
                EthereumClientIntegrationFixture.AccountPrivateKey,
                EthereumClientIntegrationFixture.ChainId);

            Web3 = _clientFixture.GetWeb3();

            BundlerAccount = CreateTestAccount(1);
            UserAccounts = new Account[5];
            for (int i = 0; i < 5; i++)
            {
                UserAccounts[i] = CreateTestAccount(i + 2);
            }

            await FundAccountsAsync();

            EntryPointService = await EntryPointService.DeployContractAndGetServiceAsync(
                Web3, new EntryPointDeployment());

            Contracts = new AppChainDeployment
            {
                EntryPointAddress = EntryPointService.ContractAddress
            };

            var accountRegistryDeployment = new AccountRegistryDeployment
            {
                InitialAdmin = OperatorAccount.Address
            };
            AccountRegistryService = await AccountRegistryService.DeployContractAndGetServiceAsync(
                Web3, accountRegistryDeployment);

            var sponsoredPaymasterDeployment = new SponsoredPaymasterDeployment
            {
                EntryPoint = EntryPointService.ContractAddress,
                Owner = OperatorAccount.Address,
                Registry = AccountRegistryService.ContractAddress,
                MaxPerUser = Nethereum.Web3.Web3.Convert.ToWei(1),
                MaxTotal = Nethereum.Web3.Web3.Convert.ToWei(10)
            };
            SponsoredPaymasterService = await SponsoredPaymasterService.DeployContractAndGetServiceAsync(
                Web3, sponsoredPaymasterDeployment);

            var depositAmount = Nethereum.Web3.Web3.Convert.ToWei(5);
            await SponsoredPaymasterService.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = depositAmount });
        }

        public async Task DisposeAsync()
        {
            _clientFixture?.Dispose();
            await Task.CompletedTask;
        }

        private Account CreateTestAccount(int index)
        {
            var keyBytes = new byte[32];
            keyBytes[31] = (byte)(index + 1);
            var privateKey = "0x" + BitConverter.ToString(keyBytes).Replace("-", "").ToLowerInvariant();
            return new Account(privateKey, EthereumClientIntegrationFixture.ChainId);
        }

        private async Task FundAccountsAsync()
        {
            var fundAmount = Nethereum.Web3.Web3.Convert.ToWei(1);

            await FundAccountAsync(BundlerAccount.Address, fundAmount);

            foreach (var user in UserAccounts)
            {
                await FundAccountAsync(user.Address, fundAmount);
            }
        }

        private async Task FundAccountAsync(string address, BigInteger amount)
        {
            var txHash = await Web3.Eth.GetEtherTransferService()
                .TransferEtherAndWaitForReceiptAsync(address, Nethereum.Web3.Web3.Convert.FromWei(amount));
        }

        public IWeb3 GetWeb3ForAccount(Account account)
        {
            return new Web3.Web3(account, _clientFixture.GetClient());
        }

        public AccountRegistryService GetAccountRegistryServiceForAccount(Account account)
        {
            var web3 = GetWeb3ForAccount(account);
            return new AccountRegistryService(web3, AccountRegistryService.ContractAddress);
        }

        public SponsoredPaymasterService GetSponsoredPaymasterServiceForAccount(Account account)
        {
            var web3 = GetWeb3ForAccount(account);
            return new SponsoredPaymasterService(web3, SponsoredPaymasterService.ContractAddress);
        }
    }
}
