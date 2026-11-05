using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.NodeDb;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.ChainNode.Hosting
{
    public sealed class ChainNodeSyncStack : IAsyncDisposable
    {
        private readonly PeerPoolManager _pool;
        private readonly ILogger _logger;

        private ChainNodeSyncStack(
            PeerPoolManager pool, IFetchRequestScheduler scheduler, IBlockSource blockSource,
            ICanonicalStateRootSource tip, ILogger logger)
        {
            _pool = pool;
            _logger = logger;
            Pool = pool;
            Scheduler = scheduler;
            BlockSource = blockSource;
            Tip = tip;
        }

        public PeerPoolManager Pool { get; }

        public IFetchRequestScheduler Scheduler { get; }

        public IBlockSource BlockSource { get; }

        public ICanonicalStateRootSource Tip { get; }

        public static async Task<ChainNodeSyncStack> StartAsync(
            IChainProfile profile,
            IChainStoreBundle bundle,
            ChainNodeConfig config,
            ILoggerFactory loggerFactory,
            Func<PeerPoolManager, ICanonicalStateRootSource> tipFactory = null,
            CancellationToken ct = default,
            Func<IChainStoreBundle, Task<ulong>>? minPeerLatestBlockFactory = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));

            config ??= new ChainNodeConfig();
            loggerFactory ??= NullLoggerFactory.Instance;

            ChainNodeEnodeValidator.RefuseMalformed(
                (profile.Bootnodes ?? (IReadOnlyList<string>)Array.Empty<string>())
                    .Select(bootnode => new KeyValuePair<string, string>("chain profile bootnodes", bootnode)));

            var dialTargets = DialTargets(profile, config);
            var minPeerLatestBlock = minPeerLatestBlockFactory != null
                ? await minPeerLatestBlockFactory(bundle).ConfigureAwait(false)
                : config.Sync.MinPeerLatestBlock;
            var peerCache = OpenPeerCacheOrNull(config, loggerFactory);
            var pool = await StartPoolAsync(
                profile, config, dialTargets, minPeerLatestBlock, peerCache, loggerFactory, ct).ConfigureAwait(false);
            var scheduler = BuildScheduler(pool, config, loggerFactory);

            return new ChainNodeSyncStack(
                pool,
                scheduler,
                BuildPullSource(pool, scheduler, bundle, config, loggerFactory),
                tipFactory?.Invoke(pool)
                    ?? new PeerHeadCanonicalSource(pool, trustedPeersOnly: config.Sync.TrustedPeersOnlyTip),
                loggerFactory.CreateLogger<ChainNodeSyncStack>());
        }

        private static PersistentPeerCache? OpenPeerCacheOrNull(ChainNodeConfig config, ILoggerFactory loggerFactory)
        {
            if (config.Storage.InMemory) return null;

            try
            {
                var logger = loggerFactory.CreateLogger<ChainNodeSyncStack>();
                var cache = config.OpenPeerCache(msg => logger.LogDebug("{Msg}", msg));
                cache.Load();
                return cache;
            }
            catch
            {
                return null;
            }
        }

        private static IReadOnlyList<string> DialTargets(IChainProfile profile, ChainNodeConfig config) =>
            config.ResolveDialEnodes()
                .Concat(profile.Bootnodes ?? (IReadOnlyList<string>)Array.Empty<string>())
                .Where(enode => !string.IsNullOrWhiteSpace(enode))
                .Distinct()
                .ToList();

        private static IReadOnlyList<string> TrustedDialTargets(ChainNodeConfig config) =>
            config.ResolveDialEnodes()
                .Concat(config.Network.TrustedBootnodes ?? Array.Empty<string>())
                .Where(enode => !string.IsNullOrWhiteSpace(enode))
                .Distinct()
                .ToList();

        private static async Task<PeerPoolManager> StartPoolAsync(
            IChainProfile profile, ChainNodeConfig config, IReadOnlyList<string> dialTargets,
            ulong minPeerLatestBlock, PersistentPeerCache? peerCache,
            ILoggerFactory loggerFactory, CancellationToken ct)
        {
            var pool = new PeerPoolManager(
                profile.CreateHandshakeWorker(loggerFactory, config.Sync.Snap.AdvertiseSnap2),
                new PeerPoolOptions(
                    TargetPeerCount: config.Sync.FloorTargetPeerCountByDialPool
                        ? Math.Max(config.Network.TargetPeerCount, dialTargets.Count)
                        : config.Network.TargetPeerCount,
                    MaxConcurrentDials: config.Network.MaxConcurrentDials,
                    MinPeerLatestBlock: minPeerLatestBlock,
                    DialBudgetPerSecond: config.Network.DialBudgetPerSecond,
                    MaxPeersPerIPv4Subnet: config.Network.MaxPeersPerIPv4Subnet,
                    MaxPeersPerIPv6Subnet: config.Network.MaxPeersPerIPv6Subnet),
                logger: loggerFactory.CreateLogger<PeerPoolManager>(),
                peerCache: peerCache,
                trustedDialKeys: TrustedDialTargets(config));

            await pool.StartAsync(ct).ConfigureAwait(false);
            foreach (var enode in dialTargets) pool.EnqueueCandidate(enode);

            return pool;
        }

        private static IFetchRequestScheduler BuildScheduler(
            PeerPoolManager pool, ChainNodeConfig config, ILoggerFactory loggerFactory) =>
            new FetchRequestScheduler(
                pool,
                new PeerRequestWorker(),
                new FetchRequestSchedulerOptions(MaxInFlightPerPeer: config.Sync.MaxInFlightPerPeer),
                pool.GetScore,
                loggerFactory.CreateLogger<FetchRequestScheduler>());

        private static DevP2PBlockSource BuildPullSource(
            PeerPoolManager pool, IFetchRequestScheduler scheduler, IChainStoreBundle bundle,
            ChainNodeConfig config, ILoggerFactory loggerFactory) =>
            new DevP2PBlockSource(
                pool, scheduler,
                number => ParentHashOfAsync(bundle, number),
                headerBatchSize: config.Sync.HeaderBatchSize,
                bodyBatchSize: config.Sync.BodyBatchSize,
                logger: loggerFactory.CreateLogger<DevP2PBlockSource>());

        private static async Task<byte[]?> ParentHashOfAsync(IChainStoreBundle bundle, ulong number) =>
            number == 0
                ? null
                : await bundle.Blocks.GetHashByNumberAsync((BigInteger)(number - 1)).ConfigureAwait(false);

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _pool.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Peer pool did not shut down cleanly");
            }
        }
    }
}
