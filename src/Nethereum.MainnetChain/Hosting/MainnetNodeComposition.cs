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
using MainnetChainNode = Nethereum.ChainNode.Hosting.ChainNode;
using Nethereum.ChainNode.Hosting;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Discv5;
using Nethereum.DevP2P.Dns;
using Nethereum.DevP2P.NodeDb;
using Nethereum.DevP2P.Sync;
using Nethereum.Documentation;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.MainnetChain.Configuration;
using Nethereum.Model.Enr;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Nethereum.Signer.Enr;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.EVM.ForkId;
using Nethereum.DevP2P.Sync.Mempool;

namespace Nethereum.MainnetChain.Hosting
{
    public static class MainnetNodeComposition
    {
        [NethereumDocExample(DocSection.ChainInfrastructure, "mainnet-follower", "Build and register the RocksDB + DevP2P production node")]
        public static async Task<IServiceCollection> AddMainnetNodeAsync(
            this IServiceCollection services,
            MainnetChainServerConfig config,
            ILoggerFactory loggerFactory)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
            if (string.IsNullOrWhiteSpace(config.DataDir))
                throw new ArgumentException("DataDir is required for the production composition.", nameof(config));
            if (ChainNodeMaintenanceRunner.AnyRequested(config.ToChainNodeConfig().Maintenance))
                throw new InvalidOperationException(
                    "A maintenance flag (WipeState / CompactAll / RebuildStateFromFlat / VerifyFlat) is set. Maintenance is a " +
                    $"one-shot that runs and exits: call {nameof(RunMaintenanceAsync)} instead of composing the syncing node, " +
                    "then clear the flag before the next start.");

            var runtime = await MainnetNodeRuntime.CreateAsync(config, loggerFactory).ConfigureAwait(false);
            services.AddSingleton(runtime);

            services.AddSingleton<ChainNodeConfig>(sp =>
                sp.GetRequiredService<MainnetNodeRuntime>().ChainNodeConfig);

            services.AddSingleton<IChainStoreBundle>(sp =>
                sp.GetRequiredService<MainnetNodeRuntime>().Bundle);

            if (config.EngineOnly)
                services.AddSingleton<IBlockSource>(_ => new LocalReplayBlockSource(new List<BlockBundle>()));
            else
                AddPeerSyncServices(services, config);

            services.AddHostedService(sp => sp.GetRequiredService<MainnetNodeRuntime>());

            return services;
        }

        private static void AddPeerSyncServices(IServiceCollection services, MainnetChainServerConfig config)
        {
            services.AddSingleton<IBlockSource>(sp =>
                sp.GetRequiredService<MainnetNodeRuntime>().BlockSource);

            services.AddSingleton<IPeerPool>(sp =>
                sp.GetRequiredService<MainnetNodeRuntime>().Pool);

            services.AddSingleton<IFetchRequestScheduler>(sp =>
                sp.GetRequiredService<MainnetNodeRuntime>().Scheduler);

            if (config.EnableTxSubmission)
                services.AddSingleton<ITransactionSubmissionService>(sp =>
                    sp.GetRequiredService<MainnetNodeRuntime>().TransactionSubmission
                    ?? throw new InvalidOperationException(
                        "EnableTxSubmission is set but the node runtime built no transaction-submission service."));

            services.AddSingleton<BackwardBlockWalker>(sp =>
            {
                var runtime = sp.GetRequiredService<MainnetNodeRuntime>();
                return new BackwardBlockWalker(
                    runtime.Scheduler,
                    runtime.Bundle,
                    new BackwardBlockWalkerOptions(),
                    sp.GetRequiredService<ILoggerFactory>().CreateLogger<BackwardBlockWalker>());
            });

            services.AddSingleton<BackwardWalkerDelegate>(sp =>
            {
                var walker = sp.GetRequiredService<BackwardBlockWalker>();
                return async (fromBlock, fromHash, toBlock, bundle, ct, noShortCircuitAboveBlock) =>
                {
                    Func<ulong, CancellationToken, Task<(byte[]? hash, bool exists)>> lookup = async (bn, c) =>
                    {
                        c.ThrowIfCancellationRequested();
                        var header = await bundle.Blocks.GetByNumberAsync(bn).ConfigureAwait(false);
                        if (header == null) return ((byte[]?)null, false);
                        var hash = await bundle.Blocks.GetHashByNumberAsync(bn).ConfigureAwait(false);
                        return (hash, true);
                    };
                    var result = await walker.WalkAsync(fromBlock, fromHash, toBlock, lookup, ct, noShortCircuitAboveBlock).ConfigureAwait(false);
                    return new WalkerOutcome(
                        ExitReason: result.ExitReason,
                        HeadersWritten: result.HeadersWritten,
                        DivergenceBlock: result.DivergenceBlock,
                        SkeletonBottomBlock: result.SkeletonBottomBlock,
                        MetExistingStore: result.MetExistingStore);
                };
            });

            services.AddSingleton<HeaderFollowService>(sp =>
            {
                var runtime = sp.GetRequiredService<MainnetNodeRuntime>();
                var canonical = sp.GetService<ICanonicalStateRootSource>();
                if (canonical == null) return null;

                var headersOnlyWalker = new BackwardBlockWalker(
                    runtime.Scheduler,
                    runtime.Bundle,
                    new BackwardBlockWalkerOptions { HeadersOnly = true },
                    sp.GetRequiredService<ILoggerFactory>().CreateLogger<BackwardBlockWalker>());

                BackwardWalkerDelegate headersOnly = async (fromBlock, fromHash, toBlock, bundle, ct, noShortCircuitAboveBlock) =>
                {
                    Func<ulong, CancellationToken, Task<(byte[]? hash, bool exists)>> lookup = async (bn, c) =>
                    {
                        c.ThrowIfCancellationRequested();
                        var header = await bundle.Blocks.GetByNumberAsync(bn).ConfigureAwait(false);
                        if (header == null) return ((byte[]?)null, false);
                        var hash = await bundle.Blocks.GetHashByNumberAsync(bn).ConfigureAwait(false);
                        return (hash, true);
                    };
                    var result = await headersOnlyWalker.WalkAsync(fromBlock, fromHash, toBlock, lookup, ct, noShortCircuitAboveBlock).ConfigureAwait(false);
                    return new WalkerOutcome(
                        result.ExitReason, result.HeadersWritten, result.DivergenceBlock,
                        SkeletonBottomBlock: result.SkeletonBottomBlock,
                        MetExistingStore: result.MetExistingStore);
                };

                return new HeaderFollowService(
                    canonical,
                    headersOnly,
                    new HeaderFollowOptions(),
                    sp.GetRequiredService<ILoggerFactory>().CreateLogger<HeaderFollowService>(),
                    ancestorResolver: sp.GetService<AncestorResolverDelegate>());
            });

            services.AddSingleton<IAncestorResolver>(sp =>
            {
                var runtime = sp.GetRequiredService<MainnetNodeRuntime>();
                return new AncestorResolver(
                    runtime.Scheduler,
                    runtime.Bundle,
                    sp.GetRequiredService<ILoggerFactory>().CreateLogger<AncestorResolver>());
            });

            services.AddSingleton<AncestorResolverDelegate>(sp =>
            {
                var resolver = sp.GetRequiredService<IAncestorResolver>();
                return (divergedBlock, floorBlock, ct) =>
                    resolver.FindAsync(divergedBlock, floorBlock, ct);
            });

            services.AddSingleton<TipBandBodyFollowService>(sp =>
            {
                var runtime = sp.GetRequiredService<MainnetNodeRuntime>();
                return new TipBandBodyFollowService(
                    runtime.Scheduler,
                    runtime.Pool,
                    runtime.Bundle,
                    MainnetChainActivations.Instance,
                    sp.GetRequiredService<ILoggerFactory>().CreateLogger<MainnetChainHostedService>());
            });

            services.AddSingleton<BodyRepairDelegate>(sp =>
            {
                var tipBand = sp.GetRequiredService<TipBandBodyFollowService>();
                return (fromBlock, toBlock, bundle, ct) => tipBand.RequeueAndWaitAsync(fromBlock, ct);
            });
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "mainnet-follower", "Run a one-shot maintenance operation and report its exit code")]
        public static async Task<int> RunMaintenanceAsync(
            MainnetChainServerConfig config, ILogger logger, CancellationToken ct = default)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrWhiteSpace(config.DataDir))
                throw new ArgumentException("DataDir is required to run maintenance.", nameof(config));

            logger ??= NullLogger.Instance;
            var nodeConfig = config.ToChainNodeConfig();
            var verifyOnly = nodeConfig.Maintenance.VerifyFlat;
            var recoveryGate = new BootRecoveryGate(logger);
            try
            {
                if (verifyOnly && recoveryGate.HasPendingRestore(config.DataDir!))
                    return RefuseVerifyUntilBootRecovery(logger, "a checkpoint restore is pending");
                await using var storage = verifyOnly
                    ? ChainNodeStorage.Open(nodeConfig, logger, new TransactionVerificationAndRecoveryImp())
                    : await OpenAndRecoverAsync(config, nodeConfig, recoveryGate, logger).ConfigureAwait(false);
                var bundle = (RocksDbChainStoreBundle)storage.Bundle;
                if (verifyOnly && await CommittedHeadIsTornAsync(bundle).ConfigureAwait(false))
                    return RefuseVerifyUntilBootRecovery(logger, "the committed head's state root does not resolve in the trie (torn head)");

                return await ChainNodeMaintenanceRunner.RunAndReportExitCodeAsync(
                    bundle, nodeConfig.Maintenance, logger, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Maintenance: could not open or boot-recover the store at {DataDir}; exiting with a non-zero code.",
                    config.DataDir);
                return 1;
            }
        }

        internal static async Task<ChainNodeStorage> OpenAndRecoverAsync(
            MainnetChainServerConfig config, ChainNodeConfig nodeConfig, BootRecoveryGate recoveryGate, ILogger logger)
        {
            recoveryGate.ApplyPendingRestore(
                config.DataDir!,
                config.UseFreezerHistory ? config.FreezerHistoryDirectory : null,
                config.PromotionEnabled);

            var storage = ChainNodeStorage.Open(nodeConfig, logger, new TransactionVerificationAndRecoveryImp());
            try
            {
                recoveryGate.EnsureConsistentOrEscalate((RocksDbChainStoreBundle)storage.Bundle, config.DataDir!);
                return storage;
            }
            catch
            {
                await storage.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        private static int RefuseVerifyUntilBootRecovery(ILogger logger, string reason)
        {
            logger.LogError(
                "Maintenance: VerifyFlat refused — store needs boot recovery ({Reason}): start the node normally or run " +
                "--rebuild-state-from-flat. VerifyFlat is write-free and does not repair; nothing was written.",
                reason);
            return 1;
        }

        private static async Task<bool> CommittedHeadIsTornAsync(RocksDbChainStoreBundle bundle)
        {
            var head = bundle.Metadata.GetLastBlock();
            if (head == 0) return false;
            var header = await bundle.Blocks.GetByNumberAsync(head).ConfigureAwait(false);
            return header?.StateRoot == null || !bundle.StateTrieNodes.ContainsKey(header.StateRoot);
        }

        public static RocksDbStorageOptions BuildStorageOptions(MainnetChainServerConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            var options = ChainNodeStorage.BuildStorageOptions(MainnetChainNodeConfigFactory.MapStorage(config));
            options.DatabasePath = new RocksDbStorageOptions().DatabasePath;
            return options;
        }

        public static void AlignByHashReindexCursorToFreezerHead(MainnetChainServerConfig config, ILogger logger)
        {
            var storageOptions = BuildStorageOptions(config);
            storageOptions.SkipBootReconcile = true;
            var journalOptions = ChainNodeStorage.BuildJournalOptions(config.JournalBlocks);
            using var bundle = RocksDbChainStoreBundle.Open(
                config.DataDir!, journalOptions, bulkSync: config.BulkSync, storageOptions: storageOptions,
                signer: new TransactionVerificationAndRecoveryImp());
            var (previous, updated) = bundle.AlignByHashReindexCursorToFreezerHead();
            if (updated == previous)
                logger.LogWarning("align-byhash-cursor: cursor already at {Cursor} (freezer off or already caught up) — no change.", previous);
            else
                logger.LogWarning(
                    "align-byhash-cursor: by-hash reindex cursor {Previous} -> {Head} (freezer head). " +
                    "The background trailer will start caught up and re-scan nothing.", previous, updated);
        }

        public static IValidationPolicy BuildValidationPolicy(
            MainnetChainServerConfig config, ILogger<StrictValidationPolicy>? logger)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            return new StrictValidationPolicy(config.ContinueOnMismatch, anchorEvery: 0, logger: logger);
        }

        public static IFlushCadence? BuildFlushCadence(MainnetChainServerConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            return FlushCadences.ForBlocks(
                MainnetChainNodeConfigFactory.MapStorage(config).FlushCadenceBlocks, nameof(config));
        }
    }

    internal sealed class MainnetNodeRuntime : IHostedService, IAsyncDisposable
    {
        private readonly MainnetChainServerConfig _config;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        private MainnetChainNode? _node;
        private ChainNodeConfig? _chainNodeConfig;
        private ITransactionSubmissionService? _txSubmission;
        private EthECKey? _nodeKey;
        private Discv5Listener? _discv5;
        private Discv5PeerDiscoveryService? _discv5Discovery;
        private Nethereum.DevP2P.Discv4.PeerDiscoveryService? _discv4;
        private CancellationTokenSource? _runtimeCts;
        private readonly BootRecoveryGate _recoveryGate;

        private MainnetNodeRuntime(MainnetChainServerConfig config, ILoggerFactory loggerFactory)
        {
            _config = config;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger("Nethereum.MainnetChain.MainnetNodeRuntime");
            _recoveryGate = new BootRecoveryGate(_logger);
        }

        public static async Task<MainnetNodeRuntime> CreateAsync(
            MainnetChainServerConfig config, ILoggerFactory loggerFactory, CancellationToken ct = default)
        {
            var runtime = new MainnetNodeRuntime(config, loggerFactory);
            await runtime.InitAsync(ct).ConfigureAwait(false);
            return runtime;
        }

        public IChainStoreBundle Bundle =>
            _node?.Bundle ?? throw new InvalidOperationException("MainnetNodeRuntime not initialised.");

        public IBlockSource BlockSource =>
            _node?.Sync?.BlockSource ?? throw new InvalidOperationException("MainnetNodeRuntime not started.");

        public IPeerPool Pool =>
            _node?.Sync?.Pool ?? throw new InvalidOperationException("MainnetNodeRuntime not started.");

        public IFetchRequestScheduler Scheduler =>
            _node?.Sync?.Scheduler ?? throw new InvalidOperationException("MainnetNodeRuntime not started.");

        public ITransactionSubmissionService? TransactionSubmission => _txSubmission;

        internal ChainNodeConfig ChainNodeConfig =>
            _chainNodeConfig ?? throw new InvalidOperationException("MainnetNodeRuntime not initialised.");

        private async Task InitAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _chainNodeConfig = _config.ToChainNodeConfig();
            ChainNodeEnodeValidator.RefuseMalformed(_chainNodeConfig.Network.TrustedPeers
                .Select(enode => new KeyValuePair<string, string>("MainnetChain:TrustedPeer", enode)));

            Nethereum.CoreChain.RocksDB.Stores.RocksDbCheckpointManager.CompleteInterruptedRestore(_config.DataDir!);
            System.IO.Directory.CreateDirectory(_config.DataDir!);
            _recoveryGate.ApplyPendingRestore(
                _config.DataDir!,
                _config.UseFreezerHistory ? _config.FreezerHistoryDirectory : null,
                _config.PromotionEnabled);

            _nodeKey = _chainNodeConfig.ResolveNodeKey(msg => _logger.LogInformation("{Msg}", msg));

            _runtimeCts = new CancellationTokenSource();
            _node = await MainnetChainNode.ComposeAsync(
                new MainnetChainDefinition(_nodeKey, genesis: _config.LoadGenesis()),
                _chainNodeConfig,
                _loggerFactory,
                _runtimeCts.Token,
                storageFactory: (cfg, logger) => ChainNodeStorage.Open(cfg, logger, new TransactionVerificationAndRecoveryImp()))
                .ConfigureAwait(false);
            _logger.LogInformation(
                "Storage: RocksDB at {DataDir} (journal_blocks={Journal}, bulk_sync={Bulk}, path_keyed={PathKeyed}, trie_node_history_blocks={NodeHistory}, split_history_store={Split})",
                _config.DataDir, _config.JournalBlocks, _config.BulkSync,
                _config.PathKeyedState, _config.TrieNodeHistoryBlocks, _config.SplitHistoryStore);

            _recoveryGate.EnsureConsistentOrEscalate((RocksDbChainStoreBundle)_node.Bundle, _config.DataDir!);

            if (!_config.EngineOnly)
            {
                await _node.StartSyncAsync(
                    _runtimeCts.Token,
                    minPeerLatestBlockFactory: b => Task.FromResult(b.Metadata.GetLastBlock() + 1))
                    .ConfigureAwait(false);
                _logger.LogInformation("PeerPool started (target_peers={Target}).", _config.TargetPeers);
                LogTrustedPeers();
            }

            if (_config.EnableTxSubmission) WireTransactionSubmission();
        }

        private void WireTransactionSubmission()
        {
            _txSubmission = new MempoolTransactionSubmissionService(_node!.Mempool.Relay);
            _logger.LogInformation(
                "Transaction submission ENABLED — eth_sendRawTransaction admits to the local mempool, serves GetPooledTransactions, and announces to peers.");
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_config.EngineOnly) return;

            var ct = _runtimeCts!.Token;
            cancellationToken.Register(() =>
            {
                try { _runtimeCts?.Cancel(); }
                catch (ObjectDisposedException) { }
            });

            LaunchDnsSeeding(ct);
            await StartInboundServingAsync(ct).ConfigureAwait(false);
            await StartDiscv5DiscoveryAsync(ct).ConfigureAwait(false);
            StartDiscv4Discovery(ct);
        }

        private void StartDiscv4Discovery(CancellationToken ct)
        {
            if (_chainNodeConfig!.Network.Discovery.DisableDiscv4) return;

            _discv4 = new Nethereum.DevP2P.Discv4.PeerDiscoveryService(msg => _logger.LogDebug("{Msg}", msg));
            _discv4.Start(_chainNodeConfig.Network.Discovery.Discv4Port);
            _logger.LogInformation("Discv4 peer-discovery active (seeds={Seeds}).",
                Nethereum.DevP2P.Sync.Peering.SyncPeerSession.MainnetBootnodes.Length);

            _ = DiscoveryLoop.RunAsync(
                harvest: async token => await _discv4.DiscoverAsync(
                    Nethereum.DevP2P.Sync.Peering.SyncPeerSession.MainnetBootnodes,
                    Discv4PerSeedTimeout, token).ConfigureAwait(false),
                enqueue: enode => _node!.Sync.Pool.EnqueueCandidate(enode),
                interval: Discv4DiscoveryInterval,
                onError: ex => _logger.LogDebug(ex, "discv4 discovery round failed; continuing."),
                ct: ct);
        }

        private static readonly TimeSpan Discv4PerSeedTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan Discv4DiscoveryInterval = TimeSpan.FromSeconds(30);

        private void LaunchDnsSeeding(CancellationToken ct)
        {
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
                            _node!.Sync.Pool.EnqueueCandidate(enode);
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
        }

        private async Task StartInboundServingAsync(CancellationToken ct)
        {
            if (_config.ListenPort >= 0)
            {
                var listener = await _node!.StartServingAsync(callbacks: null, ct).ConfigureAwait(false);
                _logger.LogInformation(
                    "Inbound RLPx listener bound on 0.0.0.0:{Port} (NodeId=0x{NodeId}...)",
                    listener.Port,
                    _nodeKey!.GetPubKeyNoPrefix().ToHex().Substring(0, 16));
            }
            else
            {
                _logger.LogInformation("Inbound RLPx listener disabled (ListenPort < 0).");
            }
        }

        private async Task StartDiscv5DiscoveryAsync(CancellationToken ct)
        {
            if (!_chainNodeConfig!.Network.Discovery.DisableDiscv5)
            {
                var discv5Key = EthECKey.GenerateKey();
                _discv5 = new Discv5Listener(discv5Key);
                _discv5.Start(IPAddress.Any, port: _chainNodeConfig.Network.Discovery.Discv5Port);

                var localEnr = new EnrRecord { Sequence = 1 };
                localEnr.Pairs["id"] = new byte[] { (byte)'v', (byte)'4' };
                localEnr.Pairs["ip"] = IPAddress.Any.GetAddressBytes();
                localEnr.Pairs["udp"] = new byte[] {
                    (byte)((_discv5.Port >> 8) & 0xff), (byte)(_discv5.Port & 0xff)
                };
                if (_node!.Listener != null && _node.Listener.Port > 0)
                {
                    int tcpPort = _node.Listener.Port;
                    localEnr.Pairs["tcp"] = new byte[] {
                        (byte)((tcpPort >> 8) & 0xff), (byte)(tcpPort & 0xff)
                    };
                }
                var enrGenesisHash = MainnetGenesisConstants.BlockHashHex.HexToByteArray();
                var (enrHeadBlock, enrHeadTime) = await ChainHeadResolver.ResolveOurHeadAsync(_node.Bundle).ConfigureAwait(false);
                var enrForkId = Eip2124ForkIdCalculator.NewId(
                    enrGenesisHash, MainnetChainSchedule.ForkIdentity.BlockHeights, MainnetChainSchedule.ForkIdentity.Timestamps,
                    enrHeadBlock, enrHeadTime);
                localEnr.SetListValue("eth", EnrForkIdEntry.Encode(enrForkId.Hash, enrForkId.Next));

                EnrRecordSigner.Sign(localEnr, discv5Key);
                _discv5.LocalEnrEncoded = EnrRecordEncoder.EncodeRecord(localEnr);
                _discv5.LocalEnrSequence = localEnr.Sequence;
                _logger.LogInformation(
                    "Discv5 listener bound on 0.0.0.0:{Port} (NodeId=0x{NodeId}...)",
                    _discv5.Port,
                    _discv5.NodeId.ToHex().Substring(0, 16));

                Func<byte[], bool> ethForkIdFilter = ethBytes =>
                {
                    try
                    {
                        EnrForkIdEntry.Decode(ethBytes, out var remoteHash, out var remoteNext);
                        var (headBlock, headTime) = ChainHeadResolver.ResolveOurHeadAsync(_node!.Bundle).GetAwaiter().GetResult();
                        return Eip2124ForkIdCalculator.ValidateForkId(
                            remoteHash, remoteNext, enrGenesisHash,
                            MainnetChainSchedule.ForkIdentity.BlockHeights, MainnetChainSchedule.ForkIdentity.Timestamps,
                            headBlock, headTime) == Eip2124ValidationResult.Accepted;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "eth fork-id discovery filter threw; admitting candidate (handshake remains authoritative)");
                        return true;
                    }
                };

                var bootnodes = new List<(EnrRecord, IPEndPoint)>(Discv5Bootnodes.ResolveMainnet());
                _discv5Discovery = new Discv5PeerDiscoveryService(
                    _discv5,
                    enode => _node!.Sync.Pool.EnqueueCandidate(enode),
                    bootnodes,
                    msg => _logger.LogDebug("{Msg}", msg),
                    ethForkIdFilter: ethForkIdFilter);
                await _discv5Discovery.StartAsync(ct).ConfigureAwait(false);
                _logger.LogInformation("Discv5 peer-discovery active ({Bootnodes} bootnodes).", bootnodes.Count);
            }
            else
            {
                _logger.LogInformation("Discv5 disabled.");
            }
        }

        private void LogTrustedPeers()
        {
            var trustedPeers = _chainNodeConfig!.Network.TrustedPeers ?? Array.Empty<string>();
            if (trustedPeers.Length > 0)
            {
                _logger.LogInformation(
                    "Snap-sync trusted peers enqueued: {Count} ({First}...)",
                    trustedPeers.Length,
                    trustedPeers[0].Length > 64 ? trustedPeers[0].Substring(0, 64) : trustedPeers[0]);
            }
            else
            {
                _logger.LogInformation(
                    "No trusted peer configured; snap-sync handshake will go through bootnode dial pool ({Count} bootnodes).",
                    SyncPeerSession.MainnetBootnodes.Length);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _runtimeCts?.Cancel();
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            try { _runtimeCts?.Cancel(); }
            catch (Exception ex) { _logger.LogDebug(ex, "Cancelling the runtime token on shutdown threw; continuing dispose."); }

            if (_discv5Discovery != null)
            {
                try { await _discv5Discovery.DisposeAsync().ConfigureAwait(false); }
                catch (Exception ex) { _logger.LogDebug(ex, "discv5 discovery dispose failed on shutdown; continuing."); }
            }
            if (_discv5 != null)
            {
                try { await _discv5.DisposeAsync().ConfigureAwait(false); }
                catch (Exception ex) { _logger.LogDebug(ex, "discv5 dispose failed on shutdown; continuing."); }
            }
            if (_discv4 != null)
            {
                try { _discv4.Dispose(); }
                catch (Exception ex) { _logger.LogDebug(ex, "discv4 dispose failed on shutdown; continuing."); }
            }
            if (_node?.Sync?.BlockSource is IAsyncDisposable disposableSource)
            {
                try { await disposableSource.DisposeAsync().ConfigureAwait(false); }
                catch (Exception ex) { _logger.LogDebug(ex, "Canonical source dispose failed on shutdown; continuing."); }
            }

            if (_node != null)
            {
                await _node.DisposeAsync().ConfigureAwait(false);

                if (_node.DisposedCleanly)
                    _recoveryGate.MarkCleanShutdown(_config.DataDir!);
                else
                    _logger.LogError(
                        "ChainNode dispose did not complete cleanly on shutdown; NOT stamping the clean-shutdown " +
                        "marker so the next boot's integrity gate verifies the head.");
            }

            _runtimeCts?.Dispose();
        }
    }
}
