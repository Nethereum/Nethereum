using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Anchoring.Metrics;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Sequencer.Metrics;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.AppChain.Server.Metrics;
using Nethereum.AppChain.Server.Rpc;
using Nethereum.AppChain.Sync;
using Nethereum.AppChain.Sync.Metrics;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Metrics;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using OpenTelemetry.Metrics;

namespace Nethereum.AppChain.Server.Hosting
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAppChainServer(
            this IServiceCollection services,
            AppChainServerConfig config)
        {
            services.AddSingleton(config);
            services.AddSingleton<MudWorldDeployer>();

            services.AddSingleton<IBatchStore, InMemoryBatchStore>();
            services.AddSingleton<IFinalityTracker, InMemoryFinalityTracker>();

            return services;
        }

        public static IServiceCollection AddPeerManager(
            this IServiceCollection services,
            PeerManagerConfig? peerConfig = null,
            IEnumerable<string>? initialPeers = null)
        {
            services.AddSingleton<IPeerManager>(provider =>
            {
                var manager = new PeerManager(peerConfig);
                if (initialPeers != null)
                {
                    foreach (var peer in initialPeers)
                    {
                        manager.AddPeer(peer);
                    }
                }
                return manager;
            });

            return services;
        }

        public static RpcHandlerRegistry AddAdminHandlers(this RpcHandlerRegistry registry)
        {
            registry.Register(new AdminAddPeerHandler());
            registry.Register(new AdminRemovePeerHandler());
            registry.Register(new AdminPeersHandler());
            registry.Register(new AdminNodeInfoHandler());
            return registry;
        }

        public static IServiceCollection AddMultiPeerSync(
            this IServiceCollection services,
            IEnumerable<string> peerUrls,
            MultiPeerSyncConfig? syncConfig = null,
            PeerManagerConfig? peerConfig = null,
            bool enableStateSync = true)
        {
            services.AddSingleton<IPeerManager>(provider =>
            {
                var manager = new PeerManager(peerConfig);
                foreach (var url in peerUrls)
                {
                    manager.AddPeer(url);
                }
                return manager;
            });

            services.AddSingleton(syncConfig ?? MultiPeerSyncConfig.Default);

            if (enableStateSync)
            {
                services.AddSingleton<IBlockReExecutor>(provider =>
                {
                    var stateStore = provider.GetRequiredService<IStateStore>();
                    var blockStore = provider.GetRequiredService<IBlockStore>();
                    var chainConfig = provider.GetRequiredService<ChainConfig>();
                    var txVerifier = provider.GetRequiredService<ITransactionVerificationAndRecovery>();
                    var loggerFactory = provider.GetService<ILoggerFactory>();
                    var sharedCalculator = provider.GetService<IncrementalStateRootCalculator>();

                    var transactionProcessor = new TransactionProcessor(
                        stateStore,
                        blockStore,
                        chainConfig,
                        txVerifier,
                        chainConfig.GetHardforkConfig());

                    return new BlockReExecutor(
                        transactionProcessor,
                        stateStore,
                        chainConfig,
                        loggerFactory?.CreateLogger<BlockReExecutor>(),
                        sharedCalculator);
                });
            }

            services.AddSingleton<ILiveBlockSync>(provider =>
            {
                var config = provider.GetRequiredService<MultiPeerSyncConfig>();
                var blockStore = provider.GetRequiredService<IBlockStore>();
                var txStore = provider.GetRequiredService<ITransactionStore>();
                var receiptStore = provider.GetRequiredService<IReceiptStore>();
                var logStore = provider.GetRequiredService<ILogStore>();
                var finalityTracker = provider.GetRequiredService<IFinalityTracker>();
                var peerManager = provider.GetRequiredService<IPeerManager>();
                var blockReExecutor = provider.GetService<IBlockReExecutor>();
                var loggerFactory = provider.GetService<ILoggerFactory>();

                return new MultiPeerSyncService(
                    config,
                    blockStore,
                    txStore,
                    receiptStore,
                    logStore,
                    finalityTracker,
                    peerManager,
                    blockReExecutor,
                    loggerFactory?.CreateLogger<MultiPeerSyncService>());
            });

            return services;
        }

        public static IServiceCollection AddSequencerCoordinator(
            this IServiceCollection services,
            SequencerCoordinatorConfig? config = null)
        {
            services.AddSingleton(config ?? SequencerCoordinatorConfig.Default);
            services.AddSingleton<ISequencerCoordinator, SequencerCoordinator>();
            return services;
        }

        public static IServiceCollection AddAppChainMetrics(
            this IServiceCollection services,
            AppChainServerConfig config)
        {
            var chainId = config.ChainId.ToString();
            var name = config.ChainName ?? "Nethereum";

            services.AddSingleton(new BlockProductionMetrics(chainId, name));
            services.AddSingleton(new TxPoolMetrics(chainId, name));
            services.AddSingleton(new RpcMetrics(chainId, name));
            services.AddSingleton(new StorageMetrics(chainId, name));
            services.AddSingleton(new SyncMetrics(chainId, name));
            services.AddSingleton(new SequencerMetrics(chainId, name));
            services.AddSingleton(new HAMetrics(chainId, name));
            services.AddSingleton(new AnchoringMetrics(chainId, name));
            services.AddSingleton(new MetricsConfig());

            return services;
        }

        public static IServiceCollection AddAppChainOpenTelemetry(
            this IServiceCollection services,
            AppChainServerConfig config)
        {
            var name = config.ChainName ?? "Nethereum";

            services.AddOpenTelemetry()
                .WithMetrics(metrics =>
                {
                    metrics.AddMeter($"{name}.CoreChain");
                    metrics.AddMeter($"{name}.CoreChain.Detailed");
                    metrics.AddMeter($"{name}.Sequencer");
                    metrics.AddMeter($"{name}.Sequencer.Detailed");
                    metrics.AddMeter($"{name}.Sync");
                    metrics.AddMeter($"{name}.Sync.Detailed");
                    metrics.AddMeter($"{name}.Anchoring");
                    metrics.AddMeter($"{name}.Anchoring.Detailed");
                    metrics.AddOtlpExporter(opts =>
                    {
                        if (!string.IsNullOrEmpty(config.OtlpEndpoint))
                        {
                            opts.Endpoint = new System.Uri(config.OtlpEndpoint);
                        }
                    });
                });

            return services;
        }

        public static IServiceCollection AddAppChainHealthChecks(this IServiceCollection services)
        {
            services.AddHealthChecks()
                .Add(new HealthCheckRegistration(
                    "sequencer",
                    sp => new SequencerHealthCheck(sp.GetService<ISequencer>()),
                    failureStatus: null,
                    tags: null))
                .Add(new HealthCheckRegistration(
                    "sync",
                    sp => new SyncHealthCheck(sp.GetService<ILiveBlockSync>()),
                    failureStatus: null,
                    tags: null));

            return services;
        }
    }
}
