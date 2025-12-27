using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nethereum.DataServices.CoinGecko;
using Nethereum.TokenServices.Caching;
using Nethereum.TokenServices.ERC20;
using Nethereum.TokenServices.ERC20.Balances;
using Nethereum.TokenServices.ERC20.Discovery;
using Nethereum.TokenServices.ERC20.Events;
using Nethereum.TokenServices.ERC20.Pricing;
using Nethereum.TokenServices.ERC20.Refresh;
using Nethereum.TokenServices.MultiAccount;
using Nethereum.TokenServices.Refresh;

namespace Nethereum.TokenServices
{
    public class TokenServicesOptions
    {
        public string CacheDirectory { get; set; }
        public string TokenListDiffStorageDirectory { get; set; }
        public string CoinMappingDiffStorageDirectory { get; set; }
        public string DefaultCurrency { get; set; } = "usd";
        public TimeSpan TokenListCacheExpiry { get; set; } = TimeSpan.FromDays(7);
        public TimeSpan PriceCacheExpiry { get; set; } = TimeSpan.FromMinutes(5);
        public TimeSpan CoinsListCacheExpiry { get; set; } = TimeSpan.FromDays(7);
        public TimeSpan PlatformsCacheExpiry { get; set; } = TimeSpan.FromDays(30);
        public TimeSpan MinTimeBetweenRefreshes { get; set; } = TimeSpan.FromSeconds(30);
        public int MaxRefreshRetries { get; set; } = 3;
        public int MultiCallBatchSize { get; set; } = 100;
        public bool UseFileCache { get; set; } = false;
        public bool UseFileDiffStorage { get; set; } = false;
    }

    public class TokenServicesBuilder
    {
        private readonly IServiceCollection _services;
        private readonly TokenServicesOptions _options;

        public TokenServicesBuilder(IServiceCollection services, TokenServicesOptions options)
        {
            _services = services;
            _options = options;
        }

        public TokenServicesBuilder UseTokenListProvider<T>() where T : class, ITokenListProvider
        {
            _services.Replace(ServiceDescriptor.Singleton<ITokenListProvider, T>());
            return this;
        }

        public TokenServicesBuilder UseBalanceProvider<T>() where T : class, ITokenBalanceProvider
        {
            _services.Replace(ServiceDescriptor.Singleton<ITokenBalanceProvider, T>());
            return this;
        }

        public TokenServicesBuilder UsePriceProvider<T>() where T : class, ITokenPriceProvider
        {
            _services.Replace(ServiceDescriptor.Singleton<ITokenPriceProvider, T>());
            return this;
        }

        public TokenServicesBuilder UseCacheProvider<T>() where T : class, ICacheProvider
        {
            _services.Replace(ServiceDescriptor.Singleton<ICacheProvider, T>());
            return this;
        }

        public TokenServicesBuilder UseEventScanner<T>() where T : class, ITokenEventScanner
        {
            _services.Replace(ServiceDescriptor.Singleton<ITokenEventScanner, T>());
            return this;
        }

        public TokenServicesBuilder UseRefreshCoordinator<T>() where T : class, IResourceRefreshCoordinator
        {
            _services.Replace(ServiceDescriptor.Singleton<IResourceRefreshCoordinator, T>());
            return this;
        }

        public TokenServicesBuilder UseDiscoveryStrategy<T>() where T : class, IDiscoveryStrategy
        {
            _services.Replace(ServiceDescriptor.Singleton<IDiscoveryStrategy, T>());
            return this;
        }

        public TokenServicesBuilder UseMultiAccountService<T>() where T : class, IMultiAccountTokenService
        {
            _services.Replace(ServiceDescriptor.Singleton<IMultiAccountTokenService, T>());
            return this;
        }

        public TokenServicesBuilder UseTokenListDiffStorage<T>() where T : class, ITokenListDiffStorage
        {
            _services.Replace(ServiceDescriptor.Singleton<ITokenListDiffStorage, T>());
            return this;
        }

        public TokenServicesBuilder UseCoinMappingDiffStorage<T>() where T : class, ICoinMappingDiffStorage
        {
            _services.Replace(ServiceDescriptor.Singleton<ICoinMappingDiffStorage, T>());
            return this;
        }
    }

    public static class TokenServiceCollectionExtensions
    {
        public static TokenServicesBuilder AddErc20TokenServices(
            this IServiceCollection services,
            Action<TokenServicesOptions> configureOptions = null)
        {
            var options = new TokenServicesOptions();
            configureOptions?.Invoke(options);

            services.TryAddSingleton(options);

            // Cache provider
            if (options.UseFileCache)
            {
                services.TryAddSingleton<ICacheProvider>(sp =>
                    new FileCacheProvider(options.CacheDirectory));
            }
            else
            {
                services.TryAddSingleton<ICacheProvider, MemoryCacheProvider>();
            }

            // Diff storage providers (pluggable for PostgreSQL, SQLite, etc.)
            if (options.UseFileDiffStorage)
            {
                services.TryAddSingleton<ITokenListDiffStorage>(sp =>
                    new FileTokenListDiffStorage(options.TokenListDiffStorageDirectory));

                services.TryAddSingleton<ICoinMappingDiffStorage>(sp =>
                    new FileCoinMappingDiffStorage(options.CoinMappingDiffStorageDirectory));
            }
            else
            {
                // Default: no persistence for diff storage (in-memory only via cache)
                // Users can plug in their own implementations via builder methods
                services.TryAddSingleton<ITokenListDiffStorage, NullTokenListDiffStorage>();
                services.TryAddSingleton<ICoinMappingDiffStorage, NullCoinMappingDiffStorage>();
            }

            services.TryAddSingleton<CoinGeckoApiService>();

            // Embedded providers (fallbacks)
            services.TryAddSingleton<EmbeddedTokenListProvider>();
            services.TryAddSingleton<EmbeddedCoinMappingProvider>();

            // CoinGecko token list provider (primary)
            services.TryAddSingleton<CoinGeckoTokenListProvider>(sp =>
            {
                var cache = sp.GetRequiredService<ICacheProvider>();
                var coinGecko = sp.GetRequiredService<CoinGeckoApiService>();
                var opts = sp.GetRequiredService<TokenServicesOptions>();
                return new CoinGeckoTokenListProvider(
                    coinGecko,
                    cache,
                    opts.TokenListCacheExpiry,
                    opts.PlatformsCacheExpiry);
            });

            // Resilient token list provider (cache -> API -> embedded)
            services.TryAddSingleton<ITokenListProvider>(sp =>
            {
                var cache = sp.GetRequiredService<ICacheProvider>();
                var coinGecko = sp.GetRequiredService<CoinGeckoTokenListProvider>();
                var embedded = sp.GetRequiredService<EmbeddedTokenListProvider>();
                var diffStorage = sp.GetRequiredService<ITokenListDiffStorage>();
                var opts = sp.GetRequiredService<TokenServicesOptions>();

                // Pass null if using NullTokenListDiffStorage
                var actualDiffStorage = diffStorage is NullTokenListDiffStorage ? null : diffStorage;

                return new ResilientTokenListProvider(
                    coinGecko,
                    embedded,
                    diffStorage: actualDiffStorage,
                    cacheProvider: cache,
                    cacheExpiry: opts.TokenListCacheExpiry);
            });

            services.TryAddSingleton<ITokenBalanceProvider>(sp =>
            {
                var opts = sp.GetRequiredService<TokenServicesOptions>();
                return new MultiCallBalanceProvider(opts.MultiCallBatchSize);
            });

            services.TryAddSingleton<ITokenPriceProvider>(sp =>
            {
                var cache = sp.GetRequiredService<ICacheProvider>();
                var coinGecko = sp.GetRequiredService<CoinGeckoApiService>();
                var embeddedCoins = sp.GetRequiredService<EmbeddedCoinMappingProvider>();
                var opts = sp.GetRequiredService<TokenServicesOptions>();
                return new CoinGeckoPriceProvider(
                    coinGecko,
                    cache,
                    opts.PriceCacheExpiry,
                    opts.CoinsListCacheExpiry,
                    embeddedCoins);
            });

            services.TryAddSingleton<ITokenEventScanner, Erc20EventScanner>();

            services.TryAddSingleton<IResourceRefreshCoordinator>(sp =>
            {
                var coinGecko = sp.GetRequiredService<CoinGeckoApiService>();
                var cache = sp.GetRequiredService<ICacheProvider>();
                var embedded = sp.GetRequiredService<EmbeddedTokenListProvider>();
                var opts = sp.GetRequiredService<TokenServicesOptions>();
                return new ResourceRefreshCoordinator(
                    coinGecko,
                    cache,
                    embedded,
                    opts.TokenListCacheExpiry,
                    opts.PlatformsCacheExpiry,
                    opts.MinTimeBetweenRefreshes,
                    opts.MaxRefreshRetries);
            });

            services.TryAddSingleton<IErc20TokenService>(sp =>
            {
                var tokenList = sp.GetRequiredService<ITokenListProvider>();
                var balance = sp.GetRequiredService<ITokenBalanceProvider>();
                var price = sp.GetRequiredService<ITokenPriceProvider>();
                var eventScanner = sp.GetRequiredService<ITokenEventScanner>();
                var cache = sp.GetRequiredService<ICacheProvider>();
                return new Erc20TokenService(tokenList, balance, price, eventScanner, cache);
            });

            services.TryAddSingleton<ITokenDiscoveryEngine>(sp =>
            {
                var tokenList = sp.GetRequiredService<ITokenListProvider>();
                var balance = sp.GetRequiredService<ITokenBalanceProvider>();
                return new TokenDiscoveryEngine(tokenList, balance);
            });

            services.TryAddSingleton<ITokenRefreshOrchestrator>(sp =>
            {
                var eventScanner = sp.GetRequiredService<ITokenEventScanner>();
                var balance = sp.GetRequiredService<ITokenBalanceProvider>();
                var tokenList = sp.GetRequiredService<ITokenListProvider>();
                return new TokenRefreshOrchestrator(eventScanner, balance, tokenList);
            });

            services.TryAddSingleton<IDiscoveryStrategy>(sp =>
            {
                var tokenList = sp.GetRequiredService<ITokenListProvider>();
                var discoveryEngine = sp.GetRequiredService<ITokenDiscoveryEngine>();
                return new TokenListDiscoveryStrategy(tokenList, discoveryEngine);
            });

            services.TryAddSingleton<IMultiAccountTokenService>(sp =>
            {
                var tokenService = sp.GetRequiredService<IErc20TokenService>();
                var refreshOrchestrator = sp.GetService<ITokenRefreshOrchestrator>();
                return new MultiAccountTokenService(tokenService, refreshOrchestrator);
            });

            return new TokenServicesBuilder(services, options);
        }
    }
}
