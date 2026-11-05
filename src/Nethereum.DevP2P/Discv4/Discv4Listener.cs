using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Netutil;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.DevP2P.Discv4
{
    public class Discv4Listener : IDisposable
    {
        private sealed class PendingPing
        {
            public byte[] NodeId;
            public IPAddress Address;
            public ushort TcpPort;
            public DateTime CreatedUtc;
            public TimeSpan Lifetime;
            public PendingPingKind Kind;
            public TaskCompletionSource Answered;
        }

        private enum PendingPingKind
        {
            Outbound,
            BackPing
        }

        private sealed class PendingFindNode
        {
            public byte[] NodeId;
            public int Remaining;
            public DateTime ExpiresUtc;
        }

        private readonly EthECKey _localKey;
        private readonly Discv4RoutingTable _routingTable;
        private readonly ConcurrentDictionary<string, DateTime> _bondedPeers = new();
        private readonly ConcurrentDictionary<string, PendingPing> _pendingPings = new();
        private readonly Dictionary<string, PendingFindNode> _pendingFindNodes = new();
        private readonly object _pendingFindNodesGate = new();
        private readonly object _pendingPingsGate = new();
        private static readonly TimeSpan BondLifetime = TimeSpan.FromHours(12);
        private static readonly TimeSpan BackPingReplyTimeout = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan OutboundReplyTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan MapSweepInterval = TimeSpan.FromMinutes(1);
        private DateTime _lastMapSweepUtc = DateTime.UtcNow;
        private UdpClient _udp;
        private CancellationTokenSource _cts;
        private Task _readLoop;
        private Task _revalidationLoop;

        public IPEndPoint LocalEndpoint => (IPEndPoint)_udp?.Client?.LocalEndPoint;
        public int Port => LocalEndpoint?.Port ?? 0;
        public byte[] NodeId => _localKey.GetPubKeyNoPrefix();
        public Discv4RoutingTable Routing => _routingTable;

        public bool AutoRespond { get; set; } = true;
        public byte[] LocalEnrEncoded { get; set; }
        public ulong LocalEnrSequence { get; set; } = 1;
        public int MaxPendingPings { get; set; } = 1024;
        public int MaxBondedPeers { get; set; } = 8192;
        public TimeSpan RevalidationInterval { get; set; } = TimeSpan.FromSeconds(10);
        public TimeSpan RevalidationTimeout { get; set; } = TimeSpan.FromSeconds(2);

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
            _udp.IgnoreIcmpConnectionReset();
            _cts = new CancellationTokenSource();
            _readLoop = Task.Run(() => ReadLoopAsync(_cts.Token));
            if (RevalidationInterval > TimeSpan.Zero)
                _revalidationLoop = Task.Run(() => RevalidationLoopAsync(_cts.Token));
        }

        public async Task StopAsync()
        {
            if (_cts == null) return;
            _cts.Cancel();
            try { _udp?.Close(); } catch { }
            try { if (_readLoop != null) await _readLoop; } catch { }
            try { if (_revalidationLoop != null) await _revalidationLoop; } catch { }
            _revalidationLoop = null;
            _udp = null;
            _cts.Dispose();
            _cts = null;
            _readLoop = null;
        }

        public Task SendPingAsync(IPEndPoint remote, Discv4PingMessage ping, CancellationToken ct = default, byte[] remoteNodeId = null)
        {
            return SendTrackedPingAsync(remote, ping, remoteNodeId, null, ct);
        }

        private async Task SendTrackedPingAsync(IPEndPoint remote, Discv4PingMessage ping, byte[] remoteNodeId, TaskCompletionSource answered, CancellationToken ct)
        {
            var data = Discv4MessageEncoder.EncodePing(ping);
            var packet = Discv4Packet.Encode(_localKey, Discv4MessageType.Ping, data);
            RegisterPendingPing(packet, new PendingPing
            {
                NodeId = remoteNodeId,
                Address = remote.Address,
                TcpPort = ping.To.TcpPort,
                Lifetime = OutboundReplyTimeout,
                Kind = PendingPingKind.Outbound,
                Answered = answered
            }, enforceCap: false);
            await _udp.SendAsync(packet, remote, ct).ConfigureAwait(false);
        }

        public async Task SendPongAsync(IPEndPoint remote, Discv4PongMessage pong, CancellationToken ct = default)
        {
            var data = Discv4MessageEncoder.EncodePong(pong);
            var packet = Discv4Packet.Encode(_localKey, Discv4MessageType.Pong, data);
            await _udp.SendAsync(packet, packet.Length, remote);
        }

        public async Task SendFindNodeAsync(IPEndPoint remote, Discv4FindNodeMessage findNode, CancellationToken ct = default, byte[] remoteNodeId = null)
        {
            var data = Discv4MessageEncoder.EncodeFindNode(findNode);
            var packet = Discv4Packet.Encode(_localKey, Discv4MessageType.FindNode, data);
            ExpectNeighbors(remote.Address, remoteNodeId);
            await _udp.SendAsync(packet, packet.Length, remote);
        }

        public async Task SendNeighborsAsync(IPEndPoint remote, Discv4NeighborsMessage neighbors, CancellationToken ct = default)
        {
            var data = Discv4MessageEncoder.EncodeNeighbors(neighbors);
            var packet = Discv4Packet.Encode(_localKey, Discv4MessageType.Neighbors, data);
            await _udp.SendAsync(packet, packet.Length, remote);
        }

        public async Task<bool> RevalidateAsync(CancellationToken ct = default)
        {
            var node = _routingTable.NextRevalidationCandidate();
            if (node == null) return true;
            var remote = new IPEndPoint(node.IP, node.UdpPort);
            var answered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await SendTrackedPingAsync(remote, BuildPing(remote, node.TcpPort), node.NodeId, answered, ct).ConfigureAwait(false);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var expired = Task.Delay(RevalidationTimeout, timeout.Token);
            var winner = await Task.WhenAny(answered.Task, expired).ConfigureAwait(false);
            timeout.Cancel();
            ct.ThrowIfCancellationRequested();
            if (winner == answered.Task) return true;
            _bondedPeers.TryRemove(BondKey(node.NodeId, remote), out _);
            _routingTable.Remove(node.NodeId);
            return false;
        }

        private async Task RevalidationLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(RevalidationInterval, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
                try { await RevalidateAsync(ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
                catch (ObjectDisposedException) { return; }
                catch (Exception ex) { RaiseError("Revalidate", ex); }
            }
        }

        private Discv4PingMessage BuildPing(IPEndPoint to, ushort toTcpPort)
        {
            var localEp = LocalEndpoint;
            return new Discv4PingMessage
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
                    TcpPort = toTcpPort
                },
                Expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds(),
                EnrSeq = LocalEnrEncoded != null ? LocalEnrSequence : (ulong?)null
            };
        }

        private void RaiseError(string phase, Exception ex)
        {
            var handlers = ErrorOccurred;
            if (handlers == null) return;
            foreach (EventHandler<Discv4ErrorEventArgs> handler in handlers.GetInvocationList())
            {
                try { handler(this, new Discv4ErrorEventArgs(phase, ex)); }
                catch { }
            }
        }

        private void Raise<TArgs>(EventHandler<TArgs> handlers, TArgs args)
        {
            if (handlers == null) return;
            foreach (EventHandler<TArgs> handler in handlers.GetInvocationList())
            {
                try { handler(this, args); }
                catch (Exception ex) { RaiseError("Handler", ex); }
            }
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
            SweepExpiredPendingPings(now);
            lock (_pendingFindNodesGate)
            {
                var expired = new List<string>();
                foreach (var kv in _pendingFindNodes)
                {
                    if (kv.Value.ExpiresUtc < now) expired.Add(kv.Key);
                }
                foreach (var key in expired) _pendingFindNodes.Remove(key);
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
                    RaiseError("Receive", ex);
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
                    RaiseError("Decode", ex);
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
                    HandlePing(packet, sender, sourceNode);
                    break;
                case Discv4MessageType.Pong:
                    HandlePong(packet, sender, sourceNode);
                    break;
                case Discv4MessageType.FindNode:
                    HandleFindNode(packet, sender, sourceNode);
                    break;
                case Discv4MessageType.Neighbors:
                    HandleNeighbors(packet, sender, sourceNode);
                    break;
                case Discv4MessageType.EnrRequest:
                    HandleEnrRequest(packet, sender, sourceNode);
                    break;
            }
        }

        private void HandlePing(Discv4Packet.DecodedPacket packet, IPEndPoint sender, Discv4Node sourceNode)
        {
            var ping = Discv4MessageEncoder.DecodePing(packet.Data);
            if (IsExpired(ping.Expiration)) return;
            sourceNode.TcpPort = ping.From.TcpPort;
            if (AutoRespond)
            {
                _ = AutoPongAsync(sender, packet.Hash);
                if (IsBonded(packet.SenderPubKey, sender))
                    _routingTable.Add(sourceNode);
                else
                    _ = AutoBackPingAsync(sender, packet.SenderPubKey, sourceNode.TcpPort);
            }
            Raise(PingReceived, new Discv4PingReceivedEventArgs(sender, ping, packet.Hash, sourceNode));
        }

        private void HandlePong(Discv4Packet.DecodedPacket packet, IPEndPoint sender, Discv4Node sourceNode)
        {
            var pong = Discv4MessageEncoder.DecodePong(packet.Data);
            if (IsExpired(pong.Expiration)) return;
            if (!TryTakePendingPing(pong.PingHash, packet.SenderPubKey, sender.Address, out var pending)) return;
            RecordBond(BondKey(packet.SenderPubKey, sender));
            sourceNode.TcpPort = pending.TcpPort;
            _routingTable.Add(sourceNode);
            pending.Answered?.TrySetResult();
            Raise(PongReceived, new Discv4PongReceivedEventArgs(sender, pong, sourceNode));
        }

        private void HandleFindNode(Discv4Packet.DecodedPacket packet, IPEndPoint sender, Discv4Node sourceNode)
        {
            var findNode = Discv4MessageEncoder.DecodeFindNode(packet.Data);
            if (IsExpired(findNode.Expiration)) return;
            if (AutoRespond && IsBonded(packet.SenderPubKey, sender))
                _ = AutoNeighborsAsync(sender, findNode.Target);
            Raise(FindNodeReceived, new Discv4FindNodeReceivedEventArgs(sender, findNode, sourceNode));
        }

        private void HandleNeighbors(Discv4Packet.DecodedPacket packet, IPEndPoint sender, Discv4Node sourceNode)
        {
            var neighbors = Discv4MessageEncoder.DecodeNeighbors(packet.Data);
            if (IsExpired(neighbors.Expiration)) return;
            if (!TryConsumeExpectedNeighbors(packet.SenderPubKey, sender.Address, neighbors.Nodes.Count)) return;
            Raise(NeighborsReceived, new Discv4NeighborsReceivedEventArgs(sender, neighbors, sourceNode));
        }

        private void HandleEnrRequest(Discv4Packet.DecodedPacket packet, IPEndPoint sender, Discv4Node sourceNode)
        {
            if (IsExpired(Discv4MessageEncoder.DecodeEnrRequestExpiration(packet.Data))) return;
            if (AutoRespond && LocalEnrEncoded != null && IsBonded(packet.SenderPubKey, sender))
                _ = AutoEnrResponseAsync(sender, packet.Hash);
            Raise(EnrRequestReceived, new Discv4EnrRequestReceivedEventArgs(sender, packet.Hash, sourceNode));
        }

        private static string BondKey(byte[] nodeId, IPEndPoint endpoint) => BondKey(nodeId, endpoint.Address);

        private static string BondKey(byte[] nodeId, IPAddress address)
        {
            return nodeId.ToHex() + "|" + address.ToString();
        }

        private void RecordBond(string key)
        {
            var now = DateTime.UtcNow;
            if (!_bondedPeers.ContainsKey(key) && _bondedPeers.Count >= MaxBondedPeers)
            {
                foreach (var kv in _bondedPeers)
                {
                    if ((now - kv.Value) > BondLifetime)
                        _bondedPeers.TryRemove(kv.Key, out _);
                }
                if (_bondedPeers.Count >= MaxBondedPeers) EvictBond();
            }
            _bondedPeers[key] = now;
        }

        private void EvictBond()
        {
            var inTable = new HashSet<string>();
            foreach (var node in _routingTable.AllNodes())
                inTable.Add(BondKey(node.NodeId, node.IP));

            string victim = null;
            var victimInTable = false;
            var victimBondedUtc = DateTime.MinValue;
            foreach (var kv in _bondedPeers)
            {
                var candidateInTable = inTable.Contains(kv.Key);
                var better = victim == null
                    || (!victimInTable && candidateInTable)
                    || (victimInTable && candidateInTable && kv.Value < victimBondedUtc)
                    || (!victimInTable && !candidateInTable && kv.Value > victimBondedUtc);
                if (!better) continue;
                victim = kv.Key;
                victimInTable = candidateInTable;
                victimBondedUtc = kv.Value;
            }
            if (victim != null) _bondedPeers.TryRemove(victim, out _);
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
                RaiseError("AutoPong", ex);
            }
        }

        private async Task AutoBackPingAsync(IPEndPoint to, byte[] recipientNodeId, ushort recipientTcpPort)
        {
            try
            {
                var ping = BuildPing(to, 0);
                var data = Discv4MessageEncoder.EncodePing(ping);
                var packet = Discv4Packet.Encode(_localKey, Discv4MessageType.Ping, data);
                RegisterPendingPing(packet, new PendingPing
                {
                    NodeId = recipientNodeId,
                    Address = to.Address,
                    TcpPort = recipientTcpPort,
                    Lifetime = BackPingReplyTimeout,
                    Kind = PendingPingKind.BackPing
                }, enforceCap: true);
                await _udp.SendAsync(packet, packet.Length, to);
            }
            catch (Exception ex)
            {
                RaiseError("AutoBackPing", ex);
            }
        }

        private static string PendingPingKey(byte[] nodeId, IPAddress address, byte[] pingHash)
        {
            return (nodeId == null ? string.Empty : nodeId.ToHex()) + "|" + address + "|" + pingHash.ToHex();
        }

        private static byte[] PacketHash(byte[] packet)
        {
            var hash = new byte[Discv4Packet.HashLength];
            Buffer.BlockCopy(packet, 0, hash, 0, Discv4Packet.HashLength);
            return hash;
        }

        private void RegisterPendingPing(byte[] packet, PendingPing pending, bool enforceCap)
        {
            pending.CreatedUtc = DateTime.UtcNow;
            lock (_pendingPingsGate)
            {
                if (enforceCap && _pendingPings.Count >= MaxPendingPings)
                {
                    SweepExpiredPendingPings(pending.CreatedUtc);
                    if (_pendingPings.Count >= MaxPendingPings) EvictOldestBackPing();
                }
                _pendingPings[PendingPingKey(pending.NodeId, pending.Address, PacketHash(packet))] = pending;
            }
        }

        private void EvictOldestBackPing()
        {
            string victim = null;
            var victimCreatedUtc = DateTime.MaxValue;
            foreach (var kv in _pendingPings)
            {
                if (kv.Value.Kind != PendingPingKind.BackPing || kv.Value.CreatedUtc >= victimCreatedUtc) continue;
                victim = kv.Key;
                victimCreatedUtc = kv.Value.CreatedUtc;
            }
            if (victim != null) _pendingPings.TryRemove(victim, out _);
        }

        private void SweepExpiredPendingPings(DateTime now)
        {
            foreach (var kv in _pendingPings)
            {
                if ((now - kv.Value.CreatedUtc) > kv.Value.Lifetime)
                    _pendingPings.TryRemove(kv.Key, out _);
            }
        }

        private bool TryTakePendingPing(byte[] pingHash, byte[] senderNodeId, IPAddress senderAddress, out PendingPing pending)
        {
            pending = null;
            if (pingHash == null) return false;
            return TryTakePendingPing(PendingPingKey(senderNodeId, senderAddress, pingHash), out pending)
                || TryTakePendingPing(PendingPingKey(null, senderAddress, pingHash), out pending);
        }

        private bool TryTakePendingPing(string key, out PendingPing pending)
        {
            pending = null;
            if (!_pendingPings.TryGetValue(key, out var candidate)) return false;
            if (DateTime.UtcNow - candidate.CreatedUtc > candidate.Lifetime) return false;
            return _pendingPings.TryRemove(key, out pending);
        }

        private void ExpectNeighbors(IPAddress address, byte[] nodeId)
        {
            lock (_pendingFindNodesGate)
            {
                var key = address.ToString();
                _pendingFindNodes.TryGetValue(key, out var pending);
                pending ??= new PendingFindNode();
                pending.NodeId = nodeId;
                pending.Remaining = Math.Min(pending.Remaining + Discv4RoutingTable.BucketSize, 3 * Discv4RoutingTable.BucketSize);
                pending.ExpiresUtc = DateTime.UtcNow + OutboundReplyTimeout;
                _pendingFindNodes[key] = pending;
            }
        }

        private bool TryConsumeExpectedNeighbors(byte[] senderNodeId, IPAddress senderAddress, int nodeCount)
        {
            lock (_pendingFindNodesGate)
            {
                var key = senderAddress.ToString();
                if (!_pendingFindNodes.TryGetValue(key, out var pending)) return false;
                if (pending.ExpiresUtc < DateTime.UtcNow)
                {
                    _pendingFindNodes.Remove(key);
                    return false;
                }
                if (pending.NodeId != null && !ByteUtil.AreEqual(pending.NodeId, senderNodeId)) return false;
                pending.Remaining -= Math.Max(nodeCount, 1);
                if (pending.Remaining <= 0) _pendingFindNodes.Remove(key);
                return true;
            }
        }

        private async Task AutoNeighborsAsync(IPEndPoint to, byte[] target)
        {
            try
            {
                var closest = _routingTable.FindClosest(target);
                var neighbors = NewNeighborsMessage();
                var sent = false;
                foreach (var n in closest)
                {
                    if (n.IP.IsValidRelayTarget(to.Address))
                    {
                        neighbors.Nodes.Add(new Discv4Neighbor
                        {
                            IP = n.IP,
                            UdpPort = n.UdpPort,
                            TcpPort = n.TcpPort,
                            NodeId = n.NodeId
                        });
                    }
                    if (neighbors.Nodes.Count == Discv4NeighborsMessage.MaxNeighbors)
                    {
                        await SendNeighborsAsync(to, neighbors);
                        neighbors = NewNeighborsMessage();
                        sent = true;
                    }
                }
                if (neighbors.Nodes.Count > 0 || !sent)
                    await SendNeighborsAsync(to, neighbors);
            }
            catch (Exception ex)
            {
                RaiseError("AutoNeighbors", ex);
            }
        }

        private static Discv4NeighborsMessage NewNeighborsMessage() => new Discv4NeighborsMessage
        {
            Expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds()
        };

        private async Task AutoEnrResponseAsync(IPEndPoint to, byte[] requestHash)
        {
            try
            {
                var payload = Nethereum.RLP.RLP.EncodeList(
                    Nethereum.RLP.RLP.EncodeElement(requestHash),
                    LocalEnrEncoded);
                var packet = Discv4Packet.Encode(_localKey, Discv4MessageType.EnrResponse, payload);
                await _udp.SendAsync(packet, packet.Length, to);
            }
            catch (Exception ex)
            {
                RaiseError("AutoEnrResponse", ex);
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
