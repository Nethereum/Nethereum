using System;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.ChainNode.Hosting;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.EVM;
using Nethereum.EVM.Precompiles;

namespace Nethereum.AppChain.Server.Hosting
{
    public sealed class AppChainDevP2PFollower
    {
        private static readonly TimeSpan PeerWaitTimeout = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan PeerPollInterval = TimeSpan.FromMilliseconds(250);

        private readonly AppChainServerConfig _config;
        private readonly Nethereum.EVM.IChainActivations _activations;
        private readonly Nethereum.ChainNode.Hosting.ChainNode _node;
        private readonly IChainStoreBundle _bundle;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        private readonly Consensus.Clique.CliqueEngine? _clique;
        private readonly IChainForkChoice? _forkChoice;
        private readonly IMempoolReorgReconciler? _mempoolReconciler;

        public AppChainDevP2PFollower(
            AppChainServerConfig config, Nethereum.ChainNode.Hosting.ChainNode node, ILoggerFactory loggerFactory,
            Consensus.Clique.CliqueEngine? clique = null,
            IChainForkChoice? forkChoice = null,
            IMempoolReorgReconciler? mempoolReconciler = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _activations = Nethereum.EVM.ChainRules.ForConsensus(_config.ForkSchedule);
            _clique = clique;
            _forkChoice = forkChoice;
            _mempoolReconciler = mempoolReconciler;
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _bundle = node.Bundle;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger("Nethereum.AppChain.Server.DevP2PFollower");
        }

        public static Task RunAsync(
            AppChainServerConfig config, Nethereum.ChainNode.Hosting.ChainNode node,
            ILoggerFactory loggerFactory, CancellationToken ct,
            Consensus.Clique.CliqueEngine? clique = null,
            IChainForkChoice? forkChoice = null,
            IMempoolReorgReconciler? mempoolReconciler = null) =>
            new AppChainDevP2PFollower(config, node, loggerFactory, clique, forkChoice, mempoolReconciler).RunAsync(ct);

        public async Task RunAsync(CancellationToken ct)
        {
            var node = _node.Sync;
            if (node == null)
            {
                _logger.LogError("DevP2P follower: the node was composed without a sync stack");
                return;
            }

            var peer = await WaitForSnapPeerAsync(node, ct).ConfigureAwait(false);
            if (peer == null) return;

            var pivot = await ResolvePivotAsync(peer, ct).ConfigureAwait(false);
            if (pivot == null) return;

            await SnapBootstrapAsync(node, pivot, ct).ConfigureAwait(false);

            await ForwardExecuteAsync(node, ct).ConfigureAwait(false);
        }



        private async Task<SyncPeerSession> WaitForSnapPeerAsync(ChainNodeSyncStack node, CancellationToken ct)
        {
            _logger.LogInformation("DevP2P follower dialing producer {Enode}", _config.Node.Sync.FollowPeerEnode);

            var deadline = DateTime.UtcNow.Add(PeerWaitTimeout);
            while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
            {
                var peer = node.Pool.ActivePeers.OfType<SyncPeerSession>().FirstOrDefault(p => p.SupportsSnap);
                if (peer != null) return peer;

                await Task.Delay(PeerPollInterval, ct).ConfigureAwait(false);
            }

            _logger.LogError("DevP2P follower: no snap-capable producer at {Enode}",
                _config.Node.Sync.FollowPeerEnode);
            return null;
        }

        private async Task<FixedTipCanonicalSource> ResolvePivotAsync(SyncPeerSession peer, CancellationToken ct)
        {
            var headNumber = peer.PeerLatestBlock;
            var headers = await peer.GetHeadersAsync(headNumber, 1, ct).ConfigureAwait(false);

            if (headers == null || headers.Count == 0)
            {
                _logger.LogError("DevP2P follower: no pivot header at {Block}", headNumber);
                return null;
            }

            return new FixedTipCanonicalSource(headNumber, peer.PeerLatestBlockHash, headers[0].StateRoot);
        }

        private async Task SnapBootstrapAsync(
            ChainNodeSyncStack node, ICanonicalStateRootSource pivot, CancellationToken ct)
        {
            var snap = await SnapSyncOrchestrator.RunAsync(
                _bundle, node.Pool, node.Scheduler, pivot, _activations, _logger,
                ChainNodeSnapBootstrapOptionsBuilder.Build(_config.Node.Sync), ct).ConfigureAwait(false);

            _logger.LogInformation("DevP2P snap bootstrap: ran={Ran} reason={Reason}", snap.Ran, snap.SkipReason);
        }

        private async Task ForwardExecuteAsync(ChainNodeSyncStack node, CancellationToken ct)
        {
            var bounds = ChainNodeFollowerOptionsBuilder.Resolve(
                _config.Node.Sync, _bundle.Metadata.GetSnapSyncState(), _bundle.Metadata.GetLastBlock());
            var options = ChainNodeFollowerOptionsBuilder.BuildFollowerOptions(bounds, _config.Node.Sync);

            _logger.LogInformation(
                "DevP2P follower forward-executing from block {Start} (reason={Reason})",
                options.StartBlock, bounds.Reason);

            var receiptBackfillTask = LaunchReceiptBackfillScrub(node, ct);

            try
            {
                var result = await new FollowerService(_forkChoice, _mempoolReconciler).RunAsync(
                    node.BlockSource,
                    () => _bundle,
                    BuildExecutorFactory(),
                    StrictPolicy(_config, _loggerFactory),
                    node.Tip,
                    options,
                    ct,
                    _logger).ConfigureAwait(false);

                _logger.LogInformation("DevP2P follower exited: reason={Reason} lastBlock={Last}",
                    result.ExitReason, result.LastExecutedBlock);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("DevP2P follower stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DevP2P follower failed");
            }
            finally
            {
                _ = receiptBackfillTask;
            }
        }

        private Task LaunchReceiptBackfillScrub(ChainNodeSyncStack node, CancellationToken ct)
        {
            if (!_config.Node.Sync.ReceiptBackfill) return Task.CompletedTask;

            var backfill = new Nethereum.DevP2P.Sync.FullSync.ReceiptBackfillService(
                _bundle, node.Scheduler, logger: _logger);
            _logger.LogInformation(
                "DevP2P receipt-backfill scrub: enabled (cursor={Cursor})",
                _bundle.Metadata.GetReceiptBackfillCursor());
            return Task.Run(() => backfill.RunAsync(ct), ct);
        }


        private Func<IChainStoreBundle, IBlockExecutor> BuildExecutorFactory()
        {
            var activations = _activations;

            return bundle => Gated(bundle, new BlockImporter(
                new BlockExecutor(
                    bundle.State, bundle.Blocks, activations,
                    chainConfigFactory: fork =>
                    {
                        var forkChainConfig = new ChainConfig
                        {
                            ChainId = _config.ChainId,
                            BaseFee = BigInteger.Zero,
                            Coinbase = _config.Consensus.Sequencer.Address ?? "",
                            Hardfork = fork.ToString().ToLowerInvariant()
                        };
                        _config.Node.Rpc.ApplyTo(forkChainConfig);
                        return forkChainConfig;
                    },
                    hardforkConfigFactory: fork => DefaultMainnetHardforkRegistry.Instance.Get(fork),
                    stateRootCalculator: new IncrementalStateRootCalculator(
                        bundle.State, bundle.StateTrieNodes,
                        emitTombstones: !ReferenceEquals(bundle.StateTrieNodes, bundle.TrieNodes)),
                    rewardPolicy: NoRewardPolicy.Instance,
                    trieNodeStore: bundle.TrieNodes,
                    logger: _loggerFactory.CreateLogger<BlockExecutor>(),
                    authorResolver: _clique != null ? h => _clique.RecoverSigner(h) ?? h.Coinbase : null),
                bundle.Blocks, bundle.State, bundle.Transactions, bundle.Receipts, bundle.Logs,
                uncleStore: bundle.Uncles,
                logger: _loggerFactory.CreateLogger<BlockImporter>(),
                nodeCommitBlockContext: null, atomicFlush: null, flushCadence: null,
                blockAccessListStore: bundle.BlockAccessLists,
                withdrawalStore: bundle.Withdrawals));
        }

        private IBlockExecutor Gated(IChainStoreBundle bundle, IBlockExecutor inner) =>
            _clique == null
                ? inner
                : new ConsensusGatedBlockExecutor(
                    inner,
                    new Consensus.Clique.CliqueConsensusBlockGate(
                        _clique, bundle.Blocks,
                        _loggerFactory.CreateLogger<Consensus.Clique.CliqueConsensusBlockGate>()),
                    _loggerFactory.CreateLogger<ConsensusGatedBlockExecutor>());

        public static StrictValidationPolicy StrictPolicy(AppChainServerConfig config, ILoggerFactory loggerFactory) =>
            ChainNodeFollowerOptionsBuilder.BuildStrictValidationPolicy(
                config.Node.Sync, loggerFactory.CreateLogger<StrictValidationPolicy>());


    }
}
