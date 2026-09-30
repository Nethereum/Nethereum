using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Mempool;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Publish;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.IntegrationTests.Harness
{
    public sealed class DevChainNode : IAsyncDisposable
    {
        public const int DefaultChainId = 1337;

        private static readonly byte[] DevGenesisHash = BuildDevGenesisHash();

        private readonly string _dataDir;
        private int _trustedPushes;
        private int _trustedAdmitted;
        private Exception _trustedAdmissionError;

        private readonly bool _inMemory;
        private readonly string[] _trustedNodeIds;
        private readonly int _listenPort;
        private readonly string[] _trustedPeers;
        private readonly bool _advertiseSnap2;
        private readonly int? _snapSoftResponseLimit;
        private readonly bool _pathKeyedState;
        private Nethereum.ChainNode.Hosting.ChainNode _composed;
        private PeerPoolManager _dialPool;
        private Eth68PeerPool _broadcastPool;

        private readonly List<byte[]> _observedAnnouncements = new();
        private readonly List<ISignedTransaction> _observedRemoteTransactions = new();
        private readonly object _observeLock = new();

        private CancellationTokenSource _lifetime;
        private int _started;

        public EthECKey Key { get; }

        public ulong ChainId { get; }

        public IChainStoreBundle Bundle { get; private set; }

        private async Task<ulong> CommittedHeight()
        {
            var height = await Bundle.Blocks.GetHeightAsync().ConfigureAwait(false);
            return (ulong)(System.Numerics.BigInteger)height;
        }

        public TxPool TxPool { get; private set; }

        public SyncNode Node { get; private set; }

        public PeerListener Serving { get; private set; }

        public RelayMempool Mempool => Node.Mempool;

        public Eth68PeerPool BroadcastPool => _broadcastPool;

        public PeerPoolManager DialPool => _dialPool;

        public IReadOnlyList<byte[]> ObservedAnnouncements
        {
            get { lock (_observeLock) return _observedAnnouncements.ToList(); }
        }

        public IReadOnlyList<ISignedTransaction> ObservedRemoteTransactions
        {
            get { lock (_observeLock) return _observedRemoteTransactions.ToList(); }
        }

        public bool ObservedTransaction(byte[] hash)
        {
            if (hash == null) return false;
            lock (_observeLock)
            {
                foreach (var announced in _observedAnnouncements)
                    if (announced != null && ByteUtil.AreEqual(announced, hash)) return true;
                foreach (var tx in _observedRemoteTransactions)
                    if (tx?.Hash != null && ByteUtil.AreEqual(tx.Hash, hash)) return true;
            }
            return false;
        }

        public string Enode => $"enode://{Key.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{Serving.Port}";

        private DevChainNode(
            int chainId, EthECKey key, string[] trustedNodeIds, bool inMemory = true,
            string dataDir = null, bool advertiseSnap2 = false, int? snapSoftResponseLimit = null,
            bool pathKeyedState = false, int listenPort = 0, string[] trustedPeers = null)
        {
            ChainId = (ulong)chainId;
            Key = key ?? EthECKey.GenerateKey();
            _dataDir = dataDir;
            _inMemory = inMemory;
            _trustedNodeIds = trustedNodeIds ?? Array.Empty<string>();
            _listenPort = listenPort;
            _trustedPeers = trustedPeers ?? Array.Empty<string>();
            _advertiseSnap2 = advertiseSnap2;
            _snapSoftResponseLimit = snapSoftResponseLimit;
            _pathKeyedState = pathKeyedState;
            PushedBlocks = new PushedBlockSource(committedHeight: CommittedHeight);
        }

        private Nethereum.ChainNode.Hosting.Configuration.ChainNodeConfig BuildConfig()
        {
            var config = new Nethereum.ChainNode.Hosting.Configuration.ChainNodeConfig();

            config.Storage.InMemory = _inMemory;
            config.Storage.DataDirectory = _dataDir ?? config.Storage.DataDirectory;
            config.Storage.PathKeyedState = _pathKeyedState;

            config.Network.Serve = true;
            config.Network.NodeKeyHex = Key.GetPrivateKey();
            config.Network.TrustedNodeIds = _trustedNodeIds;
            config.Network.ListenPort = _listenPort;
            config.Network.TrustedPeers = _trustedPeers;
            config.Network.BindAddress = IPAddress.Loopback;
            config.Network.MaxInboundPeers = 8;
            config.Network.MaxInboundPerIP = 8;
            config.Network.ClientId = "Nethereum.DevChainNode/1.0";
            config.Network.TargetPeerCount = 4;
            config.Network.DialBudgetPerSecond = 0;
            config.Network.MaxPeersPerIPv4Subnet = 0;
            config.Network.MaxPeersPerIPv6Subnet = 0;

            config.Sync.Snap.AdvertiseSnap2 = _advertiseSnap2;
            config.Sync.Snap.SoftResponseLimit = _snapSoftResponseLimit;

            return config;
        }

        public static DevChainNode Create(int chainId = DefaultChainId, bool advertiseSnap2 = false) =>
            new DevChainNode(chainId, key: null, trustedNodeIds: null, advertiseSnap2: advertiseSnap2);

        public static DevChainNode CreateWithRocksDb(
            int chainId = DefaultChainId, EthECKey key = null, string[] trustedNodeIds = null,
            bool advertiseSnap2 = false, int? snapSoftResponseLimit = null,
            bool pathKeyedState = false)
        {
            var dataDir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "devchain_rocks_" + Guid.NewGuid().ToString("N").Substring(0, 10));

            return new DevChainNode(
                chainId, key, trustedNodeIds, inMemory: false, dataDir: dataDir,
                advertiseSnap2: advertiseSnap2, snapSoftResponseLimit: snapSoftResponseLimit,
                pathKeyedState: pathKeyedState);
        }

        public static DevChainNode CreateClusterSibling(EthECKey key, string[] trustedNodeIds, int chainId = DefaultChainId) =>
            new DevChainNode(chainId, key, trustedNodeIds);

        public static DevChainNode CreateConfigured(
            EthECKey key, int listenPort, string[] trustedPeers, string[] trustedNodeIds = null,
            int chainId = DefaultChainId) =>
            new DevChainNode(
                chainId, key, trustedNodeIds, listenPort: listenPort, trustedPeers: trustedPeers);

        public static string NodeIdOf(EthECKey key) => key.GetPubKeyNoPrefix().ToHex();

        public static string EnodeOf(EthECKey key, int port) => $"enode://{NodeIdOf(key)}@127.0.0.1:{port}";

        public int TrustedPushCount => Volatile.Read(ref _trustedPushes);

        public int TrustedAdmittedCount => Volatile.Read(ref _trustedAdmitted);

        public Exception TrustedAdmissionError => Volatile.Read(ref _trustedAdmissionError);

        private void AdmitTrustedTransactions(TransactionsMessage msg)
        {
            if (msg?.Transactions == null) return;
            Interlocked.Increment(ref _trustedPushes);

            _ = Task.Run(async () =>
            {
                try
                {
                    var admitted = await Node.Mempool.AdmitFromTrustedPeerAsync(msg.Transactions).ConfigureAwait(false);
                    Interlocked.Add(ref _trustedAdmitted, admitted);
                }
                catch (Exception ex)
                {
                    Volatile.Write(ref _trustedAdmissionError, ex);
                }
            });
        }

        public async Task StartAsync(CancellationToken ct = default)
        {
            if (Interlocked.Exchange(ref _started, 1) == 1) return;
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);

            _composed = await Nethereum.ChainNode.Hosting.ChainNode.StartAsync(
                new DevChainDefinition(ChainId, Key),
                BuildConfig(),
                loggerFactory: null,
                callbacks: new Nethereum.ChainNode.Hosting.ChainNodeServeCallbacks
                {
                    TrustedTransactionsReceived = AdmitTrustedTransactions,
                    PooledTransactionHashesReceived = RecordAnnouncement,
                    TransactionsReceived = RecordRemoteTransactions,
                    NewBlockReceived = RecordRemoteBlock,
                },
                _lifetime.Token).ConfigureAwait(false);

            Bundle = _composed.Bundle;
            TxPool = (TxPool)_composed.Mempool.TxPool;
            Serving = _composed.Listener;
            _broadcastPool = _composed.Mempool.BroadcastPool;
            _dialPool = _composed.Sync.Pool;

            _dialPool.PeerAdded += OnDialPeerAdded;

            Node = new SyncNode(
                Bundle,
                new FixedChainActivations(HardforkNames.Parse("prague")),
                NullLogger.Instance,
                peers: _dialPool,
                serving: Serving,
                mempool: _composed.Mempool.Relay);
        }

        public async Task ConnectToAsync(DevChainNode other, CancellationToken ct = default)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            var targetEnode = other.Enode;
            _dialPool.EnqueueCandidate(targetEnode);

            var connected = await DevChainNetwork.WaitUntilAsync(
                () => OutboundSessionTo(other) != null,
                TimeSpan.FromSeconds(20), ct).ConfigureAwait(false);
            if (!connected)
                throw new TimeoutException($"DevChainNode {Enode} could not establish an outbound session to {targetEnode}");
        }

        public SyncPeerSession OutboundSessionTo(DevChainNode other)
        {
            var enode = other.Enode;
            return _dialPool.ActivePeers
                .OfType<SyncPeerSession>()
                .FirstOrDefault(p => string.Equals(p.PeerEnode, enode, StringComparison.OrdinalIgnoreCase));
        }

        public Task FundAccountAsync(string address, BigInteger balance, BigInteger nonce = default)
            => Bundle.State.SaveAccountAsync(
                address.ToLowerInvariant(),
                new Account { Balance = (EvmUInt256)balance, Nonce = (EvmUInt256)nonce });

        private readonly List<NewBlockMessage> _observedBlocks = new();

        public IReadOnlyList<NewBlockMessage> ObservedBlocks
        {
            get { lock (_observeLock) return _observedBlocks.ToList(); }
        }

        public PushedBlockSource PushedBlocks { get; } = new PushedBlockSource();

        private void RecordRemoteBlock(NewBlockMessage msg)
        {
            if (msg?.Header == null) return;
            lock (_observeLock) _observedBlocks.Add(msg);
            PushedBlocks.OnNewBlock(msg);
        }

        private void RecordAnnouncement(NewPooledTransactionHashesMessage msg)
        {
            if (msg?.Hashes == null) return;
            lock (_observeLock)
                foreach (var h in msg.Hashes)
                    if (h != null) _observedAnnouncements.Add(h);
        }

        private void RecordRemoteTransactions(TransactionsMessage msg)
        {
            if (msg?.Transactions == null) return;
            lock (_observeLock)
                foreach (var tx in msg.Transactions)
                    if (tx != null) _observedRemoteTransactions.Add(tx);
        }

        private void OnDialPeerAdded(object sender, IEthPeer peer)
        {
            if (peer is SyncPeerSession outbound)
                outbound.NewBlockReceived += (_, msg) => RecordRemoteBlock(msg);
        }

        public async ValueTask DisposeAsync()
        {
            if (_dialPool != null) _dialPool.PeerAdded -= OnDialPeerAdded;
            try { _lifetime?.Cancel(); } catch { }
            if (_composed != null)
                try { await _composed.DisposeAsync().ConfigureAwait(false); } catch { }
            if (_dataDir != null)
                try { System.IO.Directory.Delete(_dataDir, recursive: true); } catch { }
            try { _lifetime?.Dispose(); } catch { }
        }

        private static byte[] BuildDevGenesisHash()
        {
            using var probe = InMemoryChainStoreBundle.Open();
            return DevChainGenesis.EnsureAsync(probe, DefaultChainId).GetAwaiter().GetResult();
        }
    }
}
