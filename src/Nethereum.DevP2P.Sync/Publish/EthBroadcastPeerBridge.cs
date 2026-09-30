using System;
using System.Collections.Concurrent;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P.Sync.Publish
{
    public sealed class EthBroadcastPeerBridge : IDisposable
    {
        private readonly IPeerPool _peers;
        private readonly Eth68PeerPool _broadcastPool;
        private readonly ulong _networkId;
        private readonly byte[] _genesisHash;
        private readonly ConcurrentDictionary<Guid, Guid> _bridgedSessionByPeer = new();

        public EthBroadcastPeerBridge(IPeerPool peers, Eth68PeerPool broadcastPool, ulong networkId, byte[] genesisHash)
        {
            _peers = peers ?? throw new ArgumentNullException(nameof(peers));
            _broadcastPool = broadcastPool ?? throw new ArgumentNullException(nameof(broadcastPool));
            _networkId = networkId;
            _genesisHash = genesisHash ?? throw new ArgumentNullException(nameof(genesisHash));
            _peers.PeerAdded += OnPeerAdded;
            _peers.PeerRemoved += OnPeerRemoved;

            foreach (var peer in _peers.ActivePeers) OnPeerAdded(this, peer);
        }

        private void OnPeerAdded(object sender, IEthPeer peer)
        {
            try
            {
                var connection = peer.Connection;
                if (connection == null) return;

                var remoteStatus = new Eth68StatusMessage
                {
                    ProtocolVersion = peer.EthVersion,
                    NetworkId = _networkId,
                    GenesisHash = _genesisHash,
                    ForkHash = peer.PeerForkHash,
                };
                var session = _broadcastPool.Add(connection, connection.GetCapabilityOffset("eth"), remoteStatus);
                if (_bridgedSessionByPeer.TryGetValue(peer.Id, out var superseded) && superseded != session.Id)
                    _broadcastPool.Remove(superseded);
                _bridgedSessionByPeer[peer.Id] = session.Id;
            }
            catch
            {
            }
        }

        private void OnPeerRemoved(object sender, IEthPeer peer)
        {
            if (_bridgedSessionByPeer.TryRemove(peer.Id, out var sessionId))
                _broadcastPool.Remove(sessionId);
        }

        public void Dispose()
        {
            _peers.PeerAdded -= OnPeerAdded;
            _peers.PeerRemoved -= OnPeerRemoved;
        }
    }
}
