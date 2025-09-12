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


var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Basic services
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Storage services - Use Nethereum.Wallet core storage
// For demo purposes, we'll use the built-in storage service from Nethereum.Wallet
// This removes dependency on the deleted UI storage components

// Wallet core services - using Bouncy Castle encryption from Nethereum.Wallet
builder.Services.AddSingleton<IEncryptionStrategy, BouncyCastleAes256EncryptionStrategy>(); // Bouncy Castle encryption for WASM
builder.Services.AddSingleton<IWalletVaultService, LocalStorageWalletVaultService>();
builder.Services.AddSingleton<IWalletConfigurationService, InMemoryWalletConfigurationService>(); // Add wallet configuration service

// Register wallet storage service for network services
builder.Services.AddSingleton<Nethereum.Wallet.Storage.IWalletStorageService, Nethereum.Wallet.UI.Components.Blazor.Services.LocalStorageWalletStorageService>();

// Chain features service registration
builder.Services.AddSingleton<Nethereum.RPC.Chain.IChainFeaturesService>(sp => 
    Nethereum.RPC.Chain.ChainFeaturesService.Current);



builder.Services.AddNethereumChainManagement(o =>
{
    // Strategy (default is PreconfiguredEnrich: preconfigured + fill missing RPCs/explorers via ChainList)
    o.Strategy = ChainFeatureStrategyType.PreconfiguredEnrich;

    // (Optional) explicit built-in ID set; omitted means BuiltInDefaultChainIds.All:
    // o.DefaultChainIds = BuiltInDefaultChainIds.All;

    // Provide a post-process hook to inject baseline RPC endpoints if missing.
    o.PostProcessPreconfigured = list =>
    {
        // Map of chainId -> primary RPC(s) (only added if chain currently has none)
        var rpcSeed = new Dictionary<long, string[]>
        {
            { 1,        new[]{ "https://cloudflare-eth.com" } },
            { 10,       new[]{ "https://mainnet.optimism.io" } },
            { 56,       new[]{ "https://bsc-dataseed.binance.org" } },
            { 137,      new[]{ "https://polygon-rpc.com" } },
            { 8453,     new[]{ "https://mainnet.base.org" } },
            { 42161,    new[]{ "https://arb1.arbitrum.io/rpc" } },
            { 324,      new[]{ "https://mainnet.era.zksync.io" } },
            { 59144,    new[]{ "https://linea-mainnet.infura.io/v3/" } }, // placeholder base (user can append key)
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
                // Add only unique
                foreach (var u in urls)
                {
                    if (!chain.HttpRpcs.Contains(u, StringComparer.OrdinalIgnoreCase))
                        chain.HttpRpcs.Add(u);
                }
            }
        }
    };

    // Disable external ChainList if you want strictly static defaults:
    // o.EnableExternalChainList = false;
});

// Register the unified RPC endpoint service
builder.Services.AddSingleton<Nethereum.Wallet.Services.Network.IRpcEndpointService, Nethereum.Wallet.Services.Network.RpcEndpointService>();

// RPC client factory with rotation and failover (depends on IRpcEndpointService)
builder.Services.AddScoped<IRpcClientFactory, RpcClientFactory>();

// dApp integration services - removed as part of simplification

// Configuration service - using core Nethereum.Wallet interfaces only
builder.Services.AddSingleton<ICoreWalletAccountService>(sp => 
{
    var vaultService = sp.GetRequiredService<IWalletVaultService>();
    var encryptionStrategy = sp.GetRequiredService<IEncryptionStrategy>(); // Use registered encryption strategy
    var vault = vaultService.GetCurrentVault() ?? new WalletVault(encryptionStrategy);
    return new CoreWalletAccountService(vault);
});

// Add Nethereum Wallet Hosting services (includes interceptor and RPC handlers)
builder.Services.AddNethereumWalletHosting();
builder.Services.AddNethereumWalletHostProvider();
builder.Services.AddScoped<SelectedEthereumHostProviderService>();

// MudBlazor for UI components - replaced by AddNethereumWalletUI()
// builder.Services.AddMudServices();

// Add global Nethereum wallet UI configuration
builder.Services.AddNethereumWalletUIConfiguration(config => 
{
    config.ApplicationName = "Nethereum";
    config.LogoPath = "/nethereum-logo.png";
    config.WelcomeLogoPath = "/nethereum-logo-large.png";
    config.ShowLogo = true;
    config.ShowApplicationName = true;
    config.ShowNetworkInHeader = true;
    config.ShowAccountDetailsInHeader = true;
    
    // Configure drawer/sidebar behavior
    config.DrawerBehavior = DrawerBehavior.Responsive; // Default: responsive based on component width
    config.ResponsiveBreakpoint = 1000;
    config.SidebarWidth = 200; // Sidebar width in pixels
    
    // Alternative configurations:
    // config.DrawerBehavior = DrawerBehavior.AlwaysShow; // Always show sidebar
    // config.DrawerBehavior = DrawerBehavior.AlwaysHidden; // Always hide sidebar, use overlay
    
    // Configure wallet settings
    config.WalletConfig.Security.MinPasswordLength = 8;
    config.WalletConfig.Behavior.EnableWalletReset = true;
    config.WalletConfig.AllowPasswordVisibilityToggle = true;
});

// Add all Nethereum Wallet UI services (includes MudBlazor, ViewModels, Configuration, Localization)
builder.Services.AddNethereumWalletUI();

// Add theme service for demo
builder.Services.AddSingleton<Nethereum.Wallet.Blazor.Demo.Services.ThemeService>();

// Add demo-specific network icon provider
builder.Services.AddSingleton<INetworkIconProvider, DemoNetworkIconProvider>();

// Wallet configuration objects - commented out until implemented
// builder.Services.AddSingleton<Nethereum.Wallet.UI.Components.Abstractions.WalletVaultConfiguration>();

// Wallet Host UI Service - potentially conflicts with AddNethereumWalletUI()
// AddNethereumWalletUI() registers BlazorWalletHostUIService, but we use SimpleWalletHostUIService
// Override the registration

// Account creation is now handled by the unified registry system
// which is registered in ServiceCollectionExtensions.AddNethereumWalletBlazorComponents()

// Dashboard Plugin System - now uses PluginViewModel pattern
builder.Services.AddScoped<Nethereum.Wallet.UI.Components.Dashboard.Services.IDashboardNavigationService, Nethereum.Wallet.UI.Components.Dashboard.Services.DashboardNavigationService>();

// Notification services - already registered by AddNethereumWalletUI()
// builder.Services.AddScoped<Nethereum.Wallet.UI.Components.Abstractions.IWalletNotificationService, Nethereum.Wallet.UI.Components.Blazor.Services.MudNotificationService>();

// ViewModels - All ViewModels are now registered by AddNethereumWalletUI()
// builder.Services.AddScoped<Nethereum.Wallet.UI.Components.ViewModels.WalletDashboardViewModel>(); // Now registered automatically
// builder.Services.AddScoped<Nethereum.Wallet.UI.Components.ViewModels.AccountManagementViewModel>();
// builder.Services.AddScoped<Nethereum.Wallet.UI.Components.ViewModels.NetworkManagementViewModel>();
// builder.Services.AddScoped<Nethereum.Wallet.UI.Components.ViewModels.NethereumWalletViewModel>(); // Now registered automatically
// builder.Services.AddScoped<Nethereum.Wallet.UI.Components.ViewModels.AccountOverviewViewModel>();

// Dashboard Plugins are now registered automatically in ConfigureDashboardPlugins()

// Authentication - using Ethereum authentication from Nethereum.Blazor
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, Nethereum.Blazor.EthereumAuthenticationStateProvider>();

// Service initializer to wire up events and avoid circular dependencies (remove for now)
// builder.Services.AddScoped<WalletServiceInitializer>();

var app = builder.Build();

// Initialize the account creation registry with ViewModel-to-Component mappings
app.Services.InitializeAccountTypes();

// Initialize the dashboard plugin registry
app.Services.ConfigureDashboardPluginRegistry();

// Register RPC handlers for wallet functionality
var rpcRegistry = app.Services.GetRequiredService<RpcHandlerRegistry>();
WalletRpcHandlerRegistration.RegisterAll(rpcRegistry);

// Start global transaction notifications
// This ensures notifications work app-wide
var notificationService = app.Services.GetRequiredService<Nethereum.Wallet.UI.Components.Transactions.PendingTransactionNotificationService>();

await app.RunAsync();