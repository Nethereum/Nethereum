using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Anchoring.Metrics;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Sequencer.Metrics;
using Nethereum.AppChain.Sequencer.ProducerAuthority;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.AppChain.Server.Metrics;
using Nethereum.AppChain.Server.Rpc;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Metrics;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.Model;
using Nethereum.Signer;
using OpenTelemetry.Metrics;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;

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

            services.AddSingleton<IFinalityTracker, InMemoryFinalityTracker>();

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
                    sp => new SequencerHealthCheck(
                        sp.GetService<ISequencer>(),
                        sp.GetService<IProducerAuthority>(),
                        sp.GetService<ProducerAuthorityIdentity>()?.NodeId),
                    failureStatus: null,
                    tags: null))
                .Add(new HealthCheckRegistration(
                    "sync",
                    sp => new SyncHealthCheck(sp.GetService<IPeerPool>()),
                    failureStatus: null,
                    tags: null));

            return services;
        }
    }
}
