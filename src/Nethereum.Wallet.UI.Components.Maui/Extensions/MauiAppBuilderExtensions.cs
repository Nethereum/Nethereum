using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui.Hosting;
using Nethereum.RPC.Chain;
using Nethereum.Wallet;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.RpcRequests;
using Nethereum.Wallet.Services;
using Nethereum.Wallet.Services;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Storage;
using Nethereum.Wallet.UI;
using Nethereum.Wallet.UI.Components.Configuration;
using Nethereum.Wallet.UI.Components.Dashboard.Services;
using Nethereum.Wallet.UI.Components.Maui.Options;
using Nethereum.Wallet.UI.Components.Maui.Services;
using Nethereum.Wallet.UI.Components.Services;

namespace Nethereum.Wallet.UI.Components.Maui.Extensions
{
    public static class MauiAppBuilderExtensions
    {
        private static readonly Dictionary<long, string[]> DefaultRpcSeed = new()
        {
            { 1,        new[]{ "https://rpc.mevblocker.io", "https://eth.llamarpc.com"  } },
            { 10,       new[]{ "https://mainnet.optimism.io" } },
            { 56,       new[]{ "https://bsc-dataseed.binance.org" } },
            { 100,      new[]{ "https://rpc.gnosischain.com" } },
            { 137,      new[]{ "https://polygon-rpc.com" } },
            { 324,      new[]{ "https://mainnet.era.zksync.io" } },
            { 42161,    new[]{ "https://arb1.arbitrum.io/rpc" } },
            { 42220,    new[]{ "https://forno.celo.org" } },
            { 43114,    new[]{ "https://api.avax.network/ext/bc/C/rpc" } },
            { 59144,    new[]{ "https://linea-mainnet.infura.io/v3/" } },
            { 8453,     new[]{ "https://mainnet.base.org" } },
            { 84532,    new[]{ "https://sepolia.base.org" } },
            { 11155111, new[]{ "https://rpc.sepolia.org" } },
            { 11155420, new[]{ "https://optimism-sepolia.blockpi.network/v1/rpc/public" } },
            { 1337,     new[]{ "http://localhost:8545" } },
            { 421614,   new[]{ "https://sepolia-rollup.arbitrum.io/rpc" } }
        };


       
        public static MauiAppBuilder AddNethereumWalletMauiComponents(this MauiAppBuilder builder, Action<MauiWalletComponentOptions>? configure = null)
        {
            var options = new MauiWalletComponentOptions();
            configure?.Invoke(options);

            builder.Services.TryAddSingleton(options);

            builder.Services.TryAddSingleton<IEncryptionStrategy, BouncyCastleAes256EncryptionStrategy>();
            builder.Services.TryAddSingleton<IWalletVaultService, MauiSecureStorageWalletVaultService>();
            builder.Services.TryAddSingleton<IWalletStorageService, MauiPreferencesWalletStorageService>();
            builder.Services.TryAddSingleton<IWalletConfigurationService, InMemoryWalletConfigurationService>();
            builder.Services.TryAddSingleton<IDappPermissionService, DefaultDappPermissionService>();

            builder.Services.AddSingleton<IChainFeaturesService>(sp => ChainFeaturesService.Current);

            builder.Services.AddNethereumChainManagement(chainOptions =>
            {
                chainOptions.EnableExternalChainList = true;
                chainOptions.Strategy = ChainFeatureStrategyType.PreconfiguredEnrich;
                chainOptions.PostProcessPreconfigured = list =>
                {
                    if (!list.Any(c => (long)c.ChainId == 1337))
                    {
                        list.Add(new ChainFeature
                        {
                            ChainId = new System.Numerics.BigInteger(1337),
                            ChainName = "Localhost (1337)",
                            NativeCurrency = new NativeCurrency
                            {
                                Name = "Ether",
                                Symbol = "ETH",
                                Decimals = 18
                            },
                            HttpRpcs = new List<string> { "http://localhost:8545" },
                            WsRpcs = new List<string>(),
                            Explorers = new List<string>(),
                            SupportEIP155 = true,
                            SupportEIP1559 = true
                        });
                    }

                    foreach (var chain in list)
                    {
                        var id = (long)chain.ChainId;
                        if (!DefaultRpcSeed.TryGetValue(id, out var urls))
                        {
                            continue;
                        }

                        chain.HttpRpcs ??= new List<string>();

                        foreach (var url in urls)
                        {
                            if (!chain.HttpRpcs.Contains(url, StringComparer.OrdinalIgnoreCase))
                            {
                                chain.HttpRpcs.Add(url);
                            }
                        }
                    }
                };
            },
            chainServiceLifetime: ServiceLifetime.Singleton,
            strategyLifetime: ServiceLifetime.Singleton);

            builder.Services.TryAddSingleton<IRpcEndpointService, RpcEndpointService>();
            builder.Services.TryAddScoped<IRpcClientFactory, RpcClientFactory>();

            builder.Services.AddWalletPromptServices(ServiceLifetime.Singleton);

            builder.Services.AddSingleton<ICoreWalletAccountService>(sp =>
            {
                var vaultService = sp.GetRequiredService<IWalletVaultService>();
                var encryption = sp.GetRequiredService<IEncryptionStrategy>();
                var vault = vaultService.GetCurrentVault() ?? new WalletVault(encryption);
                return new CoreWalletAccountService(vault);
            });

            builder.Services.AddNethereumWalletServicesSingleton(options.DefaultChainId);
            builder.Services.AddNethereumWalletUIConfiguration(config =>
            {
                config.ApplicationName = "Nethereum Wallet";
                config.ShowApplicationName = true;
                config.ShowLogo = true;
                config.LogoPath = "/nethereum-logo.png";
                config.WelcomeLogoPath = "/nethereum-logo-large.png";
                config.DrawerBehavior = DrawerBehavior.Responsive;
                config.ResponsiveBreakpoint = 1024;
                config.SidebarWidth = 200;
                config.WalletConfig.Security.MinPasswordLength = 8;
                config.WalletConfig.Behavior.EnableWalletReset = true;
                config.WalletConfig.AllowPasswordVisibilityToggle = true;

                options.ConfigureUi?.Invoke(config);
            });

            builder.Services.TryAddScoped<IDashboardNavigationService, DashboardNavigationService>();

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddSingleton<IMauiInitializeService, WalletRegistryInitializer>();

            return builder;
        }

        internal static IReadOnlyDictionary<long, string[]> GetDefaultRpcSeed() => DefaultRpcSeed;
    }

    internal class WalletRegistryInitializer : IMauiInitializeService
    {
        public void Initialize(IServiceProvider services)
        {
            var registry = services.GetService<RpcHandlerRegistry>();
            if (registry != null)
            {
                WalletRpcHandlerRegistration.RegisterAll(registry);
            }

            var options = services.GetService<MauiWalletComponentOptions>();
            var chainManagement = services.GetService<IChainManagementService>();
            var configuration = services.GetService<IWalletConfigurationService>();
            var storage = services.GetService<IWalletStorageService>();
            var rpcEndpointService = services.GetService<IRpcEndpointService>();

            if (chainManagement == null || configuration == null)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                var defaultChainIds = new long[] { 1, 10, 100, 137, 56, 42161, 42220, 43114, 59144, 8453, 1337 };

                foreach (var chainId in defaultChainIds)
                {
                    try
                    {
                        var feature = await chainManagement.GetChainAsync(new System.Numerics.BigInteger(chainId));
                        if (feature != null)
                        {
                            await configuration.AddOrUpdateChainAsync(feature);
                        }
                    }
                    catch
                    {
                        // Ignore failures when preloading optional networks.
                    }
                }

                List<ChainFeature> networks = new();

                if (storage != null)
                {
                    try
                    {
                        networks = await storage.GetUserNetworksAsync();
                        var updated = false;

                        if (networks.Count == 0)
                        {
                            foreach (var chainId in defaultChainIds)
                            {
                                try
                                {
                                    var seedFeature = await chainManagement.GetChainAsync(new System.Numerics.BigInteger(chainId));
                                    if (seedFeature == null)
                                    {
                                        continue;
                                    }

                                    if (seedFeature.HttpRpcs == null || seedFeature.HttpRpcs.Count == 0)
                                    {
                                        if (MauiAppBuilderExtensions.GetDefaultRpcSeed().TryGetValue(chainId, out var seedUrls))
                                        {
                                            seedFeature.HttpRpcs = seedUrls.ToList();
                                        }
                                    }

                                    await storage.SaveUserNetworkAsync(seedFeature);
                                    updated = true;
                                }
                                catch
                                {
                                    // Ignore seeding failures for optional networks.
                                }
                            }

                            networks = await storage.GetUserNetworksAsync();
                        }
                        foreach (var network in networks)
                        {
                            var chainId = (long)network.ChainId;
                            if ((network.HttpRpcs == null || network.HttpRpcs.Count == 0) &&
                                MauiAppBuilderExtensions.GetDefaultRpcSeed().TryGetValue(chainId, out var urls))
                            {
                                network.HttpRpcs = urls.ToList();
                                await storage.SaveUserNetworkAsync(network);
                                updated = true;
                            }
                        }

                        if (updated)
                        {
                            await chainManagement.RefreshChainDataAsync();
                        }
                    }
                    catch
                    {
                        // Ignore preference upgrade failures; wallet UI can still prompt user to configure RPCs.
                    }
                }

                if (rpcEndpointService != null && networks.Count > 0)
                {
                    foreach (var network in networks)
                    {
                        try
                        {
                            var chainId = network.ChainId;
                            var config = await rpcEndpointService.GetConfigurationAsync(chainId);
                            if (config == null || config.SelectedRpcUrls == null || config.SelectedRpcUrls.Count == 0)
                            {
                                var candidateUrls = (network.HttpRpcs ?? new List<string>()).Where(u => !string.IsNullOrWhiteSpace(u)).ToList();
                                if (candidateUrls.Count == 0 && MauiAppBuilderExtensions.GetDefaultRpcSeed().TryGetValue((long)chainId, out var seedUrls))
                                {
                                    candidateUrls = seedUrls.ToList();
                                }

                                if (candidateUrls.Count > 0)
                                {
                                    var rpcConfig = new RpcSelectionConfiguration
                                    {
                                        ChainId = chainId,
                                        Mode = RpcSelectionMode.Single,
                                        SelectedRpcUrls = new List<string> { candidateUrls[0] },
                                        LastModified = DateTime.UtcNow
                                    };

                                    await rpcEndpointService.SaveConfigurationAsync(rpcConfig);
                                }
                            }
                        }
                        catch
                        {
                            // Ignore configuration seeding failures per network.
                        }
                    }
                }

                var activeChainId = options?.DefaultChainId ?? 1;
                try
                {
                    await configuration.SetActiveChainAsync(new System.Numerics.BigInteger(activeChainId));
                }
                catch
                {
                    // Ignore failures when selecting default chain.
                }
            });
        }
    }
}
