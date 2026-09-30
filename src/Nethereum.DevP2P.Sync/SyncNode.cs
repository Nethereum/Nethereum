using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.Documentation;
using Nethereum.EVM;

namespace Nethereum.DevP2P.Sync
{
    [NethereumDocExample(DocSection.DevP2P, "devp2p-sync", "SyncNode — the discoverable sync entry-point facade")]
    public sealed class SyncNode
    {
        private readonly IChainStoreBundle _bundle;
        private readonly IChainActivations _activations;
        private readonly ILogger _logger;

        public IPeerPool Peers { get; }

        public IFetchRequestScheduler Scheduler { get; }

        public PeerListener Serving { get; }

        public Nethereum.DevP2P.Sync.Mempool.RelayMempool Mempool { get; }

        public SyncNode(
            IChainStoreBundle bundle,
            IChainActivations activations,
            ILogger logger,
            IPeerPool peers = null,
            IFetchRequestScheduler scheduler = null,
            PeerListener serving = null,
            Nethereum.DevP2P.Sync.Mempool.RelayMempool mempool = null)
        {
            _bundle = bundle ?? throw new System.ArgumentNullException(nameof(bundle));
            _activations = activations ?? throw new System.ArgumentNullException(nameof(activations));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            Peers = peers;
            Scheduler = scheduler;
            Serving = serving;
            Mempool = mempool;
        }

        public System.Threading.Tasks.Task StartServingAsync(System.Threading.CancellationToken ct = default)
            => Serving?.StartAsync(ct) ?? System.Threading.Tasks.Task.CompletedTask;

        public System.Threading.Tasks.Task<SnapBootstrapper.Result> RunSnapBootstrapAsync(
            ICanonicalStateRootSource canonicalTip,
            SnapSyncOrchestratorOptions options = null,
            System.Threading.CancellationToken ct = default)
            => SnapSyncOrchestrator.RunAsync(_bundle, Peers, Scheduler, canonicalTip, _activations, _logger, options, ct);
    }
}
