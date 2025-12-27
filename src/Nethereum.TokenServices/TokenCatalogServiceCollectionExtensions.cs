using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nethereum.DataServices.CoinGecko;
using Nethereum.TokenServices.Caching;
using Nethereum.TokenServices.ERC20.Catalog;
using Nethereum.TokenServices.ERC20.Catalog.Sources;
using Nethereum.TokenServices.ERC20.Discovery;

namespace Nethereum.TokenServices
{
    public class TokenCatalogOptions
    {
        public string CatalogDirectory { get; set; }
        public bool AutoSeedFromEmbedded { get; set; } = true;
        public TimeSpan DefaultRefreshInterval { get; set; } = TimeSpan.FromHours(6);
        public TimeSpan RateLimitDelay { get; set; } = TimeSpan.FromSeconds(1.5);
        public bool RegisterCoinGeckoSource { get; set; } = true;
    }

    public class TokenCatalogBuilder
    {
        private readonly IServiceCollection _services;
        private readonly TokenCatalogOptions _options;

        public TokenCatalogBuilder(IServiceCollection services, TokenCatalogOptions options)
        {
            _services = services;
            _options = options;
        }

        public TokenCatalogBuilder UseRepository<T>() where T : class, ITokenCatalogRepository
        {
            _services.Replace(ServiceDescriptor.Singleton<ITokenCatalogRepository, T>());
            return this;
        }

        public TokenCatalogBuilder UseRepository(Func<IServiceProvider, ITokenCatalogRepository> factory)
        {
            _services.Replace(ServiceDescriptor.Singleton(factory));
            return this;
        }

        public TokenCatalogBuilder AddRefreshSource<T>() where T : class, ITokenCatalogRefreshSource
        {
            _services.AddSingleton<ITokenCatalogRefreshSource, T>();
            return this;
        }

        public TokenCatalogBuilder AddRefreshSource(Func<IServiceProvider, ITokenCatalogRefreshSource> factory)
        {
            _services.AddSingleton(factory);
            return this;
        }
    }

    public static class TokenCatalogServiceCollectionExtensions
    {
        public static TokenCatalogBuilder AddTokenCatalog(
            this IServiceCollection services,
            Action<TokenCatalogOptions> configureOptions = null)
        {
            var options = new TokenCatalogOptions();
            configureOptions?.Invoke(options);

            services.TryAddSingleton(options);

            services.TryAddSingleton<EmbeddedTokenListProvider>();

            services.TryAddSingleton<CoinGeckoApiService>();

            services.TryAddSingleton<ITokenCatalogRepository>(sp =>
            {
                var embedded = sp.GetRequiredService<EmbeddedTokenListProvider>();
                var opts = sp.GetRequiredService<TokenCatalogOptions>();
                return new FileTokenCatalogRepository(
                    opts.CatalogDirectory,
                    embedded);
            });

            if (options.RegisterCoinGeckoSource)
            {
                services.AddSingleton<ITokenCatalogRefreshSource>(sp =>
                {
                    var coinGecko = sp.GetRequiredService<CoinGeckoApiService>();
                    var opts = sp.GetRequiredService<TokenCatalogOptions>();
                    return new CoinGeckoRefreshSource(coinGecko, opts.RateLimitDelay);
                });
            }

            services.TryAddSingleton<ITokenCatalogRefreshService>(sp =>
            {
                var repository = sp.GetRequiredService<ITokenCatalogRepository>();
                var sources = sp.GetServices<ITokenCatalogRefreshSource>();
                return new TokenCatalogRefreshService(repository, sources);
            });

            return new TokenCatalogBuilder(services, options);
        }

        public static TokenCatalogBuilder AddTokenCatalogWithCustomRepository<TRepository>(
            this IServiceCollection services,
            Action<TokenCatalogOptions> configureOptions = null)
            where TRepository : class, ITokenCatalogRepository
        {
            var options = new TokenCatalogOptions();
            configureOptions?.Invoke(options);

            services.TryAddSingleton(options);

            services.TryAddSingleton<CoinGeckoApiService>();

            services.TryAddSingleton<ITokenCatalogRepository, TRepository>();

            if (options.RegisterCoinGeckoSource)
            {
                services.AddSingleton<ITokenCatalogRefreshSource>(sp =>
                {
                    var coinGecko = sp.GetRequiredService<CoinGeckoApiService>();
                    var opts = sp.GetRequiredService<TokenCatalogOptions>();
                    return new CoinGeckoRefreshSource(coinGecko, opts.RateLimitDelay);
                });
            }

            services.TryAddSingleton<ITokenCatalogRefreshService>(sp =>
            {
                var repository = sp.GetRequiredService<ITokenCatalogRepository>();
                var sources = sp.GetServices<ITokenCatalogRefreshSource>();
                return new TokenCatalogRefreshService(repository, sources);
            });

            return new TokenCatalogBuilder(services, options);
        }
    }
}
