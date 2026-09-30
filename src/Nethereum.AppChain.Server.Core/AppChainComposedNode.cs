using Nethereum.AppChain.Anchoring.Messaging;
using Nethereum.AppChain.Anchoring.Metrics;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Sequencer.Metrics;
using Nethereum.AppChain.Sequencer.ProducerAuthority;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.AppChain.Server.Metrics;
using Nethereum.Consensus.Clique;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Metrics;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.Model;

namespace Nethereum.AppChain.Server
{
    public sealed class AppChainComposedNode : IAsyncDisposable
    {
        public required Nethereum.ChainNode.Hosting.ChainNode ChainNode { get; init; }

        public required Nethereum.AppChain.AppChain AppChain { get; init; }

        public required AppChainNode Node { get; init; }

        public SequencerConfig? SequencerConfig { get; init; }

        public ISequencer? Sequencer { get; init; }

        public CliqueEngine? CliqueEngine { get; init; }

        public CliqueBlockProductionStrategy? CliqueStrategy { get; init; }

        public required IncrementalStateRootCalculator StateRootCalculator { get; init; }

        public required IFinalityTracker FinalityTracker { get; init; }

        public required IMessageResultStore MessageResultStore { get; init; }

        public required IMessageMerkleAccumulator MessageAccumulator { get; init; }

        public IMessageQueue? MessageQueue { get; init; }

        public IMessageProcessor? MessageProcessor { get; init; }

        public MudGenesisResult? MudResult { get; init; }

        public bool Imports { get; init; }

        public bool Produces { get; init; }

        public required AppChainMetricsBundle Metrics { get; init; }

        public IProducerAuthority? ProducerAuthority { get; init; }

        public string? ProducerAuthorityNodeId { get; init; }

        public IChainForkChoice? ForkChoice { get; init; }

        public IChainStoreBundle Bundle => ChainNode.Bundle;

        public async ValueTask DisposeAsync()
        {
            if (Sequencer != null)
            {
                try { await Sequencer.StopAsync(); } catch { }
            }

            if (ProducerAuthority is IAsyncDisposable disposableAuthority)
            {
                try { await disposableAuthority.DisposeAsync(); } catch { }
            }

            await ChainNode.DisposeAsync();
        }
    }

    public sealed class AppChainMetricsBundle
    {
        public required BlockProductionMetrics BlockProduction { get; init; }

        public required TxPoolMetrics TxPool { get; init; }

        public required RpcMetrics Rpc { get; init; }

        public required StorageMetrics Storage { get; init; }

        public required SyncMetrics Sync { get; init; }

        public required SequencerMetrics Sequencer { get; init; }

        public required HAMetrics HA { get; init; }

        public required AnchoringMetrics Anchoring { get; init; }

        public static AppChainMetricsBundle Create(AppChainServerConfig config)
        {
            var chainIdStr = config.ChainId.ToString();
            var metricsName = config.ChainName ?? "Nethereum";

            return new AppChainMetricsBundle
            {
                BlockProduction = new BlockProductionMetrics(chainIdStr, metricsName),
                TxPool = new TxPoolMetrics(chainIdStr, metricsName),
                Rpc = new RpcMetrics(chainIdStr, metricsName),
                Storage = new StorageMetrics(chainIdStr, metricsName),
                Sync = new SyncMetrics(chainIdStr, metricsName),
                Sequencer = new SequencerMetrics(chainIdStr, metricsName),
                HA = new HAMetrics(chainIdStr, metricsName),
                Anchoring = new AnchoringMetrics(chainIdStr, metricsName),
            };
        }
    }

    public sealed class AppChainMessagingComposition
    {
        public AppChainMessagingComposition(
            IMessageResultStore resultStore,
            IMessageMerkleAccumulator accumulator,
            IMessageQueue? queue,
            IMessageProcessor? processor)
        {
            ResultStore = resultStore;
            Accumulator = accumulator;
            Queue = queue;
            Processor = processor;
        }

        public IMessageResultStore ResultStore { get; }

        public IMessageMerkleAccumulator Accumulator { get; }

        public IMessageQueue? Queue { get; }

        public IMessageProcessor? Processor { get; }
    }
}
