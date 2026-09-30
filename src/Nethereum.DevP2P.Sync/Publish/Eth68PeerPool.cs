using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P.Sync.Publish
{
    public class Eth68PeerPool : IEthPeerRegistry
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

        public Guid Register(RlpxConnection connection, int ethOffset, Eth68StatusMessage remoteStatus)
            => Add(connection, ethOffset, remoteStatus).Id;

        public void Unregister(Guid id) => Remove(id);

        public void Remove(Guid id)
        {
            if (_peers.TryRemove(id, out var session))
                PeerLeft?.Invoke(this, session);
        }

        public TimeSpan PerPeerBroadcastTimeout { get; set; } = TimeSpan.FromSeconds(5);

        public async Task BroadcastAsync(
            int relativeMessageId, byte[] payload, Guid? exceptPeer = null,
            CancellationToken cancellationToken = default)
        {
            var peers = _peers.Values.ToList();
            var tasks = new List<Task>(peers.Count);
            foreach (var peer in peers)
            {
                if (exceptPeer.HasValue && peer.Id == exceptPeer.Value) continue;
                tasks.Add(BroadcastOneAsync(peer, relativeMessageId, payload, cancellationToken));
            }
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }

        public async Task SendToPeersAsync(
            IReadOnlyList<Eth68PeerSession> peers, int relativeMessageId, byte[] payload,
            CancellationToken cancellationToken = default)
        {
            var tasks = new List<Task>(peers.Count);
            foreach (var peer in peers)
                tasks.Add(BroadcastOneAsync(peer, relativeMessageId, payload, cancellationToken));
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }

        private async Task BroadcastOneAsync(
            Eth68PeerSession peer, int relativeMessageId, byte[] payload, CancellationToken ct)
        {
            var msgId = peer.EthOffset + relativeMessageId;

            using var perPeerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (PerPeerBroadcastTimeout > TimeSpan.Zero)
                perPeerCts.CancelAfter(PerPeerBroadcastTimeout);
            try
            {
                await peer.Connection.SendMessageAsync(msgId, payload, perPeerCts.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (OperationCanceledException)
            {
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

        public void TriggerDisconnectedForTest() => Disconnected?.Invoke(this, this);
    }
}
