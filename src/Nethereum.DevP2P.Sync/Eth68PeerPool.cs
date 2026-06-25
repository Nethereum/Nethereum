using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Tracks active eth/68 sessions for broadcast operations (NewBlock,
    /// NewBlockHashes, Transactions). Sessions register themselves on connect
    /// and unregister on disconnect. The pool is the shared registry that
    /// DevP2PBlockPublisher and similar broadcasters use to enumerate peers.
    /// </summary>
    public class Eth68PeerPool
    {
        private readonly ConcurrentDictionary<Guid, Eth68PeerSession> _peers = new();

        public int Count => _peers.Count;
        public IEnumerable<Eth68PeerSession> Peers => _peers.Values;

        public event EventHandler<Eth68PeerSession>? PeerJoined;
        public event EventHandler<Eth68PeerSession>? PeerLeft;

        public Eth68PeerSession Add(RlpxConnection connection, int ethOffset, Eth68StatusMessage remoteStatus)
        {
            var session = new Eth68PeerSession(connection, ethOffset, remoteStatus);
            _peers[session.Id] = session;
            PeerJoined?.Invoke(this, session);
            return session;
        }

        /// <summary>
        /// Overload used by the outbound dial path (Stage 1+ PeerPoolManager).
        /// Carries the post-handshake enode / host / negotiated eth version /
        /// peer-reported latest block / fork hash so the registered session
        /// satisfies <see cref="IEthPeer"/> in full.
        /// </summary>
        public Eth68PeerSession Add(
            RlpxConnection connection,
            int ethOffset,
            Eth68StatusMessage remoteStatus,
            string enode,
            string host,
            int ethVersion,
            ulong peerLatestBlock,
            uint peerForkHash)
        {
            var session = new Eth68PeerSession(connection, ethOffset, remoteStatus,
                enode, host, ethVersion, peerLatestBlock, peerForkHash);
            _peers[session.Id] = session;
            PeerJoined?.Invoke(this, session);
            return session;
        }

        public void Remove(Guid id)
        {
            if (_peers.TryRemove(id, out var session))
                PeerLeft?.Invoke(this, session);
        }

        /// <summary>
        /// Max time a single peer write may hold up the broadcast before the
        /// peer is considered uncooperative. NewBlock / NewBlockHashes / tx
        /// propagation can't wait on a half-dead socket — a peer with a hung
        /// TCP send wedges the whole broadcast under naive Task.WhenAll. Five
        /// seconds is comfortable on a healthy connection and aggressive
        /// enough to surface dead peers within one block-propagation window.
        /// </summary>
        public TimeSpan PerPeerBroadcastTimeout { get; set; } = TimeSpan.FromSeconds(5);

        public async Task BroadcastAsync(int msgId, byte[] payload, CancellationToken cancellationToken = default)
        {
            var peers = _peers.Values.ToList();
            var tasks = new List<Task>(peers.Count);
            foreach (var peer in peers)
            {
                tasks.Add(BroadcastOneAsync(peer, msgId, payload, cancellationToken));
            }
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // External cancellation — propagate cleanly without touching the pool.
                throw;
            }
        }

        private async Task BroadcastOneAsync(Eth68PeerSession peer, int msgId, byte[] payload, CancellationToken ct)
        {
            using var perPeerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (PerPeerBroadcastTimeout > TimeSpan.Zero)
                perPeerCts.CancelAfter(PerPeerBroadcastTimeout);
            try
            {
                await peer.Connection.SendMessageAsync(msgId, payload, perPeerCts.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // External cancellation — do NOT evict the peer.
            }
            catch (OperationCanceledException)
            {
                // Per-peer deadline expired — peer is stuck / half-dead. Evict
                // so it doesn't hold up the next broadcast either.
                Remove(peer.Id);
            }
            catch
            {
                Remove(peer.Id);
            }
        }
    }

    public class Eth68PeerSession : IEthPeer
    {
        public Guid Id { get; } = Guid.NewGuid();
        public RlpxConnection Connection { get; }
        public int EthOffset { get; }
        public Eth68StatusMessage RemoteStatus { get; }

        public string Enode { get; }
        public string Host { get; }
        public int EthVersion { get; }
        public ulong PeerLatestBlock { get; }
        public uint PeerForkHash { get; }

        public Eth68PeerSession(RlpxConnection connection, int ethOffset, Eth68StatusMessage remoteStatus)
            : this(connection, ethOffset, remoteStatus,
                   enode: string.Empty,
                   host: string.Empty,
                   ethVersion: 0,
                   peerLatestBlock: 0,
                   peerForkHash: remoteStatus?.ForkHash ?? 0)
        {
        }

        public Eth68PeerSession(
            RlpxConnection connection,
            int ethOffset,
            Eth68StatusMessage remoteStatus,
            string enode,
            string host,
            int ethVersion,
            ulong peerLatestBlock,
            uint peerForkHash)
        {
            Connection = connection;
            EthOffset = ethOffset;
            RemoteStatus = remoteStatus;
            Enode = enode ?? string.Empty;
            Host = host ?? string.Empty;
            EthVersion = ethVersion;
            PeerLatestBlock = peerLatestBlock;
            PeerForkHash = peerForkHash;

            if (connection != null)
                connection.Disconnected += OnConnectionDisconnected;
        }

        public event EventHandler<IEthPeer>? Disconnected;

        private void OnConnectionDisconnected(object? sender, EventArgs e)
            => Disconnected?.Invoke(this, this);

        /// <summary>Test-only hook: synthetically fire <see cref="Disconnected"/>
        /// without requiring a real socket close. Used by stub peer fixtures.</summary>
        public void TriggerDisconnectedForTest() => Disconnected?.Invoke(this, this);
    }
}
