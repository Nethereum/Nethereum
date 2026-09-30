using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.UI;
using Nethereum.Wallet;
using Nethereum.Wallet.Avalonia.Demo;
using Nethereum.Wallet.Avalonia.Demo.Extensions;
using Nethereum.Wallet.Avalonia.Demo.Services;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.RpcRequests;
using Nethereum.Wallet.Services;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Storage;
using Nethereum.Wallet.UI;
using Nethereum.Wallet.UI.Components.Abstractions;
using Nethereum.Wallet.UI.Components.Avalonia.Extensions;
using Nethereum.Wallet.UI.Components.Avalonia.Services;
using Nethereum.Wallet.UI.Components.Configuration;
using Nethereum.Wallet.UI.Components.Transactions;
using Nethereum.Wallet.UI.Components.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Nethereum.Wallet.Avalonia.Demo;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IEncryptionStrategy, DefaultAes256EncryptionStrategy>();

        services.AddSingleton<IWalletVaultService>(sp =>
        {
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nethereum", "Wallet");
            var filePath = Path.Combine(baseDir, "vault.json");
            return new FileWalletVaultService(filePath, sp.GetRequiredService<IEncryptionStrategy>());
        });

        services.AddSingleton<IWalletConfigurationService, InMemoryWalletConfigurationService>();

        services.AddSingleton<IWalletStorageService>(sp =>
        {
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nethereum", "Wallet");
            return new FileWalletStorageService(baseDir);
        });

        services.AddSingleton<Nethereum.RPC.Chain.IChainFeaturesService>(sp => Nethereum.RPC.Chain.ChainFeaturesService.Current);

        services.AddNethereumChainManagement(o =>
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

        services.AddSingleton<IRpcEndpointService, RpcEndpointService>();
        services.AddScoped<IRpcClientFactory, RpcClientFactory>();

        services.AddSingleton<ICoreWalletAccountService>(sp =>
        {
            var vaultService = sp.GetRequiredService<IWalletVaultService>();
            var encryptionStrategy = sp.GetRequiredService<IEncryptionStrategy>();
            var vault = vaultService.GetCurrentVault() ?? new WalletVault(encryptionStrategy);
            return new CoreWalletAccountService(vault);
        });

        services.AddNethereumWalletHostProvider();
        services.AddScoped<SelectedEthereumHostProviderService>();

        services.AddNethereumWalletUIConfiguration(config =>
        {
            config.ApplicationName = "Nethereum";
            config.LogoPath = "/Assets/nethereum-logo.png";
            config.WelcomeLogoPath = "/Assets/nethereum-logo-large.png";
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

        services.AddNethereumWalletAvaloniaComponents();
        services.AddSingleton<IWalletNotificationService, AvaloniaWalletNotificationService>();
        services.AddTransactionServices();
        services.AddPendingTransactionNotifications();

        services.AddSingleton<INetworkIconProvider, DemoNetworkIconProvider>();

        services.AddTransient<MainWindow>();

        Services = services.BuildServiceProvider();

        Services.InitializeAccountTypes();
        Services.ConfigureDashboardPluginRegistry();

        var rpcRegistry = Services.GetRequiredService<RpcHandlerRegistry>();
        WalletRpcHandlerRegistration.RegisterAll(rpcRegistry);

        _ = Services.GetRequiredService<PendingTransactionNotificationService>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}