using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Discv4;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;

namespace Nethereum.DevP2P.Discv4
{
    public sealed class PeerDiscoveryService : IDisposable
    {
        private readonly EthECKey _localKey;
        private readonly Discv4RoutingTable _routing;
        private readonly Discv4Listener _listener;
        private readonly Action<string> _log;
        private readonly ConcurrentDictionary<string, byte> _discovered = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingPongs = new();

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
            _listener.Start(udpPort, IPAddress.Any);
            _started = true;
            _log($"Discv4 listener bound on UDP port {_listener.Port}.");
        }

        [NethereumDocExample(DocSection.DevP2P, "devp2p", "PeerDiscoveryService.DiscoverAsync — discv4 peer harvest")]
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
                    try { host = EnodeUrl.Parse(enode).Host; } catch { host = enode; }
                    _log($"  discv4 to {host}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            return _discovered.Keys.ToList();
        }

        private async Task BondAndFindAsync(string enode, TimeSpan timeout, CancellationToken ct)
        {
            var parsed = EnodeUrl.Parse(enode);
            var ips = await System.Net.Dns.GetHostAddressesAsync(parsed.Host).WaitAsync(timeout, ct);
            var ip = Array.Find(ips, a => a.AddressFamily == AddressFamily.InterNetwork) ?? ips[0];
            var remote = new IPEndPoint(ip, parsed.DiscoveryPort);

            var expiration = await PingAndAwaitPongAsync(parsed, ip, remote, timeout, ct);
            await SendFindNodeRoundsAsync(remote, expiration, ct);
        }

        private async Task<long> PingAndAwaitPongAsync(
            EnodeUrl parsed, IPAddress ip, IPEndPoint remote, TimeSpan timeout, CancellationToken ct)
        {
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
                To = new Discv4Endpoint { IP = ip, UdpPort = (ushort)parsed.Port, TcpPort = (ushort)parsed.Port },
                Expiration = expiration
            };

            var peerKey = parsed.PublicKey.ToHex() + "|" + ip;
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
            return expiration;
        }

        private async Task SendFindNodeRoundsAsync(IPEndPoint remote, long expiration, CancellationToken ct)
        {
            for (int round = 0; round < 3; round++)
            {
                var target = new byte[64];
                System.Security.Cryptography.RandomNumberGenerator.Fill(target);
                var findNode = new Discv4FindNodeMessage { Target = target, Expiration = expiration };
                await _listener.SendFindNodeAsync(remote, findNode, ct);
            }

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
                var enode = EnodeUrl.Format(n.NodeId, n.IP, n.TcpPort);
                if (_discovered.Count >= MaxDiscoveredEntries) break;
                _discovered.TryAdd(enode, 0);
            }
        }

        public void Dispose()
        {
            try { _listener.StopAsync().GetAwaiter().GetResult(); } catch { }
            _listener.Dispose();
        }
    }
}
