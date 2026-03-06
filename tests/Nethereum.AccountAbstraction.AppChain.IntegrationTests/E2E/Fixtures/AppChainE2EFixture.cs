using System;
using System.Numerics;
using Nethereum.AccountAbstraction.AppChain.Contracts.Paymaster.SponsoredPaymaster;
using Nethereum.AccountAbstraction.AppChain.Contracts.Paymaster.SponsoredPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.AppChain.Contracts.Policy.AccountRegistry;
using Nethereum.AccountAbstraction.AppChain.Contracts.Policy.AccountRegistry.ContractDefinition;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator.ContractDefinition;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.CoreChain.Rpc;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Fixtures
{
    internal class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    public partial class AppChainE2EFixture : IAsyncLifetime
    {
        public const string COLLECTION_NAME = "AppChainE2E";
        public const int CHAIN_ID = 31337;
    }

    [CollectionDefinition(AppChainE2EFixture.COLLECTION_NAME, DisableParallelization = true)]
    public class AppChainE2ECollection : ICollectionFixture<AppChainE2EFixture> { }

    public partial class AppChainE2EFixture
    {
        public DevChainNode Node { get; private set; } = null!;
        public RpcDispatcher Dispatcher { get; private set; } = null!;
        public IWeb3 Web3 { get; private set; } = null!;
        public BundlerService BundlerService { get; private set; } = null!;

        public AppChainDeployment Contracts { get; private set; } = null!;
        public EntryPointService EntryPointService { get; private set; } = null!;
        public AccountRegistryService AccountRegistryService { get; private set; } = null!;
        public NethereumAccountFactoryService AccountFactoryService { get; private set; } = null!;
        public ECDSAValidatorService ECDSAValidatorService { get; private set; } = null!;
        public SponsoredPaymasterService SponsoredPaymasterService { get; private set; } = null!;

        public Account OperatorAccount { get; private set; } = null!;
        public Account BundlerAccount { get; private set; } = null!;
        public Account[] UserAccounts { get; private set; } = null!;

        private DevChainRpcClient _rpcClient = null!;

        public async Task InitializeAsync()
        {
            var operatorPrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
            OperatorAccount = new Account(operatorPrivateKey, CHAIN_ID);

            BundlerAccount = CreateTestAccount(1);
            UserAccounts = new Account[5];
            for (int i = 0; i < 5; i++)
            {
                UserAccounts[i] = CreateTestAccount(i + 2);
            }

            var config = new DevChainConfig
            {
                ChainId = CHAIN_ID,
                BaseFee = 1_000_000_000,
                BlockGasLimit = 30_000_000,
                AutoMine = true
            };

            Node = new DevChainNode(config);

            var prefundedAddresses = new List<string>
            {
                OperatorAccount.Address,
                BundlerAccount.Address
            };
            prefundedAddresses.AddRange(UserAccounts.Select(a => a.Address));

            await Node.StartAsync(prefundedAddresses, Nethereum.Web3.Web3.Convert.ToWei(100));

            var registry = new RpcHandlerRegistry();
            registry.AddStandardHandlers();

            var services = new EmptyServiceProvider();
            var context = new RpcContext(Node, CHAIN_ID, services);
            Dispatcher = new RpcDispatcher(registry, context);

            _rpcClient = new DevChainRpcClient(Dispatcher);
            Web3 = new Web3.Web3(OperatorAccount, _rpcClient);

            try
            {
                await DeployContractsAsync();
                await SetupBundlerServiceAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize E2E fixture: {ex.Message}", ex);
            }
        }

        private async Task DeployContractsAsync()
        {
            EntryPointService = await EntryPointService.DeployContractAndGetServiceAsync(
                Web3, new EntryPointDeployment());

            var accountRegistryDeployment = new AccountRegistryDeployment
            {
                InitialAdmin = OperatorAccount.Address
            };
            AccountRegistryService = await AccountRegistryService.DeployContractAndGetServiceAsync(
                Web3, accountRegistryDeployment);

            var accountFactoryDeployment = new NethereumAccountFactoryDeployment
            {
                EntryPoint = EntryPointService.ContractAddress
            };
            AccountFactoryService = await NethereumAccountFactoryService.DeployContractAndGetServiceAsync(
                Web3, accountFactoryDeployment);

            ECDSAValidatorService = await ECDSAValidatorService.DeployContractAndGetServiceAsync(
                Web3, new ECDSAValidatorDeployment());

            var sponsoredPaymasterDeployment = new SponsoredPaymasterDeployment
            {
                EntryPoint = EntryPointService.ContractAddress,
                Owner = OperatorAccount.Address,
                Registry = AccountRegistryService.ContractAddress,
                MaxPerUser = Nethereum.Web3.Web3.Convert.ToWei(10),
                MaxTotal = Nethereum.Web3.Web3.Convert.ToWei(100)
            };
            SponsoredPaymasterService = await SponsoredPaymasterService.DeployContractAndGetServiceAsync(
                Web3, sponsoredPaymasterDeployment);

            var depositAmount = Nethereum.Web3.Web3.Convert.ToWei(50);
            await SponsoredPaymasterService.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = depositAmount });

            Contracts = new AppChainDeployment
            {
                EntryPointAddress = EntryPointService.ContractAddress,
                AccountRegistryAddress = AccountRegistryService.ContractAddress,
                AccountFactoryAddress = AccountFactoryService.ContractAddress,
                SponsoredPaymasterAddress = SponsoredPaymasterService.ContractAddress
            };
        }

        private Task SetupBundlerServiceAsync()
        {
            var bundlerWeb3 = new Web3.Web3(BundlerAccount, _rpcClient);

            var bundlerConfig = new BundlerConfig
            {
                SupportedEntryPoints = new[] { EntryPointService.ContractAddress },
                BeneficiaryAddress = BundlerAccount.Address,
                MaxBundleSize = 10,
                MaxMempoolSize = 100,
                AutoBundleIntervalMs = 0,
                StrictValidation = false,
                UnsafeMode = true,
                ChainId = CHAIN_ID
            };

            BundlerService = new BundlerService(bundlerWeb3, bundlerConfig);

            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            BundlerService?.Dispose();
            Node?.Dispose();
            await Task.CompletedTask;
        }

        private Account CreateTestAccount(int index)
        {
            var keyBytes = new byte[32];
            keyBytes[31] = (byte)(index + 1);
            var privateKey = "0x" + BitConverter.ToString(keyBytes).Replace("-", "").ToLowerInvariant();
            return new Account(privateKey, CHAIN_ID);
        }

        public IWeb3 GetWeb3ForAccount(Account account)
        {
            return new Web3.Web3(account, _rpcClient);
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

        public NethereumAccountFactoryService GetAccountFactoryServiceForAccount(Account account)
        {
            var web3 = GetWeb3ForAccount(account);
            return new NethereumAccountFactoryService(web3, AccountFactoryService.ContractAddress);
        }

        public async Task<CoreChain.Storage.IStateSnapshot> TakeSnapshotAsync()
        {
            return await Node.TakeSnapshotAsync();
        }

        public async Task RevertToSnapshotAsync(CoreChain.Storage.IStateSnapshot snapshot)
        {
            await Node.RevertToSnapshotAsync(snapshot);
        }

        public async Task<BigInteger> GetBalanceAsync(string address)
        {
            return await Web3.Eth.GetBalance.SendRequestAsync(address);
        }

        public async Task SetBalanceAsync(string address, BigInteger balance)
        {
            await Node.SetBalanceAsync(address, balance);
        }

        public async Task MineBlockAsync()
        {
            await Node.MineBlockAsync();
        }

        public async Task<string?> ExecuteBundleAsync()
        {
            var result = await BundlerService.ExecuteBundleAsync();
            return result?.TransactionHash;
        }

        public async Task ResetBundlerServiceAsync()
        {
            BundlerService?.Dispose();

            _bundlerAccountIndex++;
            BundlerAccount = CreateTestAccount(100 + _bundlerAccountIndex);

            await Node.SetBalanceAsync(BundlerAccount.Address, Nethereum.Web3.Web3.Convert.ToWei(100));

            var bundlerWeb3 = new Web3.Web3(BundlerAccount, _rpcClient);

            var bundlerConfig = new BundlerConfig
            {
                SupportedEntryPoints = new[] { EntryPointService.ContractAddress },
                BeneficiaryAddress = BundlerAccount.Address,
                MaxBundleSize = 10,
                MaxMempoolSize = 100,
                AutoBundleIntervalMs = 0,
                StrictValidation = false,
                UnsafeMode = true,
                ChainId = CHAIN_ID
            };

            BundlerService = new BundlerService(bundlerWeb3, bundlerConfig);
        }

        private int _bundlerAccountIndex = 0;

        public byte[] EncodeInitData(string ownerAddress)
        {
            return ByteUtil.Merge(
                ECDSAValidatorService.ContractAddress.HexToByteArray(),
                ownerAddress.HexToByteArray());
        }
    }
}
