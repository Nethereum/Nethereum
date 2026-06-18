using System;
using System.Numerics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Nethereum.Consensus.LightClient;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.MainnetChain.Server.Configuration;
using Nethereum.MainnetChain.Server.Gate;
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

            services.TryAddSingleton<ICanonicalStateRootSource>(_ => new MainnetKnownCheckpoints());

            var lightClientEnabled = !string.IsNullOrWhiteSpace(config.LightClient?.BeaconEndpoint);
            if (lightClientEnabled)
            {
                services.TryAddSingleton<ILightClientStore, InMemoryLightClientStore>();
                services.TryAddSingleton<IConsensusBlockGate>(sp =>
                    new LightClientConsensusBlockGate(sp.GetRequiredService<LightClientService>()));
                services.TryAddSingleton<IFinalityCursorProvider>(sp =>
                    new LightClientFinalityCursorProvider(sp.GetRequiredService<LightClientService>()));
                services.AddHostedService<LightClientHostedService>();
            }
            else
            {
                services.TryAddSingleton<IConsensusBlockGate, AlwaysAcceptConsensusBlockGate>();
                services.TryAddSingleton<IFinalityCursorProvider, LatestOnlyFinalityCursorProvider>();
            }

            services.TryAddSingleton<MainnetChainNodeFactory>();

            services.AddSingleton<MainnetChainNode>(sp =>
            {
                var factory = sp.GetRequiredService<MainnetChainNodeFactory>();
                var bundle = sp.GetRequiredService<IChainStoreBundle>();
                var source = sp.GetRequiredService<IBlockSource>();
                var policy = sp.GetRequiredService<IValidationPolicy>();
                var options = sp.GetRequiredService<FollowerOptions>();
                var canonical = sp.GetService<ICanonicalStateRootSource>();
                return factory.Build(bundle, source, policy, options, canonical);
            });

            services.AddHostedService<MainnetChainHostedService>();

            services.AddSingleton<RpcHandlerRegistry>(sp =>
            {
                var registry = new RpcHandlerRegistry();
                registry.AddStandardHandlers();
                registry.Override(new MainnetChainEthGetBlockByNumberHandler());
                return registry;
            });

            services.AddSingleton<RpcContext>(sp =>
                new RpcContext(
                    sp.GetRequiredService<MainnetChainNode>(),
                    (BigInteger)Nethereum.EVM.MainnetGenesisConstants.ChainId,
                    sp));

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
