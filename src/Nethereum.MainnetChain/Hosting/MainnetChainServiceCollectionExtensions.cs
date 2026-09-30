using System;
using System.Net.Http;
using System.Numerics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Nethereum.Beaconchain;
using Nethereum.Consensus.LightClient;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.Documentation;
using Nethereum.MainnetChain.Bootstrap;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Gate;
using Nethereum.MainnetChain.Observability;
using Nethereum.MainnetChain.Rpc;
using Nethereum.Signer;

namespace Nethereum.MainnetChain.Hosting
{
    public static class MainnetChainServiceCollectionExtensions
    {
        private static readonly TimeSpan BeaconPollTimeout = TimeSpan.FromSeconds(15);

        [NethereumDocExample(DocSection.ChainInfrastructure, "mainnet-follower", "Register the mainnet follower DI graph")]
        public static IServiceCollection AddMainnetChainServer(
            this IServiceCollection services,
            MainnetChainServerConfig config)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (config == null) throw new ArgumentNullException(nameof(config));

            EthECKey.SignRecoverable = true;

            services.AddSingleton(config);

            services.TryAddSingleton<IValidationPolicy>(sp =>
                new StrictValidationPolicy(
                    config.ContinueOnMismatch,
                    anchorEvery: 0,
                    logger: sp.GetService<ILogger<StrictValidationPolicy>>()));
            services.TryAddSingleton<FollowerOptions>(_ => new FollowerOptions(
                StartBlock: config.StartBlock,
                CheckpointEvery: config.CheckpointEvery,
                AnchorEvery: 0UL,
                EndBlock: config.Blocks == ulong.MaxValue ? null : config.StartBlock + config.Blocks - 1,
                KeepLatestCheckpoints: config.KeepLatestCheckpoints));

            var lightClientEnabled = !string.IsNullOrWhiteSpace(config.LightClient?.BeaconEndpoint);
            if (lightClientEnabled)
            {
                if (!string.IsNullOrWhiteSpace(config.DataDir))
                {
                    services.TryAddSingleton<ILightClientStore>(sp =>
                    {
                        var lcc = sp.GetRequiredService<LightClientConfig>();
                        var maxAge = TimeSpan.FromSeconds(lcc.WeakSubjectivityPeriod * lcc.SecondsPerSlot);
                        return new Bootstrap.DurableLightClientStore(
                            sp.GetRequiredService<IChainStoreBundle>().Metadata,
                            maxAge,
                            sp.GetRequiredService<ILoggerFactory>().CreateLogger<Bootstrap.DurableLightClientStore>());
                    });
                }
                services.TryAddSingleton<ILightClientStore, InMemoryLightClientStore>();
                services.TryAddSingleton<BeaconApiClient>(_ =>
                    new BeaconApiClient(
                        config.LightClient!.BeaconEndpoint!,
                        new HttpClient { Timeout = BeaconPollTimeout }));
                services.TryAddSingleton<Nethereum.Beaconchain.LightClient.ILightClientApi>(sp =>
                    sp.GetRequiredService<BeaconApiClient>().LightClient);
                if (config.LightClient!.TrustBeaconWithoutBls)
                    services.TryAddSingleton<Signer.Bls.IBls, NoopBls>();
                else
                    services.TryAddSingleton<Signer.Bls.IBls>(_ =>
                        new Signer.Bls.NativeBls(new Signer.Bls.Herumi.HerumiNativeBindings()));
                services.TryAddSingleton<LightClientConfig>(_ =>
                {
                    var lcc = LightClientNetworks.CreateConfig(1);
                    if (!string.IsNullOrWhiteSpace(config.LightClient!.WeakSubjectivityRoot))
                        lcc.WeakSubjectivityRoot = Hex.HexConvertors.Extensions.HexByteConvertorExtensions
                            .HexToByteArray(config.LightClient.WeakSubjectivityRoot);
                    if (!string.IsNullOrWhiteSpace(config.LightClient.GenesisValidatorsRoot))
                        lcc.GenesisValidatorsRoot = Hex.HexConvertors.Extensions.HexByteConvertorExtensions
                            .HexToByteArray(config.LightClient.GenesisValidatorsRoot);
                    return lcc;
                });
                services.TryAddSingleton<LightClientService>(sp => new LightClientService(
                    sp.GetRequiredService<Nethereum.Beaconchain.LightClient.ILightClientApi>(),
                    sp.GetRequiredService<Signer.Bls.IBls>(),
                    sp.GetRequiredService<LightClientConfig>(),
                    sp.GetRequiredService<ILightClientStore>()));
                services.TryAddSingleton<IConsensusBlockGate>(sp =>
                    new LightClientConsensusBlockGate(sp.GetRequiredService<LightClientService>()));
                services.TryAddSingleton<IFinalityCursorProvider>(sp =>
                    new LightClientFinalityCursorProvider(sp.GetRequiredService<LightClientService>()));
                services.TryAddSingleton<ITrustedHeaderProvider>(sp =>
                    new TrustedHeaderProvider(sp.GetRequiredService<LightClientService>()));
                services.AddHostedService<LightClientHostedService>();
            }
            else
            {
                services.TryAddSingleton<IConsensusBlockGate, AlwaysAcceptConsensusBlockGate>();
                services.TryAddSingleton<IFinalityCursorProvider, LatestOnlyFinalityCursorProvider>();
            }

            services.TryAddSingleton<ICanonicalStateRootSource>(sp =>
            {
                var checkpoints = new MainnetKnownCheckpoints();
                if (!lightClientEnabled)
                {
                    return checkpoints;
                }
                var lightClient = new LightClientCanonicalSource(
                    sp.GetRequiredService<ITrustedHeaderProvider>(),
                    useOptimistic: true,
                    logger: sp.GetService<ILoggerFactory>()?.CreateLogger<LightClientCanonicalSource>());
                return new CompositeCanonicalStateRootSource(checkpoints, lightClient);
            });

            services.TryAddSingleton<MainnetChainNodeFactory>(sp =>
                new MainnetChainNodeFactory(
                    sp.GetRequiredService<IConsensusBlockGate>(),
                    sp.GetRequiredService<ILoggerFactory>(),
                    follower: sp.GetService<IFollowerService>(),
                    flushCadence: MainnetNodeComposition.BuildFlushCadence(sp.GetRequiredService<MainnetChainServerConfig>())));

            services.TryAddSingleton<IFollowerService>(sp =>
            {
                var walker = sp.GetService<BackwardWalkerDelegate>();
                var ancestorResolver = sp.GetService<AncestorResolverDelegate>();
                var bodyRepair = sp.GetService<BodyRepairDelegate>();
                return new FollowerService(walker, ancestorResolver, bodyRepair);
            });

            services.TryAddSingleton<MainnetChainNodeAccessor>();


            services.TryAddSingleton<SnapSyncMetrics>(_ => new SnapSyncMetrics("Nethereum"));

            services.AddHostedService<SnapSyncProgressReporter>();

            services.AddHostedService<MainnetChainHostedService>();

            services.AddSingleton<RpcHandlerRegistry>(sp =>
            {
                var registry = new RpcHandlerRegistry();
                registry.AddStandardHandlers();
                registry.Override(new FinalityLabelledEthGetBlockByNumberHandler());
                return registry;
            });

            services.AddSingleton<RpcContext>(sp =>
            {
                var accessor = sp.GetRequiredService<MainnetChainNodeAccessor>();
                return new RpcContext(
                    () => accessor.Node,
                    (BigInteger)Nethereum.EVM.MainnetGenesisConstants.ChainId,
                    sp);
            });

            services.AddSingleton<RpcDispatcher>(sp =>
            {
                var registry = sp.GetRequiredService<RpcHandlerRegistry>();
                var context = sp.GetRequiredService<RpcContext>();
                var logger = config.Verbose ? sp.GetRequiredService<ILogger<RpcDispatcher>>() : null;
                return new RpcDispatcher(registry, context, logger);
            });

            return services;
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "mainnet-follower", "Wire an in-memory bundle and block source for tests")]
        public static IServiceCollection UseInMemoryBundleAndSource(
            this IServiceCollection services,
            IBlockSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            services.AddSingleton<IChainStoreBundle>(_ =>
                Nethereum.CoreChain.Storage.InMemory.InMemoryChainStoreBundle.Open(journalOptions: null));
            services.AddSingleton<IBlockSource>(source);
            return services;
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "mainnet-follower", "Register the follower graph on a WebApplicationBuilder")]
        public static WebApplicationBuilder AddMainnetChainServer(
            this WebApplicationBuilder builder,
            MainnetChainServerConfig config)
        {
            builder.Services.AddMainnetChainServer(config);
            builder.Services.AddCors(options =>
                options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
            return builder;
        }
    }

}
