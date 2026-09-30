using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.EVM;

namespace Nethereum.DevP2P.Sync.IntegrationTests.Harness
{
    public sealed class DevChainFollower : IAsyncDisposable
    {
        private readonly DevChainNode _node;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private Task _loop;

        private DevChainFollower(DevChainNode node) => _node = node;

        public static async Task<DevChainFollower> AttachAsync(DevChainNode node)
        {
            var producer = await DevChainProducer.AttachAsync(node);
            await producer.EnsureGenesisAsync();
            return new DevChainFollower(node);
        }

        public async Task<BigInteger> HeightAsync() => await _node.Bundle.Blocks.GetHeightAsync();

        public BigInteger ExecutedHeight => _node.Bundle.Metadata.GetLastBlock();

        public Task StartAsync(Microsoft.Extensions.Logging.ILogger logger = null) => StartAsync(_node.PushedBlocks, logger);

        public async Task<Nethereum.DevP2P.Sync.Snap.Bootstrap.SnapBootstrapper.Result> SnapBootstrapAsync(
            CancellationToken ct = default,
            Microsoft.Extensions.Logging.ILogger logger = null,
            ulong? pivotStaleDistanceBlocks = null,
            int? rootRefreshIntervalMs = null)
        {
            var scheduler = new Scheduling.FetchRequestScheduler(
                _node.DialPool,
                new Scheduling.PeerRequestWorker(),
                new Scheduling.FetchRequestSchedulerOptions());

            var activations = new FixedChainActivations(HardforkNames.Parse("prague"));
            var tip = new PeerHeadCanonicalSource(_node.DialPool);

            return await Snap.Bootstrap.SnapSyncOrchestrator.RunAsync(
                _node.Bundle, _node.DialPool, scheduler, tip, activations,
                logger ?? (Microsoft.Extensions.Logging.ILogger)Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
                new Snap.Bootstrap.SnapSyncOrchestratorOptions
                {
                    UseBackwardSkeleton = true,
                    RootRefreshIntervalMs = rootRefreshIntervalMs ?? 12_000,
                    PivotStaleDistanceBlocks = pivotStaleDistanceBlocks
                        ?? Snap.Bootstrap.SnapSyncOrchestrator.PivotStaleDistanceBlocks,
                },
                ct).ConfigureAwait(false);
        }

        public Task StartCatchUpAsync(Microsoft.Extensions.Logging.ILogger logger = null)
        {
            var scheduler = new Scheduling.FetchRequestScheduler(
                _node.DialPool,
                new Scheduling.PeerRequestWorker(),
                new Scheduling.FetchRequestSchedulerOptions());

            var pull = new FullSync.DevP2PBlockSource(
                _node.DialPool,
                scheduler,
                number => number == 0
                    ? Task.FromResult<byte[]>(null)
                    : _node.Bundle.Blocks.GetHashByNumberAsync((BigInteger)(number - 1)));

            return StartAsync(new FullSync.CatchUpThenFollowBlockSource(pull, _node.PushedBlocks), logger);
        }

        private Task StartAsync(IBlockSource source, Microsoft.Extensions.Logging.ILogger logger = null)
        {
            if (_loop != null) return Task.CompletedTask;

            var bundle = _node.Bundle;
            var activations = new FixedChainActivations(HardforkNames.Parse("prague"));
            var chainConfig = new ChainConfig
            {
                ChainId = (long)_node.ChainId,
                BaseFee = BigInteger.Zero,
                Coinbase = "0x0000000000000000000000000000000000000000",
                BlockGasLimit = DevChainGenesis.BlockGasLimit
            };
            var hardforkConfig = chainConfig.GetHardforkConfig();

            Func<IChainStoreBundle, IBlockExecutor> executorFactory = b =>
            {
                var calculator = new IncrementalStateRootCalculator(b.State, b.TrieNodes);
                var engine = new BlockExecutor(
                    b.State, b.Blocks, activations,
                    chainConfigFactory: _ => chainConfig,
                    hardforkConfigFactory: _ => hardforkConfig,
                    stateRootCalculator: calculator,
                    rewardPolicy: NoRewardPolicy.Instance,
                    trieNodeStore: b.TrieNodes,
                    logger: null);

                return new BlockImporter(
                    engine, b.Blocks, b.State, b.Transactions, b.Receipts, b.Logs,
                    uncleStore: b.Uncles, logger: null,
                    nodeCommitBlockContext: null, atomicFlush: null, flushCadence: null,
                    blockAccessListStore: b.BlockAccessLists);
            };

            var policy = new StrictValidationPolicy(continueOnMismatch: false, anchorEvery: 0);

            var resumeFrom = (ulong)(bundle.Metadata.GetLastBlock() + 1);
            var options = new FollowerOptions(StartBlock: resumeFrom, CheckpointEvery: 0, AnchorEvery: 0);
            var tip = new PeerHeadCanonicalSource(_node.DialPool);

            _loop = Task.Run(() => new FollowerService().RunAsync(
                source, () => bundle, executorFactory, policy, tip, options, _lifetime.Token, logger));

            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            _lifetime.Cancel();
            if (_loop != null)
            {
                try { await _loop.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                catch (Exception) { }
            }
            _lifetime.Dispose();
        }
    }
}
