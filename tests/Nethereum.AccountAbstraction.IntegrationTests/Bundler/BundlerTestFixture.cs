using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition;
using Nethereum.Signer;
using Nethereum.Web3;
using Nethereum.XUnitEthereumClients;
using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Bundler
{
    [CollectionDefinition(BundlerTestFixture.BUNDLER_COLLECTION)]
    public class BundlerTestCollection : ICollectionFixture<BundlerTestFixture>
    {
    }

    public class BundlerTestFixture : IAsyncLifetime
    {
        public const string BUNDLER_COLLECTION = "Bundler Test Collection";

        private readonly EthereumClientIntegrationFixture _ethereumFixture;

        public IWeb3 Web3 { get; private set; } = null!;
        public EntryPointService EntryPointService { get; private set; } = null!;
        public SimpleAccountFactoryService AccountFactoryService { get; private set; } = null!;
        public BundlerService BundlerService { get; private set; } = null!;
        public BundlerConfig BundlerConfig { get; private set; } = null!;

        public string BeneficiaryAddress => EthereumClientIntegrationFixture.AccountAddress;
        public string OperatorPrivateKey => EthereumClientIntegrationFixture.AccountPrivateKey;
        public EthECKey OperatorKey { get; private set; } = null!;
        public BigInteger ChainId => EthereumClientIntegrationFixture.ChainId;

        public BundlerTestFixture()
        {
            _ethereumFixture = SharedEthereumFixture.GetOrCreate();
        }

        public async Task InitializeAsync()
        {
            Web3 = _ethereumFixture.GetWeb3();
            OperatorKey = new EthECKey(OperatorPrivateKey);

            EntryPointService = await EntryPointService.DeployContractAndGetServiceAsync(
                Web3, new EntryPointDeployment());

            var factoryDeployment = new SimpleAccountFactoryDeployment
            {
                EntryPoint = EntryPointService.ContractAddress
            };

            AccountFactoryService = await SimpleAccountFactoryService.DeployContractAndGetServiceAsync(
                Web3, factoryDeployment);

            BundlerConfig = new BundlerConfig
            {
                SupportedEntryPoints = new[] { EntryPointService.ContractAddress },
                BeneficiaryAddress = BeneficiaryAddress,
                MaxBundleSize = 10,
                MaxMempoolSize = 100,
                MinPriorityFeePerGas = 0,
                MaxBundleGas = 15_000_000,
                AutoBundleIntervalMs = 0,
                StrictValidation = false,
                SimulateValidation = false,
                UnsafeMode = true,
                ChainId = ChainId
            };

            BundlerService = new BundlerService(Web3, BundlerConfig);
        }

        public Task DisposeAsync()
        {
            BundlerService?.Dispose();
            SharedEthereumFixture.Release();
            return Task.CompletedTask;
        }

        public async Task<(string accountAddress, EthECKey accountKey)> CreateFundedAccountAsync(
            ulong salt,
            decimal ethAmount = 0.01m)
        {
            var accountKey = new EthECKey(TestAccounts.Account2PrivateKey);
            var ownerAddress = accountKey.GetPublicAddress();

            var result = await AccountFactoryService.CreateAndDeployAccountAsync(
                ownerAddress,
                ownerAddress,
                EntryPointService.ContractAddress,
                accountKey,
                ethAmount,
                salt);

            return (result.AccountAddress, accountKey);
        }

        public async Task<string> GetAccountAddressAsync(string owner, ulong salt)
        {
            return await AccountFactoryService.GetAddressQueryAsync(owner, salt);
        }

        public async Task FundAccountAsync(string address, decimal ethAmount)
        {
            await Web3.Eth.GetEtherTransferService()
                .TransferEtherAndWaitForReceiptAsync(address, ethAmount);
        }

        public BundlerService CreateNewBundlerService(BundlerConfig? config = null)
        {
            return new BundlerService(Web3, config ?? BundlerConfig);
        }
    }
}
