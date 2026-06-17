using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv4;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;

namespace Nethereum.DevP2P.Discv4
{
    /// <summary>
    /// Live discv4 peer discovery. Bonds with a list of seed enodes
    /// (PING/PONG endpoint proof per the discv4 spec), sends FINDNODE with
    /// random target keys, and harvests every node id that returns in the
    /// resulting NEIGHBORS messages. Exposes the harvested enodes as a flat
    /// list for <see cref="RotatingPeerSession"/> to expand its dial pool.
    /// </summary>
    public sealed class PeerDiscoveryService : IDisposable
    {
        private readonly EthECKey _localKey;
        private readonly Discv4RoutingTable _routing;
        private readonly Discv4Listener _listener;
        private readonly Action<string> _log;
        private readonly ConcurrentDictionary<string, byte> _discovered = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingPongs = new();

        /// <summary>
        /// Hard cap on how many distinct discovered enodes we accumulate in
        /// a single DiscoverAsync invocation. Mainnet has tens of millions of
        /// reachable peers over time; without a ceiling this dictionary would
        /// grow unbounded over long syncs and eventually OOM. Geth's
        /// discv4 tableLookup similarly caps at a fixed sample size per
        /// iteration. Caller is expected to drain via the returned list.
        /// </summary>
        public const int MaxDiscoveredEntries = 4096;
        private bool _started;

        public PeerDiscoveryService(Action<string> log)
        {
            _log = log ?? (_ => { });
            _localKey = EthECKey.GenerateKey();
            _routing = new Discv4RoutingTable(_localKey.GetPubKeyNoPrefix());
            _listener = new Discv4Listener(_localKey, _routing) { AutoRespond = true };
            _listener.PongReceived += OnPongReceived;
            _listener.NeighborsReceived += OnNeighborsReceived;
        }

        public void Start(int udpPort = 0)
        {
            if (_started) return;
            // Bind to all interfaces so PONG responses can return on the same socket.
            _listener.Start(udpPort, IPAddress.Any);
            _started = true;
            _log($"Discv4 listener bound on UDP port {_listener.Port}.");
        }

        /// <summary>
        /// Discover fresh peer enodes by bonding with each seed and asking
        /// for neighbours of a few random target keys. Returns every node id
        /// that appeared in any NEIGHBORS reply, formatted as enode URLs.
        /// Targets are random so successive calls return different slices of
        /// the routing tables of the seeds we hit.
        /// </summary>
        public async Task<List<string>> DiscoverAsync(
            IEnumerable<string> seedEnodes, TimeSpan perSeedTimeout, CancellationToken ct)
        {
            if (!_started) Start();

            foreach (var enode in seedEnodes)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await BondAndFindAsync(enode, perSeedTimeout, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    string host;
                    try { host = ParseEnode(enode).Host; } catch { host = enode; }
                    _log($"  discv4 to {host}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            return _discovered.Keys.ToList();
        }

        private async Task BondAndFindAsync(string enode, TimeSpan timeout, CancellationToken ct)
        {
            var (nodeId, host, port) = ParseEnode(enode);
            var ips = await System.Net.Dns.GetHostAddressesAsync(host).WaitAsync(timeout, ct);
            var ip = Array.Find(ips, a => a.AddressFamily == AddressFamily.InterNetwork) ?? ips[0];
            var remote = new IPEndPoint(ip, port);
            var peerKey = nodeId.ToHex() + "|" + ip;

            // 1. PING — we need PONG before the peer will answer FINDNODE.
            var expiration = DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds();
            var ping = new Discv4PingMessage
            {
                Version = 4,
                From = new Discv4Endpoint
                {
                    IP = IPAddress.Any,
                    UdpPort = (ushort)_listener.Port,
                    TcpPort = (ushort)_listener.Port
                },
                To = new Discv4Endpoint { IP = ip, UdpPort = (ushort)port, TcpPort = (ushort)port },
                Expiration = expiration
            };

            var pongTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingPongs[peerKey] = pongTcs;
            try
            {
                await _listener.SendPingAsync(remote, ping, ct);
                using var pongCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                pongCts.CancelAfter(timeout);
                await pongTcs.Task.WaitAsync(pongCts.Token);
            }
            finally
            {
                _pendingPongs.TryRemove(peerKey, out _);
            }

            // 2. FINDNODE with a random target. Geth answers with its 16
            //    closest routing-table entries to that target, so issuing a
            //    couple of FINDNODEs with different targets harvests a wider
            //    slice of the seed's routing table.
            for (int round = 0; round < 3; round++)
            {
                var target = new byte[64];
                System.Security.Cryptography.RandomNumberGenerator.Fill(target);
                var findNode = new Discv4FindNodeMessage { Target = target, Expiration = expiration };
                await _listener.SendFindNodeAsync(remote, findNode, ct);
            }

            // 3. Wait for NEIGHBORS replies to arrive (event-driven; just give
            //    them a window to land).
            await Task.Delay(TimeSpan.FromMilliseconds(2000), ct);
        }

        private void OnPongReceived(object sender, Discv4PongReceivedEventArgs e)
        {
            var key = e.SourceNode.NodeId.ToHex() + "|" + e.Sender.Address;
            if (_pendingPongs.TryRemove(key, out var tcs))
            {
                tcs.TrySetResult(true);
            }
        }

        private void OnNeighborsReceived(object sender, Discv4NeighborsReceivedEventArgs e)
        {
            foreach (var n in e.Neighbors.Nodes)
            {
                if (n.NodeId == null || n.NodeId.Length != 64) continue;
                if (n.TcpPort == 0) continue;
                if (n.IP == null) continue;
                if (n.IP.Equals(IPAddress.Any) || n.IP.Equals(IPAddress.Loopback)) continue;
                var enode = $"enode://{n.NodeId.ToHex()}@{n.IP}:{n.TcpPort}";
                if (_discovered.Count >= MaxDiscoveredEntries) break;
                _discovered.TryAdd(enode, 0);
            }
        }

        private static (byte[] NodeId, string Host, int Port) ParseEnode(string enode)
        {
            const string prefix = "enode://";
            if (!enode.StartsWith(prefix))
                throw new ArgumentException("Not an enode URL", nameof(enode));
            var rest = enode.Substring(prefix.Length);
            var atIdx = rest.IndexOf('@');
            var nodeIdHex = rest.Substring(0, atIdx);
            var hostPort = rest.Substring(atIdx + 1);
            var colonIdx = hostPort.IndexOf(':');
            var host = hostPort.Substring(0, colonIdx);
            var port = int.Parse(hostPort.Substring(colonIdx + 1));
            return (nodeIdHex.HexToByteArray(), host, port);
        }

        public void Dispose()
        {
            try { _listener.StopAsync().GetAwaiter().GetResult(); } catch { }
            _listener.Dispose();
        }
    }
}
