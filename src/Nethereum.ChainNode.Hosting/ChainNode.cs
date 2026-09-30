using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Serving;

using Nethereum.DevP2P.Sync.Publish;

namespace Nethereum.ChainNode.Hosting
{
    public delegate ChainNodeStorage ChainNodeStorageFactory(ChainNodeConfig config, ILogger logger);

    public sealed class ChainNode : IAsyncDisposable
    {
        private readonly ChainNodeStorage _storage;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IChainDefinition _definition;

        private ChainNode(
            ChainNodeConfig config, IChainProfile profile, ChainNodeStorage storage,
            ChainNodeMempool mempool, ILoggerFactory loggerFactory, IChainDefinition definition)
        {
            Config = config;
            Profile = profile;
            _storage = storage;
            Mempool = mempool;
            _loggerFactory = loggerFactory;
            _definition = definition;
        }

        public ChainNodeConfig Config { get; }

        public IChainProfile Profile { get; }

        public ChainNodeStorage Storage => _storage;

        public IChainStoreBundle Bundle => _storage.Bundle;

        public ChainNodeMempool Mempool { get; }

        public PeerListener Listener { get; private set; }

        public ChainNodeSyncStack Sync { get; private set; }

        public bool DisposedCleanly { get; private set; }

        public static async Task<ChainNode> ComposeAsync(
            IChainDefinition definition,
            ChainNodeConfig config,
            ILoggerFactory loggerFactory,
            CancellationToken ct = default,
            ChainNodeStorageFactory storageFactory = null)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (config == null) throw new ArgumentNullException(nameof(config));

            loggerFactory ??= NullLoggerFactory.Instance;
            storageFactory ??= (cfg, logger) => ChainNodeStorage.Open(cfg, logger);

            var storage = storageFactory(config, loggerFactory.CreateLogger<ChainNode>());
            await definition.EnsureGenesisAsync(storage.Bundle, ct).ConfigureAwait(false);

            var profile = await definition.CreateProfileAsync(storage.Bundle).ConfigureAwait(false);
            var ethPeers = new Eth68PeerPool();
            var mempool = ChainNodeMempool.Create(profile, storage.Bundle, config, ethPeers, loggerFactory);

            return new ChainNode(config, profile, storage, mempool, loggerFactory, definition);
        }

        public async Task<PeerListener> StartServingAsync(
            ChainNodeServeCallbacks callbacks = null, CancellationToken ct = default)
        {
            if (!Config.Network.Serve || Listener != null) return Listener;

            Listener = await ChainNodeServeListener.StartAsync(
                Profile, Bundle, Config, Mempool, callbacks, _loggerFactory, ct).ConfigureAwait(false);

            return Listener;
        }

        public async Task StartSyncAsync(
            CancellationToken ct = default,
            Func<IChainStoreBundle, Task<ulong>> minPeerLatestBlockFactory = null)
        {
            if (Config.Sync.Mode == SyncMode.None || Sync != null) return;

            Sync = await ChainNodeSyncStack.StartAsync(
                Profile, Bundle, Config, _loggerFactory, _definition.CreateTip, ct, minPeerLatestBlockFactory).ConfigureAwait(false);

            Mempool.BridgeDialledPeers(Sync.Pool);
        }

        public static async Task<ChainNode> StartAsync(
            IChainDefinition definition,
            ChainNodeConfig config,
            ILoggerFactory loggerFactory,
            ChainNodeServeCallbacks callbacks = null,
            CancellationToken ct = default,
            ChainNodeStorageFactory storageFactory = null)
        {
            var node = await ComposeAsync(definition, config, loggerFactory, ct, storageFactory).ConfigureAwait(false);
            await node.StartServingAsync(callbacks, ct).ConfigureAwait(false);
            await node.StartSyncAsync(ct).ConfigureAwait(false);
            return node;
        }

        public async ValueTask DisposeAsync()
        {
            var clean = await DisposeStepAsync(() => { Mempool?.Dispose(); return default; }, "mempool").ConfigureAwait(false);
            if (Sync != null) clean &= await DisposeStepAsync(() => Sync.DisposeAsync(), "sync").ConfigureAwait(false);
            if (Listener != null) clean &= await DisposeStepAsync(() => Listener.DisposeAsync(), "listener").ConfigureAwait(false);
            if (Bundle is IAtomicBlockFlush flush)
                clean &= await DisposeStepAsync(() => new ValueTask(flush.DrainAsync()), "drain").ConfigureAwait(false);
            clean &= await DisposeStepAsync(() => _storage.DisposeAsync(), "storage").ConfigureAwait(false);

            DisposedCleanly = clean;
        }

        private async ValueTask<bool> DisposeStepAsync(Func<ValueTask> step, string name)
        {
            try
            {
                await step().ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                _loggerFactory.CreateLogger<ChainNode>().LogError(ex, "ChainNode dispose step {Step} failed", name);
                return false;
            }
        }
    }
}
