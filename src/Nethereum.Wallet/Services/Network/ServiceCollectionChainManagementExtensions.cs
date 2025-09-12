using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nethereum.RPC.Chain;
using Nethereum.Wallet.Services.Network.Strategies;
using Nethereum.DataServices.Chainlist;
using System.Threading.Tasks;

namespace Nethereum.Wallet.Services.Network
{
    public static class ServiceCollectionChainManagementExtensions
    {
        /// <summary>
        /// Registers chain management (strategy + service).
        /// Notes:
        /// - ChainManagementService and strategy are Scoped by default (safe for Blazor Server / MAUI).
        /// - We do NOT call AddHttpClient here to avoid forcing a package dependency in this library.
        /// - If you want HttpClientFactory integration, register ChainlistRpcApiService yourself before calling this.
        /// - When external enrichment is enabled and ChainlistRpcApiService is not already registered, a simple scoped instance
        ///   using its parameterless constructor (new HttpClient()) is registered. Override if you need pooling/factory.
        /// </summary>
        public static IServiceCollection AddNethereumChainManagement(
            this IServiceCollection services,
            Action<ChainManagementRegistrationOptions>? configure = null,
            ServiceLifetime chainServiceLifetime = ServiceLifetime.Scoped,
            ServiceLifetime strategyLifetime = ServiceLifetime.Scoped)
        {
            var options = new ChainManagementRegistrationOptions();
            configure?.Invoke(options);

            // Immutable preconfigured features (singleton).
            services.AddSingleton<IReadOnlyList<ChainFeature>>(_ =>
            {
                var baseList = (options.PreconfiguredFeatures ??
                                ChainDefaultFeaturesServicesRepository.GetDefaultChainFeatures())
                               .Select(Clone)
                               .ToList();
                options.PostProcessPreconfigured?.Invoke(baseList);
                return baseList;
            });

            // Default chain IDs (singleton; immutable).
            services.AddSingleton<IReadOnlyCollection<BigInteger>>(_ =>
                (options.DefaultChainIds ?? BuiltInDefaultChainIds.All).Distinct().ToArray());

            // External provider path
            if (options.EnableExternalChainList &&
                options.Strategy is not ChainFeatureStrategyType.PreconfiguredOnly)
            {
                // Respect existing user registration.
                services.TryAddScoped<ChainlistRpcApiService>(_ => new ChainlistRpcApiService());

                services.Add(new ServiceDescriptor(
                    typeof(IExternalChainFeaturesProvider),
                    sp =>
                    {
                        var api = sp.GetRequiredService<ChainlistRpcApiService>();
                        return new ChainListExternalChainFeaturesProvider(
                            api,
                            options.ChainListTtl);
                    },
                    strategyLifetime)); // align lifetime with strategy
            }
            else
            {
                services.AddScoped<IExternalChainFeaturesProvider, NullExternalChainFeaturesProvider>();
            }

            // Strategy
            services.Add(new ServiceDescriptor(
                typeof(IChainFeatureSourceStrategy),
                sp =>
                {
                    var pre = sp.GetRequiredService<IReadOnlyList<ChainFeature>>();
                    var ids = sp.GetRequiredService<IReadOnlyCollection<BigInteger>>();
                    var ext = sp.GetService<IExternalChainFeaturesProvider>();

                    return options.Strategy switch
                    {
                        ChainFeatureStrategyType.PreconfiguredOnly =>
                            new PreconfiguredOnlyStrategy(pre, ids),

                        ChainFeatureStrategyType.ExternalOnly =>
                            new ExternalOnlyStrategy(ids,
                                RequireExternal(ext, "ExternalOnly")),

                        ChainFeatureStrategyType.PreconfiguredEnrich =>
                            new PreconfiguredEnrichStrategy(pre,
                                // If enrichment selected but external disabled, ext will be NullExternalChainFeaturesProvider (safe)
                                IsRealExternal(ext) ? ext : null,
                                ids),

                        _ => throw new ArgumentOutOfRangeException(nameof(options.Strategy))
                    };
                },
                strategyLifetime));

            // Chain management service
            services.Add(new ServiceDescriptor(
                typeof(IChainManagementService),
                typeof(ChainManagementService),
                chainServiceLifetime));

            return services;
        }

        private static IExternalChainFeaturesProvider RequireExternal(
            IExternalChainFeaturesProvider? ext,
            string mode)
        {
            if (ext is NullExternalChainFeaturesProvider || ext == null)
                throw new InvalidOperationException($"{mode} strategy requires external ChainList integration (EnableExternalChainList=true).");
            return ext;
        }

        private static bool IsRealExternal(IExternalChainFeaturesProvider? ext) =>
            ext is not NullExternalChainFeaturesProvider && ext != null;

        private static ChainFeature Clone(ChainFeature c) => new()
        {
            ChainId = c.ChainId,
            ChainName = c.ChainName,
            IsTestnet = c.IsTestnet,
            NativeCurrency = c.NativeCurrency == null ? null : new NativeCurrency
            {
                Name = c.NativeCurrency.Name,
                Symbol = c.NativeCurrency.Symbol,
                Decimals = c.NativeCurrency.Decimals
            },
            SupportEIP155 = c.SupportEIP155,
            SupportEIP1559 = c.SupportEIP1559,
            HttpRpcs = c.HttpRpcs?.ToList() ?? new List<string>(),
            WsRpcs = c.WsRpcs?.ToList() ?? new List<string>(),
            Explorers = c.Explorers?.ToList() ?? new List<string>()
        };

        /// <summary>
        /// Null-object external provider so strategy code doesn't need to check for null.
        /// </summary>
        private sealed class NullExternalChainFeaturesProvider : IExternalChainFeaturesProvider
        {
            public Task<ChainFeature?> GetExternalChainAsync(BigInteger chainId) =>
                Task.FromResult<ChainFeature?>(null);

            public Task<IReadOnlyList<ChainFeature>> GetExternalChainsAsync(IEnumerable<BigInteger> chainIds) =>
                Task.FromResult<IReadOnlyList<ChainFeature>>(Array.Empty<ChainFeature>());

            public Task<bool> RefreshAsync(BigInteger chainId) =>
                Task.FromResult(false);
        }
    }
}