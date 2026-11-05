using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.Chain;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.RpcRequests;
using Nethereum.Wallet.Services;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Storage;
using Nethereum.Wallet.UI.Components.Services;

namespace Nethereum.Wallet.UI.Components.UnitTests.SecurityAudit71.S13
{
    public sealed class S13Wallet : IDisposable
    {
        public const string EvilOrigin = "https://evil.example";
        public const string GoodOrigin = "https://good.example";

        private S13Wallet(WalletDevChainHarness harness, NethereumWalletHostProvider host, RpcHandlerRegistry registry)
        {
            Harness = harness;
            Host = host;
            Registry = registry;
            Queue = harness.Services.GetRequiredService<IPromptQueueService>();
        }

        public WalletDevChainHarness Harness { get; }
        public NethereumWalletHostProvider Host { get; }
        public RpcHandlerRegistry Registry { get; }
        public IPromptQueueService Queue { get; }

        public static async Task<S13Wallet> CreateAsync()
        {
            var harness = await WalletDevChainHarness.CreateAsync();
            var sp = harness.Services;
            var storage = sp.GetRequiredService<IWalletStorageService>();
            var registry = new RpcHandlerRegistry();
            WalletRpcHandlerRegistration.RegisterAll(registry);
            var host = new NethereumWalletHostProvider(
                sp.GetRequiredService<IWalletVaultService>(),
                sp.GetRequiredService<Nethereum.Wallet.UI.IRpcClientFactory>(),
                storage,
                sp.GetRequiredService<IChainManagementService>(),
                registry,
                sp.GetRequiredService<Nethereum.Wallet.UI.ITransactionPromptService>(),
                sp.GetRequiredService<Nethereum.Wallet.UI.ISignaturePromptService>(),
                sp.GetRequiredService<IWalletConfigurationService>(),
                sp.GetRequiredService<Nethereum.Wallet.UI.ILoginPromptService>(),
                new DefaultDappPermissionService(storage),
                sp.GetRequiredService<Nethereum.Wallet.UI.IDappPermissionPromptService>(),
                sp.GetRequiredService<Nethereum.Wallet.UI.IChainAdditionPromptService>(),
                sp.GetRequiredService<Nethereum.Wallet.UI.IChainSwitchPromptService>(),
                WalletDevChainHarness.ChainId);
            await host.RefreshAccountsAsync();
            await host.SetSelectedNetworkAsync(WalletDevChainHarness.ChainId);
            return new S13Wallet(harness, host, registry);
        }

        public async Task GrantAsync(string origin)
        {
            await Host.DappPermissions.ApproveAsync(origin.ToLowerInvariant(), Harness.FirstAddress.ToLowerInvariant());
        }

        public void SetOrigin(string? origin)
        {
            Host.SelectedDapp = origin == null ? null : new Nethereum.Wallet.UI.DappConnectionContext { Origin = origin, Title = origin };
        }

        public Task<RpcResponseMessage> CallAsync(string method, params object[] parameters)
            => Registry.GetHandler(method)!.HandleAsync(new RpcRequestMessage(1, method, parameters), Host);

        public async Task<(int Queued, IReadOnlyList<PromptRequest> Prompts, RpcResponseMessage Response)> ObserveAsync(string method, params object[] parameters)
        {
            var pending = CallAsync(method, parameters);
            await Task.WhenAny(pending, Task.Delay(1000));
            var prompts = Queue.PendingPrompts;
            var response = await DrainAsync(pending);
            return (prompts.Count, prompts, response);
        }

        public async Task<RpcResponseMessage> DrainAsync(Task<RpcResponseMessage> pending)
        {
            if (!pending.IsCompleted)
            {
                await Queue.RejectAllAsync();
            }
            return await pending;
        }

        public int PendingCount => Queue.PendingPrompts.Count;

        public void Dispose() => Harness.Dispose();
    }

    public sealed class SwitchingContext : Nethereum.Wallet.UI.IWalletContext
    {
        private readonly NethereumWalletHostProvider _inner;
        private readonly Action<NethereumWalletHostProvider> _duringEnable;

        public SwitchingContext(NethereumWalletHostProvider inner, Action<NethereumWalletHostProvider> duringEnable)
        {
            _inner = inner;
            _duringEnable = duringEnable;
        }

        public string Name => _inner.Name;
        public bool Available => _inner.Available;
        public string SelectedAccount => _inner.SelectedAccount;
        public long SelectedNetworkChainId => _inner.SelectedNetworkChainId;
        public bool Enabled => _inner.Enabled;
        public bool MultipleWalletsProvider => _inner.MultipleWalletsProvider;
        public bool MultipleWalletSelected => _inner.MultipleWalletSelected;
        public event Func<string, Task> SelectedAccountChanged { add { _inner.SelectedAccountChanged += value; } remove { _inner.SelectedAccountChanged -= value; } }
        public event Func<long, Task> NetworkChanged { add { _inner.NetworkChanged += value; } remove { _inner.NetworkChanged -= value; } }
        public event Func<bool, Task> AvailabilityChanged { add { _inner.AvailabilityChanged += value; } remove { _inner.AvailabilityChanged -= value; } }
        public event Func<bool, Task> EnabledChanged { add { _inner.EnabledChanged += value; } remove { _inner.EnabledChanged -= value; } }
        public Task<bool> CheckProviderAvailabilityAsync() => _inner.CheckProviderAvailabilityAsync();
        public Task<Nethereum.Web3.IWeb3> GetWeb3Async() => _inner.GetWeb3Async();
        public async Task<string> EnableProviderAsync()
        {
            var account = await _inner.EnableProviderAsync();
            await Task.Yield();
            _duringEnable(_inner);
            return account;
        }
        public Task<string> GetProviderSelectedAccountAsync() => _inner.GetProviderSelectedAccountAsync();
        public Task<string> SignMessageAsync(string message) => _inner.SignMessageAsync(message);
        public IReadOnlyList<IWalletAccount> Accounts => _inner.Accounts;
        public IWalletAccount? SelectedWalletAccount => _inner.SelectedWalletAccount;
        public Nethereum.Wallet.UI.DappConnectionContext? SelectedDapp { get => _inner.SelectedDapp; set => _inner.SelectedDapp = value; }
        public IDappPermissionService DappPermissions => _inner.DappPermissions;
        public Task<IAccount?> GetSelectedAccountAsync() => _inner.GetSelectedAccountAsync();
        public Nethereum.Hex.HexTypes.HexBigInteger? ChainId => _inner.ChainId;
        public Task<IClient?> GetRpcClientAsync() => _inner.GetRpcClientAsync();
        public Task<Nethereum.Web3.IWeb3> GetWalletWeb3Async() => _inner.GetWalletWeb3Async();
        public Task<bool> SwitchChainAsync(string chainIdHex) => _inner.SwitchChainAsync(chainIdHex);
        public Task<string?> ShowTransactionDialogAsync(TransactionInput input) => _inner.ShowTransactionDialogAsync(input);
        public Task<string?> RequestPersonalSignAsync(Nethereum.Wallet.UI.SignaturePromptContext context) => _inner.RequestPersonalSignAsync(context);
        public Task<string?> RequestTypedDataSignAsync(Nethereum.Wallet.UI.TypedDataSignPromptContext context) => _inner.RequestTypedDataSignAsync(context);
        public Task<bool> RequestDappPermissionAsync(Nethereum.Wallet.UI.DappConnectionContext dappContext, string accountAddress) => _inner.RequestDappPermissionAsync(dappContext, accountAddress);
        public Task<Nethereum.Wallet.UI.ChainAdditionPromptResult> RequestChainAdditionAsync(Nethereum.Wallet.UI.ChainAdditionPromptRequest request) => _inner.RequestChainAdditionAsync(request);
        public Task<Nethereum.Wallet.UI.ChainSwitchPromptResult> RequestChainSwitchAsync(Nethereum.Wallet.UI.ChainSwitchPromptRequest request) => _inner.RequestChainSwitchAsync(request);
        public Task AddChainAsync(ChainFeature chainMetadata) => _inner.AddChainAsync(chainMetadata);
        public void Initialise(IReadOnlyList<IWalletAccount> accounts, IWalletAccount? selected) => _inner.Initialise(accounts, selected);
        public Task SetSelectedWalletAccountAsync(IWalletAccount? account) => _inner.SetSelectedWalletAccountAsync(account);
        public void SetSelectedAccount(string account) => _inner.SetSelectedAccount(account);
        public Task InitialiseAccountSignerAsync() => _inner.InitialiseAccountSignerAsync();
        public IWalletConfigurationService Configuration => _inner.Configuration;
    }
}
