using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.DevP2P.Discv5;
using Nethereum.DevP2P.Dns;
using Nethereum.DevP2P.NodeDb;
using Nethereum.DevP2P.Sync;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.MainnetChain.Server.Configuration;
using Nethereum.Model.Enr;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Nethereum.Signer.Enr;

namespace Nethereum.MainnetChain.Server.Hosting
{
    public static class ProductionCompositionExtensions
    {
        public static IServiceCollection UseRocksDbAndDevP2PProductionComposition(
            this IServiceCollection services,
            MainnetChainServerConfig config)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrWhiteSpace(config.DataDir))
                throw new ArgumentException("DataDir is required for the production composition.", nameof(config));

            services.AddSingleton<ProductionRuntime>(sp =>
                new ProductionRuntime(
                    config,
                    sp.GetRequiredService<ILoggerFactory>()));

            services.AddSingleton<IChainStoreBundle>(sp =>
                sp.GetRequiredService<ProductionRuntime>().Bundle);

            services.AddSingleton<IBlockSource>(sp =>
                sp.GetRequiredService<ProductionRuntime>().BlockSource);

            services.AddHostedService(sp => sp.GetRequiredService<ProductionRuntime>());

            return services;
        }
    }

    internal sealed class ProductionRuntime : IHostedService, IAsyncDisposable
    {
        private readonly MainnetChainServerConfig _config;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        private RocksDbChainStoreBundle? _bundle;
        private PeerPoolManager? _pool;
        private FetchRequestScheduler? _scheduler;
        private DevP2PBlockSource? _source;
        private PeerListener? _peerListener;
        private EthECKey? _listenerKey;
        private Discv5Listener? _discv5;
        private Discv5PeerDiscoveryService? _discv5Discovery;
        private PersistentPeerCache? _peerCache;
        private CancellationTokenSource? _runtimeCts;

        public ProductionRuntime(MainnetChainServerConfig config, ILoggerFactory loggerFactory)
        {
            _config = config;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger("Nethereum.MainnetChain.ProductionRuntime");
            Init();
        }

        public IChainStoreBundle Bundle =>
            _bundle ?? throw new InvalidOperationException("ProductionRuntime not initialised.");

        public IBlockSource BlockSource =>
            _source ?? throw new InvalidOperationException("ProductionRuntime not initialised.");

        private void Init()
        {
            System.IO.Directory.CreateDirectory(_config.DataDir!);

            var journalOptions = BuildJournalOptions(_config.JournalBlocks);
            _bundle = RocksDbChainStoreBundle.Open(_config.DataDir!, journalOptions);
            _logger.LogInformation("Storage: RocksDB at {DataDir} (journal_blocks={Journal})",
                _config.DataDir, _config.JournalBlocks);

            EnsureGenesisLoadedAsync().GetAwaiter().GetResult();

            var dialPool = BuildDialPool(_config.TrustedPeer);

            var peerCachePath = System.IO.Path.Combine(_config.DataDir!, "peer-cache.json");
            _peerCache = new PersistentPeerCache(peerCachePath, msg => _logger.LogDebug("{Msg}", msg));
            _peerCache.Load();

            var resumeBlock = _bundle.Metadata.GetLastBlock();

            var trustedKeys = !string.IsNullOrWhiteSpace(_config.TrustedPeer)
                ? new[] { _config.TrustedPeer! }
                : Array.Empty<string>();

            _pool = new PeerPoolManager(
                new MainnetPeerHandshakeWorker(),
                new PeerPoolOptions(
                    TargetPeerCount: _config.TargetPeers,
                    MaxConcurrentDials: 10,
                    MinPeerLatestBlock: resumeBlock + 1),
                bootnodes: dialPool,
                logger: _loggerFactory.CreateLogger<PeerPoolManager>(),
                peerCache: _peerCache,
                trustedDialKeys: trustedKeys);

            _scheduler = new FetchRequestScheduler(
                _pool,
                new MainnetPeerRequestWorker(),
                new FetchRequestSchedulerOptions(),
                _pool.GetScore,
                _loggerFactory.CreateLogger<FetchRequestScheduler>());

            _source = new DevP2PBlockSource(
                _pool, _scheduler,
                parentHashLookup: async bn => bn == 0
                    ? null
                    : await _bundle.Blocks.GetHashByNumberAsync((BigInteger)(bn - 1)).ConfigureAwait(false),
                headerBatchSize: _config.HeadersBatch,
                bodyBatchSize: _config.BodiesBatch,
                logger: _loggerFactory.CreateLogger<DevP2PBlockSource>());
        }

        private async Task EnsureGenesisLoadedAsync()
        {
            if (_bundle!.Metadata.IsGenesisLoaded())
            {
                _logger.LogInformation("Genesis already loaded (skipping alloc).");
                return;
            }

            _logger.LogInformation("Loading mainnet genesis allocation...");
            var count = await MainnetGenesisLoader.PopulateAsync(_bundle.State).ConfigureAwait(false);
            _bundle.Metadata.MarkGenesisLoaded();
            _logger.LogInformation("Genesis loaded: {Accounts} accounts.", count);
        }

        private static string[] BuildDialPool(string? trustedPeer)
        {
            if (!string.IsNullOrWhiteSpace(trustedPeer))
            {
                var combined = new List<string> { trustedPeer };
                combined.AddRange(MainnetPeerSession.MainnetBootnodes);
                return combined.ToArray();
            }
            return MainnetPeerSession.MainnetBootnodes;
        }

        private static HistoricalStateOptions? BuildJournalOptions(int journalBlocks)
        {
            if (journalBlocks <= 0) return null;
            return new HistoricalStateOptions
            {
                MaxHistoryBlocks = journalBlocks,
                EnablePruning = true,
                PruningIntervalBlocks = Math.Max(64, journalBlocks / 16),
            };
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _runtimeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var ct = _runtimeCts.Token;

            await _pool!.StartAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("PeerPool started (target_peers={Target}).", _config.TargetPeers);

            _ = Task.Run(async () =>
            {
                try
                {
                    var dnsResolver = new EnrTreeResolver(msg => _logger.LogDebug("{Msg}", msg));
                    foreach (var tree in EnrTreeResolver.MainnetEnrTrees)
                    {
                        var enodes = await dnsResolver.ResolveAsync(
                            tree, TimeSpan.FromSeconds(30), 5000, ct).ConfigureAwait(false);
                        foreach (var enode in enodes)
                            _pool.EnqueueCandidate(enode);
                        _logger.LogInformation("DNS tree {Tree} resolved {Count} enodes.",
                            tree.Split('@').Length > 1 ? tree.Split('@')[1] : tree, enodes.Count);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "DNS seeding failed");
                }
            }, ct);

            if (_config.ListenPort >= 0)
            {
                _listenerKey = EthECKey.GenerateKey();
                var statusTemplate = BuildLocalStatusTemplate(_bundle!);
                var listenerOptions = new PeerListenerOptions
                {
                    ListenPort = _config.ListenPort,
                    BindAddress = IPAddress.Any,
                    MaxInboundPeers = 25,
                    MaxInboundPerIP = 9,
                    HandshakeTimeoutMs = 10000,
                    ServeSnap = true,
                    MirrorRemoteStatus = true,
                    ClientId = "Nethereum/mainnet-server",
                };
                var snapHandler = new PatriciaSnapRequestHandler(
                    _bundle!.TrieNodes,
                    new StateStoreBytecodeStore(_bundle!.State));
                _peerListener = new PeerListener(
                    _listenerKey, _bundle!, listenerOptions, statusTemplate,
                    snapHandler: snapHandler,
                    logger: _loggerFactory.CreateLogger<PeerListener>());
                await _peerListener.StartAsync(ct).ConfigureAwait(false);
                _logger.LogInformation(
                    "Inbound RLPx listener bound on 0.0.0.0:{Port} (NodeId=0x{NodeId}...)",
                    _peerListener.Port,
                    _listenerKey.GetPubKeyNoPrefix().ToHex().Substring(0, 16));
            }
            else
            {
                _logger.LogInformation("Inbound RLPx listener disabled (ListenPort < 0).");
            }

            if (!_config.DisableDiscv5)
            {
                var discv5Key = EthECKey.GenerateKey();
                _discv5 = new Discv5Listener(discv5Key);
                _discv5.Start(IPAddress.Any, port: _config.Discv5Port);

                var localEnr = new EnrRecord { Sequence = 1 };
                localEnr.Pairs["id"] = new byte[] { (byte)'v', (byte)'4' };
                localEnr.Pairs["ip"] = IPAddress.Any.GetAddressBytes();
                localEnr.Pairs["udp"] = new byte[] {
                    (byte)((_discv5.Port >> 8) & 0xff), (byte)(_discv5.Port & 0xff)
                };
                if (_peerListener != null && _peerListener.Port > 0)
                {
                    int tcpPort = _peerListener.Port;
                    localEnr.Pairs["tcp"] = new byte[] {
                        (byte)((tcpPort >> 8) & 0xff), (byte)(tcpPort & 0xff)
                    };
                }
                EnrRecordSigner.Sign(localEnr, discv5Key);
                _discv5.LocalEnrEncoded = EnrRecordEncoder.EncodeRecord(localEnr);
                _discv5.LocalEnrSequence = localEnr.Sequence;
                _logger.LogInformation(
                    "Discv5 listener bound on 0.0.0.0:{Port} (NodeId=0x{NodeId}...)",
                    _discv5.Port,
                    _discv5.NodeId.ToHex().Substring(0, 16));

                var bootnodes = new List<(EnrRecord, IPEndPoint)>(Discv5Bootnodes.ResolveMainnet());
                _discv5Discovery = new Discv5PeerDiscoveryService(
                    _discv5,
                    enode => _pool!.EnqueueCandidate(enode),
                    bootnodes,
                    msg => _logger.LogDebug("{Msg}", msg));
                await _discv5Discovery.StartAsync(ct).ConfigureAwait(false);
                _logger.LogInformation("Discv5 peer-discovery active ({Bootnodes} bootnodes).", bootnodes.Count);
            }
            else
            {
                _logger.LogInformation("Discv5 disabled.");
            }

            if (!string.IsNullOrWhiteSpace(_config.TrustedPeer))
            {
                _pool!.EnqueueCandidate(_config.TrustedPeer);
                _logger.LogInformation(
                    "Snap-sync trusted peer enqueued: {Peer}",
                    _config.TrustedPeer.Length > 64
                        ? _config.TrustedPeer.Substring(0, 64) + "..."
                        : _config.TrustedPeer);
            }
            else
            {
                _logger.LogInformation(
                    "No trusted peer configured; snap-sync handshake will go through bootnode dial pool ({Count} bootnodes).",
                    MainnetPeerSession.MainnetBootnodes.Length);
            }
        }

        private static Eth68StatusMessage BuildLocalStatusTemplate(IChainStoreBundle bundle)
        {
            var lastHash = bundle.Metadata.GetLastBlockHash();
            var bestHash = lastHash ?? MainnetGenesisConstants.BlockHashHex.HexToByteArray();
            var genesisHash = MainnetGenesisConstants.BlockHashHex.HexToByteArray();
            return new Eth68StatusMessage
            {
                ProtocolVersion = 68,
                NetworkId = MainnetGenesisConstants.ChainId,
                TotalDifficulty = BigInteger.Zero,
                BestHash = bestHash,
                GenesisHash = genesisHash,
                ForkHash = 0u,
                ForkNext = 0,
            };
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _runtimeCts?.Cancel();
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            try { _runtimeCts?.Cancel(); } catch { }

            if (_discv5Discovery != null)
            {
                try { await _discv5Discovery.DisposeAsync().ConfigureAwait(false); } catch { }
            }
            if (_discv5 != null)
            {
                try { await _discv5.DisposeAsync().ConfigureAwait(false); } catch { }
            }
            if (_peerListener != null)
            {
                try { await _peerListener.DisposeAsync().ConfigureAwait(false); } catch { }
            }
            if (_source != null)
            {
                try { await _source.DisposeAsync().ConfigureAwait(false); } catch { }
            }
            if (_pool != null)
            {
                try { await _pool.DisposeAsync().ConfigureAwait(false); } catch { }
            }
            if (_bundle != null)
            {
                try { await _bundle.DisposeAsync().ConfigureAwait(false); } catch { }
            }

            _runtimeCts?.Dispose();
        }
    }
}
