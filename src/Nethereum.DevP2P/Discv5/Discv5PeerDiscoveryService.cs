using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.Enr;
using Nethereum.Signer;

namespace Nethereum.DevP2P.Discv5
{
    /// <summary>
    /// Live discv5 peer discovery orchestrator. Bonds with seed bootnode ENRs
    /// (PING / PONG over the discv5 handshake), then walks the routing table
    /// with FINDNODE at random log-distance buckets and harvests every ENR
    /// returned. Discovered peers are surfaced as enode URLs via the
    /// <c>enqueueEnode</c> callback so a peer pool dial loop can act on them.
    /// <para>
    /// Mirrors the role <see cref="Nethereum.DevP2P.Discv4.PeerDiscoveryService"/>
    /// plays for discv4 — composes the existing initiator surface on
    /// <see cref="Discv5Listener"/> (<see cref="Discv5Listener.SendPingAsync"/>
    /// and <see cref="Discv5Listener.SendFindNodeAsync"/>) rather than reaching
    /// into the codec.
    /// </para>
    /// </summary>
    public sealed class Discv5PeerDiscoveryService : IAsyncDisposable, IDisposable
    {
        /// <summary>Default cadence for the routing-table walk loop.</summary>
        public static readonly TimeSpan DefaultWalkInterval = TimeSpan.FromSeconds(30);

        /// <summary>Per-request timeout for Ping / FindNode round-trips.</summary>
        public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Maximum peers to query per walk iteration. Drawn from the routing
        /// table by random-target XOR ordering so successive walks bias toward
        /// different slices of the table.
        /// </summary>
        public const int PeersPerWalk = 4;

        /// <summary>
        /// Distance buckets we request from each peer per walk iteration.
        /// Random node-id distance is overwhelmingly clustered in the top
        /// few buckets (256, 255, 254 cover ~99% of the address space) so
        /// these three are enough for a harvest walk.
        /// </summary>
        public static readonly uint[] WalkDistances = new uint[] { 256, 255, 254 };

        private readonly Discv5Listener _listener;
        private readonly Action<string> _enqueueEnode;
        private readonly IReadOnlyList<(EnrRecord Enr, IPEndPoint Endpoint)> _bootnodes;
        private readonly Action<string> _log;
        private readonly TimeSpan _walkInterval;
        private CancellationTokenSource _cts;
        private Task _walkLoop;
        private int _disposed;

        public Discv5PeerDiscoveryService(
            Discv5Listener listener,
            Action<string> enqueueEnode,
            IList<(EnrRecord Enr, IPEndPoint Endpoint)> bootnodes,
            Action<string> log = null)
            : this(listener, enqueueEnode, bootnodes, log, DefaultWalkInterval) { }

        public Discv5PeerDiscoveryService(
            Discv5Listener listener,
            Action<string> enqueueEnode,
            IList<(EnrRecord Enr, IPEndPoint Endpoint)> bootnodes,
            Action<string> log,
            TimeSpan walkInterval)
        {
            _listener = listener ?? throw new ArgumentNullException(nameof(listener));
            _enqueueEnode = enqueueEnode ?? throw new ArgumentNullException(nameof(enqueueEnode));
            _bootnodes = (bootnodes ?? new List<(EnrRecord, IPEndPoint)>()).ToList();
            _log = log ?? (_ => { });
            _walkInterval = walkInterval > TimeSpan.Zero ? walkInterval : DefaultWalkInterval;
        }

        /// <summary>
        /// Start the bootnode bond pass plus the routing-table walk loop.
        /// Returns once the bootnode pass has been scheduled; the walk loop
        /// continues in the background until <paramref name="ct"/> is
        /// cancelled or <see cref="DisposeAsync"/> is called.
        /// </summary>
        public Task StartAsync(CancellationToken ct)
        {
            if (Interlocked.CompareExchange(ref _disposed, 0, 0) != 0)
                throw new ObjectDisposedException(nameof(Discv5PeerDiscoveryService));
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _ = Task.Run(() => BondBootnodesAsync(_cts.Token));
            _walkLoop = Task.Run(() => WalkLoopAsync(_cts.Token));
            return Task.CompletedTask;
        }

        /// <summary>Cancel the walk loop and wait for it to drain.</summary>
        public async Task StopAsync()
        {
            try { _cts?.Cancel(); }
            catch (ObjectDisposedException) { /* already disposed */ }
            if (_walkLoop != null)
            {
                try { await _walkLoop.ConfigureAwait(false); }
                catch (OperationCanceledException) { /* expected */ }
                catch (Exception) { /* swallow on teardown */ }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            await StopAsync().ConfigureAwait(false);
            _cts?.Dispose();
        }

        public void Dispose()
        {
            try { DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            catch (Exception) { /* swallow on teardown */ }
        }

        private async Task BondBootnodesAsync(CancellationToken ct)
        {
            foreach (var (enr, endpoint) in _bootnodes)
            {
                if (ct.IsCancellationRequested) return;
                try
                {
                    await PingAndHarvestAsync(enr, endpoint, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                catch (Exception ex)
                {
                    _log($"discv5 bond to {endpoint}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        private async Task PingAndHarvestAsync(EnrRecord enr, IPEndPoint endpoint, CancellationToken ct)
        {
            if (enr?.Secp256k1 == null || enr.Secp256k1.Length != 33) return;
            var peerNodeId = Discv5Crypto.ComputeNodeId(enr.Secp256k1);

            // PING establishes a session and proves we can reach the peer.
            try
            {
                await _listener.SendPingAsync(endpoint, peerNodeId, enr.Secp256k1, RequestTimeout, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _log($"discv5 ping to {endpoint}: {ex.GetType().Name}: {ex.Message}");
                return;
            }

            // The bond itself surfaces the bootnode as a dialable enode.
            TryEnqueueEnrAsEnode(enr);

            // Harvest its routing-table slice via FINDNODE(distances).
            await FindNodeAndHarvestAsync(peerNodeId, endpoint, enr.Secp256k1, ct)
                .ConfigureAwait(false);
        }

        private async Task FindNodeAndHarvestAsync(
            byte[] peerNodeId, IPEndPoint endpoint, byte[] peerStaticPubKey, CancellationToken ct)
        {
            try
            {
                var enrs = await _listener.SendFindNodeAsync(
                    endpoint, peerNodeId, peerStaticPubKey, WalkDistances, RequestTimeout, ct)
                    .ConfigureAwait(false);
                foreach (var enr in enrs)
                {
                    TryEnqueueEnrAsEnode(enr);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _log($"discv5 findnode to {endpoint}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private async Task WalkLoopAsync(CancellationToken ct)
        {
            // Stagger the first walk slightly so the bootnode-bond pass has time
            // to populate the routing table before we ask for peers.
            try { await Task.Delay(_walkInterval, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }

            while (!ct.IsCancellationRequested)
            {
                try { await WalkOnceAsync(ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
                catch (Exception ex) { _log($"discv5 walk: {ex.GetType().Name}: {ex.Message}"); }

                try { await Task.Delay(_walkInterval, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }

        private async Task WalkOnceAsync(CancellationToken ct)
        {
            // Pick PeersPerWalk peers biased toward a random XOR target so
            // successive walks reach into different slices of the table.
            var target = new byte[32];
            RandomNumberGenerator.Fill(target);
            var peers = _listener.Routing.Nearest(target, PeersPerWalk);
            if (peers == null || peers.Count == 0) return;

            foreach (var peer in peers)
            {
                if (ct.IsCancellationRequested) return;
                if (peer?.EnrEncoded == null) continue;
                EnrRecord enr;
                try { enr = EnrRecordEncoder.Decode(peer.EnrEncoded); }
                catch (Exception) { continue; }
                if (enr?.Secp256k1 == null || enr.Secp256k1.Length != 33) continue;
                if (peer.Address == null) continue;

                await FindNodeAndHarvestAsync(peer.NodeId, peer.Address, enr.Secp256k1, ct)
                    .ConfigureAwait(false);
            }
        }

        private void TryEnqueueEnrAsEnode(EnrRecord enr)
        {
            var enode = ConvertEnrToEnode(enr);
            if (enode == null) return;
            try { _enqueueEnode(enode); }
            catch (Exception) { /* don't let the consumer crash discovery */ }
        }

        /// <summary>
        /// Convert a v4-identity-scheme ENR to a dialable enode URL
        /// (<c>enode://uncompressed-pubkey@ip:tcp</c>). Returns null if the ENR
        /// lacks any of the fields required to dial (secp256k1, IPv4, TCP port).
        /// </summary>
        public static string ConvertEnrToEnode(EnrRecord enr)
        {
            if (enr == null) return null;
            if (enr.Id != "v4") return null;
            var compressed = enr.Secp256k1;
            if (compressed == null || compressed.Length != 33) return null;
            var ip = enr.IP4 ?? enr.IP6;
            if (ip == null) return null;
            var tcp = enr.TcpPort;
            if (tcp == null || tcp == 0) return null;
            byte[] uncompressed;
            try
            {
                var key = new EthECKey(compressed, false);
                var full = key.GetPubKey(false); // 65 bytes, leading 0x04
                uncompressed = new byte[64];
                Buffer.BlockCopy(full, 1, uncompressed, 0, 64);
            }
            catch (Exception)
            {
                return null;
            }
            if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any)) return null;
            return $"enode://{uncompressed.ToHex()}@{ip}:{tcp}";
        }
    }
}
