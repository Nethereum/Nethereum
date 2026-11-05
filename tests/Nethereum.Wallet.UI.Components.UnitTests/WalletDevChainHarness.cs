using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.DevChain;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Chain;
using Nethereum.Signer;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Storage;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Prompts;
using Nethereum.Wallet.UI.Components.SendTransaction;
using Nethereum.Wallet.UI.Components.Services;
using Nethereum.Wallet.UI.Components.Tokens;
using Nethereum.Wallet.UI.Components.Transactions;
using Nethereum.Wallet.WalletAccounts;

namespace Nethereum.Wallet.UI.Components.UnitTests
{
    public sealed class WalletDevChainHarness : IDisposable
    {
        public const long ChainId = 31337;
        public static readonly BigInteger FirstBalance = Nethereum.Web3.Web3.Convert.ToWei(10);
        public static readonly BigInteger SecondBalance = Nethereum.Web3.Web3.Convert.ToWei(3);

        private readonly string _storageDirectory;
        private readonly ServiceProvider _provider;

        private WalletDevChainHarness(DevChainNode node, string storageDirectory, ServiceProvider provider, string firstAddress, string secondAddress)
        {
            Node = node;
            _storageDirectory = storageDirectory;
            _provider = provider;
            FirstAddress = firstAddress;
            SecondAddress = secondAddress;
        }

        public DevChainNode Node { get; }
        public IServiceProvider Services => _provider;
        public string FirstAddress { get; }
        public string SecondAddress { get; }

        public static async Task<WalletDevChainHarness> CreateAsync()
        {
            var firstKey = EthECKey.GenerateKey();
            var secondKey = EthECKey.GenerateKey();
            var firstAddress = firstKey.GetPublicAddress();
            var secondAddress = secondKey.GetPublicAddress();

            var node = DevChainNode.CreateInMemory(new DevChainConfig { ChainId = ChainId, AutoMine = true });
            await node.StartAsync(new Dictionary<string, BigInteger>
            {
                [firstAddress] = FirstBalance,
                [secondAddress] = SecondBalance
            });

            var storageDirectory = Path.Combine(Path.GetTempPath(), "wallet-ui-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(storageDirectory);

            var services = new ServiceCollection();
            services.AddSingleton<IWalletVaultService, InMemoryWalletVaultService>();
            services.AddSingleton<IWalletConfigurationService, InMemoryWalletConfigurationService>();
            services.AddSingleton<IWalletStorageService>(new FileWalletStorageService(storageDirectory));
            services.AddSingleton<IChainFeaturesService>(ChainFeaturesService.Current);
            services.AddNethereumChainManagement(o => o.Strategy = ChainFeatureStrategyType.PreconfiguredOnly);
            services.AddSingleton<Nethereum.Wallet.UI.IRpcClientFactory>(new DevChainRpcClientFactory(new DevChainRpcClient(node.CreateDispatcher())));
            services.AddSingleton<ILocalizationStorageProvider, SystemLocalizationStorageProvider>();
            services.AddSingleton<IWalletLocalizationService>(sp => new WalletLocalizationService(sp.GetService<ILocalizationStorageProvider>()));
            services.AddSingleton<Nethereum.Wallet.UI.ILoginSessionState, Nethereum.Wallet.UI.LoginSessionState>();
            services.AddSingleton<Nethereum.Wallet.UI.ILoginPromptService, Nethereum.Wallet.UI.LoginPromptService>();
            services.AddNethereumWalletServicesSingleton(ChainId);
            services.AddTokenServices(o => o.BaseDirectory = Path.Combine(storageDirectory, "tokens"));
            services.AddTokenTransferServices();
            services.AddTransactionServices();
            services.AddTransient<DAppSignaturePromptViewModel>();

            var provider = services.BuildServiceProvider();

            var chains = provider.GetRequiredService<IChainManagementService>();
            await chains.AddCustomChainAsync(new ChainFeature
            {
                ChainId = ChainId,
                ChainName = "Wallet DevChain",
                NativeCurrency = new NativeCurrency { Name = "Ether", Symbol = "ETH", Decimals = 18 },
                SupportEIP155 = true,
                SupportEIP1559 = true,
                HttpRpcs = new List<string> { "http://localhost:8545" }
            });

            var vault = provider.GetRequiredService<IWalletVaultService>();
            await vault.CreateNewVaultWithAccountAsync("password", new PrivateKeyWalletAccount(firstAddress, "first", firstKey.GetPrivateKey()));
            vault.GetCurrentVault()!.Accounts.Add(new PrivateKeyWalletAccount(secondAddress, "second", secondKey.GetPrivateKey()));

            var host = provider.GetRequiredService<NethereumWalletHostProvider>();
            await host.RefreshAccountsAsync();
            await host.SetSelectedNetworkAsync(ChainId);

            return new WalletDevChainHarness(node, storageDirectory, provider, firstAddress, secondAddress);
        }

        public void Dispose()
        {
            _provider.Dispose();
            Node.Dispose();
            try { Directory.Delete(_storageDirectory, true); } catch { }
        }

        private sealed class DevChainRpcClientFactory : Nethereum.Wallet.UI.IRpcClientFactory
        {
            private readonly IClient _client;

            public DevChainRpcClientFactory(IClient client)
            {
                _client = client;
            }

            public Task<IClient> CreateClientAsync(ChainFeature? activeChain = null) => Task.FromResult(_client);
        }
    }
}
