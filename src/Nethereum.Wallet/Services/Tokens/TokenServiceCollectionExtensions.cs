using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nethereum.TokenServices;
using Nethereum.TokenServices.Caching;
using Nethereum.TokenServices.ERC20;
using Nethereum.TokenServices.ERC20.Discovery;
using Nethereum.TokenServices.ERC20.Pricing;
using Nethereum.TokenServices.Refresh;
using Nethereum.Wallet.Storage;

namespace Nethereum.Wallet.Services.Tokens
{
    public class WalletTokenServicesOptions
    {
        public string CacheDirectory { get; set; }
        public string DefaultCurrency { get; set; } = "usd";
        public bool UseFileCache { get; set; } = true;
        public bool ConfigureFromServiceProvider { get; set; } = false;
    }

    public static class TokenServiceCollectionExtensions
    {
        public static IServiceCollection AddWalletTokenServices(
            this IServiceCollection services,
            Action<WalletTokenServicesOptions> configureOptions = null)
        {
            var walletOptions = new WalletTokenServicesOptions();
            configureOptions?.Invoke(walletOptions);

            services.TryAddSingleton(walletOptions);

            services.TryAddSingleton<ITokenListDiffStorage, Storage.WalletTokenListDiffStorage>();
            services.TryAddSingleton<ICoinMappingDiffStorage, Storage.WalletCoinMappingDiffStorage>();

            if (walletOptions.ConfigureFromServiceProvider)
            {
                services.TryAddSingleton<ICacheProvider>(sp =>
                {
                    var storageOptions = sp.GetService<TokenStorageOptions>();
                    var cacheDir = storageOptions?.BaseDirectory
                                   ?? walletOptions.CacheDirectory
                                   ?? TokenStorageOptions.Default.BaseDirectory;

                    return new FileCacheProvider(cacheDir);
                });
            }

            services.TryAddSingleton<ITokenListProvider>(sp =>
            {
                var cache = sp.GetRequiredService<ICacheProvider>();
                var coinGecko = sp.GetRequiredService<CoinGeckoTokenListProvider>();
                var embedded = sp.GetRequiredService<EmbeddedTokenListProvider>();
                var diffStorage = sp.GetRequiredService<ITokenListDiffStorage>();
                var opts = sp.GetRequiredService<TokenServicesOptions>();
                return new ResilientTokenListProvider(
                    coinGecko,
                    embedded,
                    diffStorage,
                    cache,
                    opts.TokenListCacheExpiry);
            });

            services.TryAddSingleton<ITokenPriceProvider>(sp =>
            {
                var cache = sp.GetRequiredService<ICacheProvider>();
                var embedded = sp.GetRequiredService<EmbeddedCoinMappingProvider>();
                var mappingDiffStorage = sp.GetRequiredService<ICoinMappingDiffStorage>();
                var opts = sp.GetRequiredService<TokenServicesOptions>();
                return new CoinGeckoPriceProvider(
                    coinGeckoService: null,
                    cacheProvider: cache,
                    priceCacheExpiry: opts.PriceCacheExpiry,
                    platformsCacheExpiry: null,
                    embeddedProvider: embedded,
                    mappingDiffStorage: mappingDiffStorage);
            });

            services.AddErc20TokenServices(options =>
            {
                options.CacheDirectory = walletOptions.CacheDirectory;
                options.DefaultCurrency = walletOptions.DefaultCurrency;
                options.UseFileCache = !walletOptions.ConfigureFromServiceProvider &&
                                       walletOptions.UseFileCache &&
                                       !string.IsNullOrEmpty(walletOptions.CacheDirectory);
            });

            services.TryAddSingleton<ITokenManagementService>(sp =>
            {
                var tokenService = sp.GetRequiredService<IErc20TokenService>();
                var tokenStorage = sp.GetRequiredService<Storage.ITokenStorageService>();
                var rpcClientFactory = sp.GetRequiredService<UI.IRpcClientFactory>();
                var chainService = sp.GetRequiredService<Network.IChainManagementService>();
                var refreshCoordinator = sp.GetService<IResourceRefreshCoordinator>();

                return new TokenManagementService(
                    tokenStorage,
                    tokenService,
                    rpcClientFactory,
                    chainService,
                    refreshCoordinator);
            });

            return services;
        }
    }
}
