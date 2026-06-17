using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.DevP2P.Discv4
{
    /// <summary>
    /// discv4 UDP socket loop. Receives signed packets, verifies hash + recovers
    /// sender pub key, dispatches to typed message handlers, maintains the
    /// routing table. When AutoRespond is true the listener answers PING with
    /// PONG, FINDNODE (from bonded peers) with NEIGHBORS, and ENRRequest with
    /// the local ENR per the discv4 spec — sufficient to pass go-ethereum's
    /// `devp2p discv4 test` conformance suite.
    /// </summary>
    public class Discv4Listener : IDisposable
    {
        private readonly EthECKey _localKey;
        private readonly Discv4RoutingTable _routingTable;
        // Bond key = "<nodeIdHex>|<ip>" — spec requires endpoint proof to be
        // tied to BOTH the node id AND the IP it was performed from
        // (amplification defence). FINDNODE arriving from a different IP than
        // the bonded one must not be answered.
        private readonly ConcurrentDictionary<string, DateTime> _bondedPeers = new();
        // Pending pings = "<nodeIdHex>|<ip>" → (ping-packet-hash, sent-at).
        // PONG only completes the endpoint proof when reply-token matches a hash
        // we recorded here (spec: amplification defence — InvalidPongHash test).
        // The DateTime lets a periodic sweep evict pings older than PendingPingTtl —
        // anything older than that will never receive a matching PONG.
        private readonly ConcurrentDictionary<string, (byte[] Hash, DateTime CreatedUtc)> _pendingPings = new();
        // Per-destination-IP rate limit on auto-back-ping. A spoofed-source-IP
        // PING tricks us into PINGing the victim; without throttling, each
        // unique attacker-spoofed source becomes one outbound PING. Geth's
        // discoverV4 handlePing has the same throttle. Map: dest-IP → last
        // back-ping sent time. Sweep alongside _bondedPeers / _pendingPings.
        private readonly ConcurrentDictionary<string, DateTime> _lastBackPingByIp = new();
        private static readonly TimeSpan BondLifetime = TimeSpan.FromHours(12);
        private static readonly TimeSpan PendingPingTtl = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan MapSweepInterval = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan BackPingCooldown = TimeSpan.FromSeconds(30);
        private DateTime _lastMapSweepUtc = DateTime.UtcNow;
        private UdpClient _udp;
        private CancellationTokenSource _cts;
        private Task _readLoop;

        public IPEndPoint LocalEndpoint => (IPEndPoint)_udp?.Client?.LocalEndPoint;
        public int Port => LocalEndpoint?.Port ?? 0;
        public byte[] NodeId => _localKey.GetPubKeyNoPrefix();
        public Discv4RoutingTable Routing => _routingTable;

        public bool AutoRespond { get; set; } = true;
        public byte[] LocalEnrEncoded { get; set; }
        public ulong LocalEnrSequence { get; set; } = 1;

        public event EventHandler<Discv4PingReceivedEventArgs> PingReceived;
        public event EventHandler<Discv4PongReceivedEventArgs> PongReceived;
        public event EventHandler<Discv4FindNodeReceivedEventArgs> FindNodeReceived;
        public event EventHandler<Discv4NeighborsReceivedEventArgs> NeighborsReceived;
        public event EventHandler<Discv4EnrRequestReceivedEventArgs> EnrRequestReceived;
        public event EventHandler<Discv4ErrorEventArgs> ErrorOccurred;

        public Discv4Listener(EthECKey localKey, Discv4RoutingTable routingTable)
        {
            _localKey = localKey ?? throw new ArgumentNullException(nameof(localKey));
            _routingTable = routingTable ?? throw new ArgumentNullException(nameof(routingTable));
        }

        public void Start(int udpPort = 0, IPAddress bindAddress = null)
        {
            if (_udp != null)
                throw new InvalidOperationException("Listener already started");

            var ep = new IPEndPoint(bindAddress ?? IPAddress.Loopback, udpPort);
            _udp = new UdpClient(ep);
            _cts = new CancellationTokenSource();
            _readLoop = Task.Run(() => ReadLoopAsync(_cts.Token));
        }

        public async Task StopAsync()
        {
            if (_cts == null) return;
            _cts.Cancel();
            try { _udp?.Close(); } catch { }
            try { if (_readLoop != null) await _readLoop; } catch { }
            _udp = null;
            _cts.Dispose();
            _cts = null;
            _readLoop = null;
        }

        public async Task SendPingAsync(IPEndPoint remote, Discv4PingMessage ping, CancellationToken ct = default)
        {
            var data = Discv4MessageEncoder.EncodePing(ping);
            var packet = Discv4Packet.Encode(_localKey, Discv4MessageType.Ping, data);
            await _udp.SendAsync(packet, packet.Length, remote);
        }

        public async Task SendPongAsync(IPEndPoint remote, Discv4PongMessage pong, CancellationToken ct = default)
        {
            var data = Discv4MessageEncoder.EncodePong(pong);
            var packet = Discv4Packet.Encode(_localKey, Discv4MessageType.Pong, data);
            await _udp.SendAsync(packet, packet.Length, remote);
        }

        public async Task SendFindNodeAsync(IPEndPoint remote, Discv4FindNodeMessage findNode, CancellationToken ct = default)
        {
            var data = Discv4MessageEncoder.EncodeFindNode(findNode);
            var packet = Discv4Packet.Encode(_localKey, Discv4MessageType.FindNode, data);
            await _udp.SendAsync(packet, packet.Length, remote);
        }

        public async Task SendNeighborsAsync(IPEndPoint remote, Discv4NeighborsMessage neighbors, CancellationToken ct = default)
        {
            var data = Discv4MessageEncoder.EncodeNeighbors(neighbors);
            var packet = Discv4Packet.Encode(_localKey, Discv4MessageType.Neighbors, data);
            await _udp.SendAsync(packet, packet.Length, remote);
        }

        private void MaybeSweepMaps()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastMapSweepUtc) < MapSweepInterval) return;
            _lastMapSweepUtc = now;

            foreach (var kv in _bondedPeers)
            {
                if ((now - kv.Value) > BondLifetime)
                    _bondedPeers.TryRemove(kv.Key, out _);
            }
            foreach (var kv in _pendingPings)
            {
                if ((now - kv.Value.CreatedUtc) > PendingPingTtl)
                    _pendingPings.TryRemove(kv.Key, out _);
            }
            foreach (var kv in _lastBackPingByIp)
            {
                // Keep cooldown entries for ~3 windows so a slow stream of
                // packets from the same IP keeps the throttle effective.
                if ((now - kv.Value) > BackPingCooldown + BackPingCooldown + BackPingCooldown)
                    _lastBackPingByIp.TryRemove(kv.Key, out _);
            }
        }

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                MaybeSweepMaps();
                UdpReceiveResult result;
                try
                {
                    result = await _udp.ReceiveAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // Transient UDP errors (ICMP-unreachable surfaces as
                    // SocketException, occasional kernel-level hiccups) must
                    // not kill the discovery loop. Brief backoff prevents
                    // hot-spin on persistent fault.
                    ErrorOccurred?.Invoke(this, new Discv4ErrorEventArgs("Receive", ex));
                    try { await Task.Delay(50, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                    continue;
                }

                try
                {
                    var decoded = Discv4Packet.Decode(result.Buffer);
                    DispatchPacket(decoded, result.RemoteEndPoint);
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, new Discv4ErrorEventArgs("Decode", ex));
                }
            }
        }

        private void DispatchPacket(Discv4Packet.DecodedPacket packet, IPEndPoint sender)
        {
            var sourceNode = new Discv4Node
            {
                NodeId = packet.SenderPubKey,
                IP = sender.Address,
                UdpPort = (ushort)sender.Port,
                LastSeen = DateTime.UtcNow
            };

            switch (packet.Type)
            {
                case Discv4MessageType.Ping:
                {
                    var ping = Discv4MessageEncoder.DecodePing(packet.Data);
                    if (IsExpired(ping.Expiration)) return;
                    _routingTable.Add(sourceNode);
                    if (AutoRespond)
                    {
                        _ = AutoPongAsync(sender, packet.Hash);
                        _ = AutoBackPingAsync(sender, packet.SenderPubKey);
                    }
                    PingReceived?.Invoke(this, new Discv4PingReceivedEventArgs(sender, ping, packet.Hash, sourceNode));
                    break;
                }
                case Discv4MessageType.Pong:
                {
                    var pong = Discv4MessageEncoder.DecodePong(packet.Data);
                    if (IsExpired(pong.Expiration)) return;
                    _routingTable.Add(sourceNode);
                    var key = BondKey(packet.SenderPubKey, sender);
                    if (_pendingPings.TryGetValue(key, out var pending)
                        && ByteUtil.AreEqual(pending.Hash, pong.PingHash))
                    {
                        _pendingPings.TryRemove(key, out _);
                        _bondedPeers[key] = DateTime.UtcNow;
                    }
                    PongReceived?.Invoke(this, new Discv4PongReceivedEventArgs(sender, pong, sourceNode));
                    break;
                }
                case Discv4MessageType.FindNode:
                {
                    var findNode = Discv4MessageEncoder.DecodeFindNode(packet.Data);
                    if (IsExpired(findNode.Expiration)) return;
                    // Bonding gates the auto-NEIGHBORS reply (amplification defence),
                    // but observers should still see the FINDNODE arrival.
                    if (AutoRespond && IsBonded(packet.SenderPubKey, sender))
                        _ = AutoNeighborsAsync(sender, findNode.Target);
                    FindNodeReceived?.Invoke(this, new Discv4FindNodeReceivedEventArgs(sender, findNode, sourceNode));
                    break;
                }
                case Discv4MessageType.Neighbors:
                {
                    var neighbors = Discv4MessageEncoder.DecodeNeighbors(packet.Data);
                    if (IsExpired(neighbors.Expiration)) return;
                    NeighborsReceived?.Invoke(this, new Discv4NeighborsReceivedEventArgs(sender, neighbors, sourceNode));
                    break;
                }
                case Discv4MessageType.EnrRequest:
                {
                    // EnrResponse is gated by bonding (per EIP-868), but observers should
                    // still see the request arrive.
                    if (AutoRespond && LocalEnrEncoded != null && IsBonded(packet.SenderPubKey, sender))
                        _ = AutoEnrResponseAsync(sender, packet.Hash);
                    EnrRequestReceived?.Invoke(this, new Discv4EnrRequestReceivedEventArgs(sender, packet.Hash, sourceNode));
                    break;
                }
            }
        }

        private static string BondKey(byte[] nodeId, IPEndPoint endpoint)
        {
            return nodeId.ToHex() + "|" + endpoint.Address.ToString();
        }

        private bool IsBonded(byte[] nodeId, IPEndPoint endpoint)
        {
            if (!_bondedPeers.TryGetValue(BondKey(nodeId, endpoint), out var when)) return false;
            return DateTime.UtcNow - when < BondLifetime;
        }

        private async Task AutoPongAsync(IPEndPoint to, byte[] pingHash)
        {
            try
            {
                var pong = new Discv4PongMessage
                {
                    To = new Discv4Endpoint
                    {
                        IP = to.Address,
                        UdpPort = (ushort)to.Port,
                        TcpPort = 0
                    },
                    PingHash = pingHash,
                    Expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds(),
                    EnrSeq = LocalEnrEncoded != null ? LocalEnrSequence : (ulong?)null
                };
                await SendPongAsync(to, pong);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new Discv4ErrorEventArgs("AutoPong", ex));
            }
        }

        private async Task AutoBackPingAsync(IPEndPoint to, byte[] recipientNodeId)
        {
            // Amplification throttle: at most one back-ping per destination IP
            // per BackPingCooldown window. Otherwise a remote attacker who
            // spoofs source IPs in PING traffic can use us as a UDP amplifier
            // toward whatever address they pick.
            var ipKey = to.Address.ToString();
            var now = DateTime.UtcNow;
            if (_lastBackPingByIp.TryGetValue(ipKey, out var lastSent)
                && now - lastSent < BackPingCooldown)
            {
                return;
            }
            _lastBackPingByIp[ipKey] = now;
            try
            {
                var localEp = LocalEndpoint;
                var ping = new Discv4PingMessage
                {
                    Version = 4,
                    From = new Discv4Endpoint
                    {
                        IP = localEp?.Address ?? IPAddress.Loopback,
                        UdpPort = (ushort)(localEp?.Port ?? 0),
                        TcpPort = 0
                    },
                    To = new Discv4Endpoint
                    {
                        IP = to.Address,
                        UdpPort = (ushort)to.Port,
                        TcpPort = 0
                    },
                    Expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds(),
                    EnrSeq = LocalEnrEncoded != null ? LocalEnrSequence : (ulong?)null
                };
                var data = Discv4MessageEncoder.EncodePing(ping);
                var packet = Discv4Packet.Encode(_localKey, Discv4MessageType.Ping, data);
                // The first 32 bytes of the packet are the keccak-256 hash that
                // the peer must echo in its PONG reply-token. Stash it so we
                // can validate the eventual PONG.
                var pingHash = new byte[32];
                Buffer.BlockCopy(packet, 0, pingHash, 0, 32);
                _pendingPings[BondKey(recipientNodeId, to)] = (pingHash, DateTime.UtcNow);
                await _udp.SendAsync(packet, packet.Length, to);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new Discv4ErrorEventArgs("AutoBackPing", ex));
            }
        }

        private async Task AutoNeighborsAsync(IPEndPoint to, byte[] target)
        {
            try
            {
                var closest = _routingTable.FindClosest(target);
                var neighbors = new Discv4NeighborsMessage
                {
                    Expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds()
                };
                foreach (var n in closest)
                {
                    neighbors.Nodes.Add(new Discv4Neighbor
                    {
                        IP = n.IP,
                        UdpPort = n.UdpPort,
                        TcpPort = n.TcpPort,
                        NodeId = n.NodeId
                    });
                }
                await SendNeighborsAsync(to, neighbors);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new Discv4ErrorEventArgs("AutoNeighbors", ex));
            }
        }

        private async Task AutoEnrResponseAsync(IPEndPoint to, byte[] requestHash)
        {
            try
            {
                // Wire format: rlp([request-hash, enr-record])
                // LocalEnrEncoded is ALREADY an RLP-encoded list, so we embed
                // it as the second element directly rather than wrapping it as
                // a byte string (which would change the typecode).
                var payload = Nethereum.RLP.RLP.EncodeList(
                    Nethereum.RLP.RLP.EncodeElement(requestHash),
                    LocalEnrEncoded);
                var packet = Discv4Packet.Encode(_localKey, Discv4MessageType.EnrResponse, payload);
                await _udp.SendAsync(packet, packet.Length, to);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new Discv4ErrorEventArgs("AutoEnrResponse", ex));
            }
        }

        private static bool IsExpired(long expiration)
        {
            return expiration < DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public void Dispose() => StopAsync().GetAwaiter().GetResult();
    }

    public class Discv4PingReceivedEventArgs : EventArgs
    {
        public IPEndPoint Sender { get; }
        public Discv4PingMessage Ping { get; }
        public byte[] PingHash { get; }
        public Discv4Node SourceNode { get; }
        public Discv4PingReceivedEventArgs(IPEndPoint s, Discv4PingMessage p, byte[] hash, Discv4Node src)
        { Sender = s; Ping = p; PingHash = hash; SourceNode = src; }
    }
    public class Discv4PongReceivedEventArgs : EventArgs
    {
        public IPEndPoint Sender { get; }
        public Discv4PongMessage Pong { get; }
        public Discv4Node SourceNode { get; }
        public Discv4PongReceivedEventArgs(IPEndPoint s, Discv4PongMessage p, Discv4Node src)
        { Sender = s; Pong = p; SourceNode = src; }
    }
    public class Discv4FindNodeReceivedEventArgs : EventArgs
    {
        public IPEndPoint Sender { get; }
        public Discv4FindNodeMessage FindNode { get; }
        public Discv4Node SourceNode { get; }
        public Discv4FindNodeReceivedEventArgs(IPEndPoint s, Discv4FindNodeMessage f, Discv4Node src)
        { Sender = s; FindNode = f; SourceNode = src; }
    }
    public class Discv4NeighborsReceivedEventArgs : EventArgs
    {
        public IPEndPoint Sender { get; }
        public Discv4NeighborsMessage Neighbors { get; }
        public Discv4Node SourceNode { get; }
        public Discv4NeighborsReceivedEventArgs(IPEndPoint s, Discv4NeighborsMessage n, Discv4Node src)
        { Sender = s; Neighbors = n; SourceNode = src; }
    }
    public class Discv4EnrRequestReceivedEventArgs : EventArgs
    {
        public IPEndPoint Sender { get; }
        public byte[] RequestHash { get; }
        public Discv4Node SourceNode { get; }
        public Discv4EnrRequestReceivedEventArgs(IPEndPoint s, byte[] hash, Discv4Node src)
        { Sender = s; RequestHash = hash; SourceNode = src; }
    }
    public class Discv4ErrorEventArgs : EventArgs
    {
        public string Phase { get; }
        public Exception Exception { get; }
        public Discv4ErrorEventArgs(string phase, Exception ex) { Phase = phase; Exception = ex; }
    }
}
