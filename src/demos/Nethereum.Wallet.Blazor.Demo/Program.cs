using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Wallet.Blazor.Demo;
using Microsoft.AspNetCore.Components.Authorization;
using Nethereum.Wallet;
using Nethereum.Wallet.UI;
using Nethereum.UI;
using Nethereum.Wallet.Services;
using MudBlazor.Services;
using Nethereum.Wallet.UI.Components.Blazor.Extensions;
using Nethereum.Wallet.UI.Components.Services;
using Nethereum.Wallet.Services.Network;
using Nethereum.DataServices.Chainlist;
using Nethereum.Wallet.UI.Components.Blazor.Services;
using Nethereum.Wallet.Blazor.Demo.Services;
using Microsoft.JSInterop;
using Nethereum.Wallet.UI.Components.Configuration;
using Nethereum.Wallet.UI.Components.Utils;
using Nethereum.Wallet.RpcRequests;
using Nethereum.Wallet.Hosting; 


var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");


builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Wallet core services
builder.Services.AddSingleton<IEncryptionStrategy, BouncyCastleAes256EncryptionStrategy>(); //wasm support
builder.Services.AddSingleton<IWalletVaultService, LocalStorageWalletVaultService>();
builder.Services.AddSingleton<IWalletConfigurationService, InMemoryWalletConfigurationService>();


builder.Services.AddSingleton<Nethereum.Wallet.Storage.IWalletStorageService, LocalStorageWalletStorageService>();


builder.Services.AddSingleton<Nethereum.RPC.Chain.IChainFeaturesService>(sp =>
    Nethereum.RPC.Chain.ChainFeaturesService.Current);

//This register chain management with ChainList as external source and a set of default chains
builder.Services.AddNethereumChainManagement(o =>
{
    o.Strategy = ChainFeatureStrategyType.PreconfiguredEnrich;

    o.PostProcessPreconfigured = list =>
    {
        var rpcSeed = new Dictionary<long, string[]>
        {
            { 1,        new[]{ "https://cloudflare-eth.com" } },
            { 10,       new[]{ "https://mainnet.optimism.io" } },
            { 56,       new[]{ "https://bsc-dataseed.binance.org" } },
            { 137,      new[]{ "https://polygon-rpc.com" } },
            { 8453,     new[]{ "https://mainnet.base.org" } },
            { 42161,    new[]{ "https://arb1.arbitrum.io/rpc" } },
            { 324,      new[]{ "https://mainnet.era.zksync.io" } },
            { 59144,    new[]{ "https://linea-mainnet.infura.io/v3/" } },
            { 43114,    new[]{ "https://api.avax.network/ext/bc/C/rpc" } },
            { 100,      new[]{ "https://rpc.gnosischain.com" } },
            { 42220,    new[]{ "https://forno.celo.org" } },
            { 11155111, new[]{ "https://rpc.sepolia.org" } },
            { 11155420, new[]{ "https://optimism-sepolia.blockpi.network/v1/rpc/public" } },
            { 84532,    new[]{ "https://sepolia.base.org" } },
            { 421614,   new[]{ "https://sepolia-rollup.arbitrum.io/rpc" } },
        };

        foreach (var chain in list)
        {
            var id = (long)chain.ChainId;
            if ((chain.HttpRpcs == null || chain.HttpRpcs.Count == 0) && rpcSeed.TryGetValue(id, out var urls))
            {
                // Ensure list initialised (fixes nullable warning)
                chain.HttpRpcs ??= new List<string>();

                foreach (var u in urls)
                {
                    if (!chain.HttpRpcs.Contains(u, StringComparer.OrdinalIgnoreCase))
                        chain.HttpRpcs.Add(u);
                }
            }
        }
    };
});

// Unified RPC endpoint service
builder.Services.AddSingleton<IRpcEndpointService, RpcEndpointService>();

// RPC client factory
builder.Services.AddScoped<IRpcClientFactory, RpcClientFactory>();

// Core wallet account service
builder.Services.AddSingleton<ICoreWalletAccountService>(sp =>
{
    var vaultService = sp.GetRequiredService<IWalletVaultService>();
    var encryptionStrategy = sp.GetRequiredService<IEncryptionStrategy>();
    var vault = vaultService.GetCurrentVault() ?? new WalletVault(encryptionStrategy);
    return new CoreWalletAccountService(vault);
});

// Wallet host provider (replaces removed AddNethereumWalletHosting)
builder.Services.AddNethereumWalletHostProvider(); // Removed obsolete AddNethereumWalletHosting()
builder.Services.AddScoped<SelectedEthereumHostProviderService>();

// UI configuration
builder.Services.AddNethereumWalletUIConfiguration(config =>
{
    config.ApplicationName = "Nethereum";
    config.LogoPath = "/nethereum-logo.png";
    config.WelcomeLogoPath = "/nethereum-logo-large.png";
    config.ShowLogo = true;
    config.ShowApplicationName = true;
    config.ShowNetworkInHeader = true;
    config.ShowAccountDetailsInHeader = true;
    config.DrawerBehavior = DrawerBehavior.Responsive;
    config.ResponsiveBreakpoint = 1000;
    config.SidebarWidth = 200;
    config.WalletConfig.Security.MinPasswordLength = 8;
    config.WalletConfig.Behavior.EnableWalletReset = true;
    config.WalletConfig.AllowPasswordVisibilityToggle = true;
});

// Full Wallet UI (MudBlazor + ViewModels + localization etc.)
builder.Services.AddNethereumWalletUI();

// Theme + network icon provider overrides
builder.Services.AddSingleton<ThemeService>();
builder.Services.AddSingleton<INetworkIconProvider, DemoNetworkIconProvider>();

// Dashboard navigation
builder.Services.AddScoped<Nethereum.Wallet.UI.Components.Dashboard.Services.IDashboardNavigationService,
    Nethereum.Wallet.UI.Components.Dashboard.Services.DashboardNavigationService>();

// Authorization
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, Nethereum.Blazor.EthereumAuthenticationStateProvider>();

var app = builder.Build();

// Initialize registries
app.Services.InitializeAccountTypes();
app.Services.ConfigureDashboardPluginRegistry();

// Register RPC handlers
var rpcRegistry = app.Services.GetRequiredService<RpcHandlerRegistry>();
WalletRpcHandlerRegistration.RegisterAll(rpcRegistry);

// Start pending transaction notifications
var notificationService =
    app.Services.GetRequiredService<Nethereum.Wallet.UI.Components.Transactions.PendingTransactionNotificationService>();

await app.RunAsync();