using System;
using Nethereum.Documentation;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Engine;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.EngineApi.Hosting;
using Nethereum.MainnetChain.Bootstrap;
using Nethereum.MainnetChain.Configuration;

namespace Nethereum.MainnetChain.Hosting
{
    public static class MainnetEngineComposition
    {
        [NethereumDocExample(DocSection.ChainInfrastructure, "mainnet-follower", "Register the Engine API for the mainnet follower")]
        public static IServiceCollection AddMainnetEngine(this IServiceCollection services, MainnetChainServerConfig config)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (!string.IsNullOrWhiteSpace(config.LightClient?.BeaconEndpoint))
                throw new InvalidOperationException(ConsensusStartupGuard.EngineWithLightClientMessage);

            services.TryAddSingleton<AsyncGate>();
            services.TryAddSingleton<EnginePayloadStashOptions>();
            services.TryAddSingleton(sp => new EnginePayloadStash(sp.GetRequiredService<EnginePayloadStashOptions>()));
            services.TryAddSingleton<EngineForkchoiceHeaders>();
            services.TryAddSingleton<IEngineForkchoiceSink>(sp => sp.GetRequiredService<EngineForkchoiceHeaders>());

            services.TryAddSingleton<ICanonicalStateRootSource>(sp =>
            {
                var trusted = new TrustedHeaderCanonicalSource(
                    sp.GetRequiredService<EngineForkchoiceHeaders>(),
                    useOptimistic: true,
                    logger: sp.GetService<ILoggerFactory>()?.CreateLogger<TrustedHeaderCanonicalSource>());
                return config.EngineOnly
                    ? trusted
                    : new CompositeCanonicalStateRootSource(new MainnetKnownCheckpoints(), trusted);
            });

            services.TryAddSingleton<EngineHeaderResolver>(sp =>
            {
                var scheduler = sp.GetService<IFetchRequestScheduler>();
                HeaderByHashFetcher peers = scheduler == null
                    ? null
                    : async (hash, ct) => (await scheduler.FetchHeadersByHashAsync(hash, 1, ct).ConfigureAwait(false)).FirstOrDefault();
                return new EngineHeaderResolver(
                    sp.GetRequiredService<IChainStoreBundle>().Blocks,
                    sp.GetRequiredService<EnginePayloadStash>(),
                    peers);
            });

            services.TryAddSingleton<FollowerEngineApiService>(sp =>
                new FollowerEngineApiService(
                    sp.GetRequiredService<IChainStoreBundle>(),
                    sp.GetRequiredService<AsyncGate>(),
                    MainnetChainNodeFactory.BuildChainConfig(config.LoadGenesis()),
                    sp.GetRequiredService<FollowerOptions>(),
                    sp.GetRequiredService<EnginePayloadStash>(),
                    sp.GetRequiredService<EngineHeaderResolver>(),
                    sp.GetRequiredService<IEngineForkchoiceSink>(),
                    sp.GetService<ILoggerFactory>()?.CreateLogger<FollowerEngineApiService>()));

            services.AddEngineApiServer(
                new EngineApiServerConfig
                {
                    JwtSecretPath = config.Engine.JwtSecretPath
                        ?? Path.Combine(config.DataDir ?? ".", EngineConfigSection.DefaultJwtSecretFileName),
                    Port = config.Engine.Port,
                    BindAddress = config.Engine.BindAddress
                },
                sp => sp.GetRequiredService<FollowerEngineApiService>(),
                config.Verbose);

            return services;
        }
    }
}
