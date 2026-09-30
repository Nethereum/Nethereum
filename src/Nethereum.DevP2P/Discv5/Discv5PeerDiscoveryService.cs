using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.Enr;
using Nethereum.Signer;

namespace Nethereum.DevP2P.Discv5
{
    public sealed class Discv5PeerDiscoveryService : IAsyncDisposable, IDisposable
    {
        public static readonly TimeSpan DefaultWalkInterval = TimeSpan.FromSeconds(30);

        public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(2);

        public const int PeersPerWalk = 4;

        public static readonly uint[] WalkDistances = new uint[] { 256, 255, 254 };

        private readonly Discv5Listener _listener;
        private readonly Action<string> _enqueueEnode;
        private readonly IReadOnlyList<(EnrRecord Enr, IPEndPoint Endpoint)> _bootnodes;
        private readonly Action<string> _log;
        private readonly TimeSpan _walkInterval;
        private readonly Func<byte[], bool> _ethForkIdFilter;
        private CancellationTokenSource _cts;
        private Task _walkLoop;
        private int _disposed;

        public Discv5PeerDiscoveryService(
            Discv5Listener listener,
            Action<string> enqueueEnode,
            IList<(EnrRecord Enr, IPEndPoint Endpoint)> bootnodes,
            Action<string> log = null,
            Func<byte[], bool> ethForkIdFilter = null)
            : this(listener, enqueueEnode, bootnodes, log, DefaultWalkInterval, ethForkIdFilter) { }

        public Discv5PeerDiscoveryService(
            Discv5Listener listener,
            Action<string> enqueueEnode,
            IList<(EnrRecord Enr, IPEndPoint Endpoint)> bootnodes,
            Action<string> log,
            TimeSpan walkInterval,
            Func<byte[], bool> ethForkIdFilter = null)
        {
            _listener = listener ?? throw new ArgumentNullException(nameof(listener));
            _enqueueEnode = enqueueEnode ?? throw new ArgumentNullException(nameof(enqueueEnode));
            _bootnodes = (bootnodes ?? new List<(EnrRecord, IPEndPoint)>()).ToList();
            _log = log ?? (_ => { });
            _walkInterval = walkInterval > TimeSpan.Zero ? walkInterval : DefaultWalkInterval;
            _ethForkIdFilter = ethForkIdFilter;
        }

        public Task StartAsync(CancellationToken ct)
        {
            if (Interlocked.CompareExchange(ref _disposed, 0, 0) != 0)
                throw new ObjectDisposedException(nameof(Discv5PeerDiscoveryService));
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _ = Task.Run(() => BondBootnodesAsync(_cts.Token));
            _walkLoop = Task.Run(() => WalkLoopAsync(_cts.Token));
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            try { _cts?.Cancel(); }
            catch (ObjectDisposedException) { }
            if (_walkLoop != null)
            {
                try { await _walkLoop.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                catch (Exception) { }
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
            catch (Exception) { }
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

            TryEnqueueEnrAsEnode(enr);

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
            if (!AdmitsForkId(enr, _ethForkIdFilter))
            {
                _log($"discv5 skip {enode}: incompatible eth fork-id");
                return;
            }
            try { _enqueueEnode(enode); }
            catch (Exception) { }
        }

        public static bool AdmitsForkId(EnrRecord enr, Func<byte[], bool> ethForkIdFilter)
        {
            if (ethForkIdFilter == null) return true;
            if (enr?.Pairs == null || !enr.Pairs.TryGetValue("eth", out var ethBytes)) return true;
            try { return ethForkIdFilter(ethBytes); }
            catch (Exception) { return true; }
        }

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
                var full = key.GetPubKey(false);
                uncompressed = new byte[64];
                Buffer.BlockCopy(full, 1, uncompressed, 0, 64);
            }
            catch (Exception)
            {
                return null;
            }
            if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any)) return null;
            return EnodeUrl.Format(uncompressed, ip, tcp.Value);
        }
    }
}
