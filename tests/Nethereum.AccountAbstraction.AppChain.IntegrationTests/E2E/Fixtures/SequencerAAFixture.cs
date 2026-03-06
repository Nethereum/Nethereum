using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
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
using Nethereum.AppChain;
using Nethereum.AppChain.Sequencer;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Xunit;

using AppChainCore = Nethereum.AppChain.AppChain;
using AppChainSequencer = Nethereum.AppChain.Sequencer.Sequencer;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Fixtures
{
    public partial class SequencerAAFixture : IAsyncLifetime
    {
        public const string COLLECTION_NAME = "SequencerAA";
        public const int CHAIN_ID = 420420;
    }

    [CollectionDefinition(SequencerAAFixture.COLLECTION_NAME, DisableParallelization = true)]
    public class SequencerAACollection : ICollectionFixture<SequencerAAFixture> { }

    public partial class SequencerAAFixture
    {
        public AppChainCore AppChain { get; private set; } = null!;
        public AppChainSequencer Sequencer { get; private set; } = null!;
        public AppChainNode Node { get; private set; } = null!;
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

        private AppChainRpcClient _rpcClient = null!;
        private int _bundlerAccountIndex = 0;

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

            var blockStore = new InMemoryBlockStore();
            var transactionStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var stateStore = new InMemoryStateStore();

            var appChainConfig = AppChainConfig.CreateWithName("SequencerAATest", CHAIN_ID);
            appChainConfig.SequencerAddress = OperatorAccount.Address;
            appChainConfig.BaseFee = 1_000_000_000;
            appChainConfig.BlockGasLimit = 30_000_000;

            AppChain = new AppChainCore(
                appChainConfig,
                blockStore,
                transactionStore,
                receiptStore,
                logStore,
                stateStore);

            var prefundedAddresses = new List<string>
            {
                OperatorAccount.Address,
                BundlerAccount.Address
            };
            prefundedAddresses.AddRange(UserAccounts.Select(a => a.Address));

            var genesisOptions = new GenesisOptions
            {
                PrefundedAddresses = prefundedAddresses.ToArray(),
                PrefundBalance = Nethereum.Web3.Web3.Convert.ToWei(1000),
                DeployCreate2Factory = true
            };
            await AppChain.InitializeAsync(genesisOptions);

            var sequencerConfig = new SequencerConfig
            {
                SequencerAddress = OperatorAccount.Address,
                SequencerPrivateKey = operatorPrivateKey,
                BlockTimeMs = 0,
                MaxTransactionsPerBlock = 1000,
                BlockProductionMode = BlockProductionMode.OnDemand,
                Policy = PolicyConfig.OpenAccess
            };

            Sequencer = new AppChainSequencer(AppChain, sequencerConfig);
            await Sequencer.StartAsync();

            Node = new AppChainNode(AppChain, Sequencer);

            _rpcClient = new AppChainRpcClient(Node, CHAIN_ID);
            Web3 = new Web3.Web3(OperatorAccount, _rpcClient);
            Web3.TransactionManager.UseLegacyAsDefault = true;

            await DeployContractsAsync();
            await SetupBundlerServiceAsync();
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
            bundlerWeb3.TransactionManager.UseLegacyAsDefault = true;

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
            await Sequencer?.StopAsync()!;
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
            var web3 = new Web3.Web3(account, _rpcClient);
            web3.TransactionManager.UseLegacyAsDefault = true;
            return web3;
        }

        public AccountRegistryService GetAccountRegistryServiceForAccount(Account account)
        {
            var web3 = GetWeb3ForAccount(account);
            return new AccountRegistryService(web3, AccountRegistryService.ContractAddress);
        }

        public NethereumAccountFactoryService GetAccountFactoryServiceForAccount(Account account)
        {
            var web3 = GetWeb3ForAccount(account);
            return new NethereumAccountFactoryService(web3, AccountFactoryService.ContractAddress);
        }

        public SponsoredPaymasterService GetSponsoredPaymasterServiceForAccount(Account account)
        {
            var web3 = GetWeb3ForAccount(account);
            return new SponsoredPaymasterService(web3, SponsoredPaymasterService.ContractAddress);
        }

        public async Task<BigInteger> GetBalanceAsync(string address)
        {
            return await Web3.Eth.GetBalance.SendRequestAsync(address);
        }

        public async Task SetBalanceAsync(string address, BigInteger balance)
        {
            await AppChain.State.SaveAccountAsync(address, new Model.Account
            {
                Balance = balance,
                Nonce = (await AppChain.GetAccountAsync(address))?.Nonce ?? 0
            });
        }

        public async Task<byte[]> ProduceBlockAsync()
        {
            return await Sequencer.ProduceBlockAsync();
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

            await SetBalanceAsync(BundlerAccount.Address, Nethereum.Web3.Web3.Convert.ToWei(100));

            var bundlerWeb3 = new Web3.Web3(BundlerAccount, _rpcClient);
            bundlerWeb3.TransactionManager.UseLegacyAsDefault = true;

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

        public async Task<BigInteger> GetBlockNumberAsync()
        {
            return await Sequencer.GetBlockNumberAsync();
        }

        public int GetPendingTxCount()
        {
            return Sequencer.TxPool.PendingCount;
        }

        public byte[] EncodeInitData(string ownerAddress)
        {
            return ByteUtil.Merge(
                ECDSAValidatorService.ContractAddress.HexToByteArray(),
                ownerAddress.HexToByteArray());
        }
    }
}
