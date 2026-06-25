using System;
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
using Nethereum.MainnetChain.Server.Bootstrap;
using Nethereum.MainnetChain.Server.Configuration;
using Nethereum.MainnetChain.Server.Gate;
using Nethereum.MainnetChain.Server.Observability;
using Nethereum.MainnetChain.Server.Rpc;

namespace Nethereum.MainnetChain.Server.Hosting
{
    public static class MainnetChainServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the chain-node DI graph for the read-only mainnet follower:
        /// <see cref="MainnetChainNodeFactory"/>, the configured <see cref="IConsensusBlockGate"/>
        /// (Always-Accept or LightClient-backed depending on <c>BeaconEndpoint</c>), the
        /// <see cref="IFinalityCursorProvider"/> used by the finality-aware RPC handlers, and
        /// the RPC dispatcher with a finality-label override over <c>eth_getBlockByNumber</c>.
        /// <para>
        /// The <see cref="IChainStoreBundle"/> and <see cref="IBlockSource"/> are NOT registered
        /// here — callers wire those in after this method, via either
        /// <see cref="UseInMemoryBundleAndSource"/> (tests) or a real RocksDB+DevP2P composition
        /// from <c>tools/Nethereum.DevP2P.SyncNode</c>.
        /// </para>
        /// </summary>
        public static IServiceCollection AddMainnetChainServer(
            this IServiceCollection services,
            MainnetChainServerConfig config)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (config == null) throw new ArgumentNullException(nameof(config));

            services.AddSingleton(config);

            services.TryAddSingleton<IValidationPolicy>(_ => new ProductionValidationPolicyAdapter(config));
            services.TryAddSingleton<FollowerOptions>(_ => new FollowerOptions(
                StartBlock: config.StartBlock,
                CheckpointEvery: config.CheckpointEvery,
                AnchorEvery: 0UL,
                EndBlock: config.Blocks == ulong.MaxValue ? null : config.StartBlock + config.Blocks - 1,
                KeepLatestCheckpoints: config.KeepLatestCheckpoints));

            var lightClientEnabled = !string.IsNullOrWhiteSpace(config.LightClient?.BeaconEndpoint);
            if (lightClientEnabled)
            {
                services.TryAddSingleton<ILightClientStore, InMemoryLightClientStore>();
                services.TryAddSingleton<BeaconApiClient>(_ =>
                    new BeaconApiClient(config.LightClient!.BeaconEndpoint!));
                services.TryAddSingleton<Nethereum.Beaconchain.LightClient.ILightClientApi>(sp =>
                    sp.GetRequiredService<BeaconApiClient>().LightClient);
                if (config.LightClient!.TrustBeaconWithoutBls)
                    services.TryAddSingleton<Signer.Bls.IBls, NoopBls>();
                else
                    services.TryAddSingleton<Signer.Bls.IBls>(_ =>
                        new Signer.Bls.NativeBls(new Signer.Bls.Herumi.HerumiNativeBindings()));
                services.TryAddSingleton<LightClientConfig>(_ =>
                {
                    // Mainnet defaults (genesis-validators-root + chain spec) from the
                    // canonical per-network constants; config can still override below.
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

            // Canonical tip + point-validation source. When the light client
            // is configured, it IS the canonical tip authority — same model
            // as the Engine API forkchoiceUpdated pivot, but in-process.
            // Hardcoded mainnet checkpoints answer historical fork-boundary point
            // lookups first; the light client answers the live tip. Without a
            // light client, only the checkpoint table is available and the
            // snap-bootstrap pivot falls back to peer-pool sampling.
            //
            // useOptimistic: the canonical tip tracks the OPTIMISTIC (attested)
            // head, not the finalized one. The finalized head lags the chain by
            // ~2 epochs (~100+ blocks); a snap pivot derived from it falls outside
            // the ~128-block window peers keep snapshots for, so no peer can serve
            // GetAccountRange at that root. The attested head (BLS sync-committee
            // signed, execution branch verified) is ~1 slot old, so the pivot sits
            // well inside the servable window. RPC finality labelling is a separate
            // provider (IFinalityCursorProvider) and still uses the finalized head.
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
                    follower: sp.GetService<IFollowerService>()));

            services.TryAddSingleton<IFollowerService>(sp =>
            {
                var walker = sp.GetService<BackwardWalkerDelegate>();
                var ancestorResolver = sp.GetService<AncestorResolverDelegate>();
                return new FollowerService(walker, ancestorResolver);
            });

            services.TryAddSingleton<MainnetChainNodeAccessor>();

            if (!string.IsNullOrWhiteSpace(config.DataDir))
            {
                services.UseRocksDbAndDevP2PProductionComposition(config);
            }

            // SnapSyncMetrics singleton — drained by the reporter heartbeat
            // and by the per-phase backfillers (HistoricalBlockBackfiller,
            // ParallelBlockBackfiller) that already accept it as a nullable
            // ctor parameter. Without this registration the class is dead
            // weight and operators get neither dashboards nor logs.
            services.TryAddSingleton<SnapSyncMetrics>(_ => new SnapSyncMetrics("Nethereum"));

            // 8-second heartbeat publisher. Reads cursors from the bundle +
            // counters from the persisted SnapSyncState + peer-pool composition
            // + canonical-source staleness; emits the snap progress log lines.
            services.AddHostedService<SnapSyncProgressReporter>();

            services.AddHostedService<MainnetChainHostedService>();

            services.AddSingleton<RpcHandlerRegistry>(sp =>
            {
                var registry = new RpcHandlerRegistry();
                registry.AddStandardHandlers();
                registry.Override(new MainnetChainEthGetBlockByNumberHandler());
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

        /// <summary>
        /// Registers an in-memory <see cref="IChainStoreBundle"/> with a supplied
        /// <see cref="IBlockSource"/>. Used by integration tests; production hosts wire a
        /// RocksDB bundle and a DevP2P-backed source explicitly.
        /// </summary>
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

    /// <summary>
    /// Thin adapter exposing <c>Nethereum.DevP2P.SyncNode.ProductionValidationPolicy</c>'s
    /// shape (continue-on-mismatch toggle + anchor cadence + rewind verdict) without
    /// pulling the SyncNode tool project as a project reference. Mirrors the rewind-and-retry
    /// default used by SyncNode for the production sync path.
    /// </summary>
    internal sealed class ProductionValidationPolicyAdapter : IValidationPolicy
    {
        private readonly MainnetChainServerConfig _config;

        public ProductionValidationPolicyAdapter(MainnetChainServerConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public bool ShouldAnchorAt(ulong blockNumber) => false;

        public ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber)
        {
            if (_config.ContinueOnMismatch)
            {
                return ValidationAction.Continue;
            }

            return ValidationAction.RewindAndRetry;
        }
    }
}
