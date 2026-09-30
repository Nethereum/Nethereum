using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Common;
using Nethereum.Model.Enr;
using Nethereum.Signer;

namespace Nethereum.DevP2P.Discv5
{
    public class Discv5Listener : IDisposable, IAsyncDisposable
    {
        public const int DisposeTimeoutMs = 250;

        public const int MaxRequestIdLength = 8;

        public const int ReciprocalPingDelayMs = 200;

        public const int MaxNodesResponseTotal = 16;

        public const int NodesRecordsBudgetBytes = 900;

        private readonly EthECKey _localKey;
        private readonly Discv5SessionManager _sessionManager;
        private readonly Discv5RoutingTable _routingTable;
        private readonly Discv5RequestTracker _requestTracker;
        private readonly ConcurrentDictionary<string, Func<byte[], IPEndPoint, byte[]>> _talkHandlers
            = new ConcurrentDictionary<string, Func<byte[], IPEndPoint, byte[]>>(StringComparer.Ordinal);
        private readonly TokenBucketRateLimiter<IPAddress> _inboundFilter;
        private readonly ConcurrentDictionary<IPAddress, long> _bannedIps
            = new ConcurrentDictionary<IPAddress, long>();
        private long _banSequence;
        private long _droppedInboundCount;
        private UdpClient _udp;
        private CancellationTokenSource _cts;
        private Task _readLoop;
        private long _outboundReqIdCounter;

        public Discv5Listener(EthECKey localKey) : this(localKey, new Discv5RequestTracker()) { }

        public Discv5Listener(EthECKey localKey, Discv5RequestTracker requestTracker)
        {
            _localKey = localKey ?? throw new ArgumentNullException(nameof(localKey));
            _requestTracker = requestTracker ?? throw new ArgumentNullException(nameof(requestTracker));
            _sessionManager = new Discv5SessionManager(localKey);
            _routingTable = new Discv5RoutingTable(_sessionManager.LocalNodeId);
            _sessionManager.SessionEstablished += OnSessionEstablished;
            _inboundFilter = new TokenBucketRateLimiter<IPAddress>(
                rate: DevP2PRateLimitConstants.InboundPacketsPerSecondPerIp,
                burst: DevP2PRateLimitConstants.InboundBurstCapacity,
                maxCachedKeys: DevP2PRateLimitConstants.KnownSourcesCacheSize);
        }

        public long DroppedInboundCount => Interlocked.Read(ref _droppedInboundCount);

        public bool IsBanned(IPAddress ip) => ip != null && _bannedIps.ContainsKey(ip);

        public Discv5RequestTracker RequestTracker => _requestTracker;

        public Discv5RoutingTable Routing => _routingTable;

        public byte[] LocalEnrEncoded { get; set; }

        public ulong LocalEnrSequence { get; set; } = 1;

        public IPEndPoint LocalEndpoint => (IPEndPoint)_udp?.Client?.LocalEndPoint;

        public int Port => LocalEndpoint?.Port ?? 0;

        public byte[] NodeId => _sessionManager.LocalNodeId;

        public void RegisterTalkHandler(string protocol, Func<byte[], IPEndPoint, byte[]> handler)
        {
            if (string.IsNullOrEmpty(protocol)) throw new ArgumentException("protocol id required", nameof(protocol));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _talkHandlers[protocol] = handler;
        }

        public void Start(IPAddress bindAddress, int port = 0)
        {
            _udp = new UdpClient(new IPEndPoint(bindAddress, port));
            _cts = new CancellationTokenSource();
            _readLoop = Task.Run(() => ReadLoopAsync(_cts.Token));
        }

        public Task StopAsync()
        {
            _cts?.Cancel();
            _udp?.Close();
            return _readLoop ?? Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            try { await StopAsync().ConfigureAwait(false); }
            catch (Exception) { }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _udp?.Close();
            try { _readLoop?.Wait(DisposeTimeoutMs); }
            catch (AggregateException) { }
            _requestTracker?.Dispose();
        }

        private void OnSessionEstablished(object sender, Discv5SessionManager.SessionEstablishedEventArgs e)
        {
            if (e.EnrEncoded != null && e.EnrEncoded.Length > 0)
            {
                _routingTable.Upsert(new Discv5RoutingTable.Entry
                {
                    NodeId = e.Session.RemoteNodeId,
                    Address = e.Session.RemoteAddr,
                    EnrEncoded = e.EnrEncoded
                });
            }
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(ReciprocalPingDelayMs).ConfigureAwait(false);
                    SendReciprocalPing(e.Session);
                }
                catch (Exception) { }
            });
        }

        private void SendReciprocalPing(Discv5Session session)
        {
            var ping = new Discv5PingMessage
            {
                RequestId = NextOutboundRequestId(),
                EnrSeq = LocalEnrSequence
            };
            SendOrdinary(session, Discv5MessageEncoder.EncodePing(ping));
        }

        public async Task<Discv5PongMessage> SendPingAsync(
            IPEndPoint peer,
            byte[] peerNodeId,
            byte[] peerStaticCompressedPubKey,
            TimeSpan timeout,
            CancellationToken ct)
        {
            if (peer == null) throw new ArgumentNullException(nameof(peer));
            if (peerNodeId == null || peerNodeId.Length != 32)
                throw new ArgumentException("peer node id must be 32 bytes", nameof(peerNodeId));

            var requestId = NextOutboundRequestId();
            var ping = new Discv5PingMessage
            {
                RequestId = requestId,
                EnrSeq = LocalEnrSequence
            };
            var msg = Discv5MessageEncoder.EncodePing(ping);

            var task = _requestTracker.RegisterPing(peerNodeId, requestId, timeout, ct);
            DispatchOutboundMessage(peer, peerNodeId, peerStaticCompressedPubKey, msg);
            return await task.ConfigureAwait(false);
        }

        public async Task<List<EnrRecord>> SendFindNodeAsync(
            IPEndPoint peer,
            byte[] peerNodeId,
            byte[] peerStaticCompressedPubKey,
            IEnumerable<uint> distances,
            TimeSpan timeout,
            CancellationToken ct)
        {
            if (peer == null) throw new ArgumentNullException(nameof(peer));
            if (peerNodeId == null || peerNodeId.Length != 32)
                throw new ArgumentException("peer node id must be 32 bytes", nameof(peerNodeId));
            if (distances == null) throw new ArgumentNullException(nameof(distances));

            var requestId = NextOutboundRequestId();
            var findNode = new Discv5FindNodeMessage { RequestId = requestId };
            foreach (var d in distances) findNode.Distances.Add(d);
            if (findNode.Distances.Count == 0)
                throw new ArgumentException("at least one distance is required", nameof(distances));

            var msg = Discv5MessageEncoder.EncodeFindNode(findNode);

            var task = _requestTracker.RegisterFindNode(
                peerNodeId, requestId, expectedTotalHint: 1, timeout, ct);
            DispatchOutboundMessage(peer, peerNodeId, peerStaticCompressedPubKey, msg);
            return await task.ConfigureAwait(false);
        }

        public async Task<byte[]> SendTalkRequestAsync(
            IPEndPoint peer,
            byte[] peerNodeId,
            byte[] peerStaticCompressedPubKey,
            byte[] protocol,
            byte[] payload,
            TimeSpan timeout,
            CancellationToken ct)
        {
            if (peer == null) throw new ArgumentNullException(nameof(peer));
            if (peerNodeId == null || peerNodeId.Length != 32)
                throw new ArgumentException("peer node id must be 32 bytes", nameof(peerNodeId));
            if (protocol == null) throw new ArgumentNullException(nameof(protocol));
            if (payload == null) payload = Array.Empty<byte>();

            var requestId = NextOutboundRequestId();
            var talkReq = new Discv5TalkReqMessage
            {
                RequestId = requestId,
                Protocol = protocol,
                Request = payload
            };
            var msg = Discv5MessageEncoder.EncodeTalkReq(talkReq);

            var task = _requestTracker.RegisterTalkRequest(peerNodeId, requestId, timeout, ct);
            DispatchOutboundMessage(peer, peerNodeId, peerStaticCompressedPubKey, msg);
            return await task.ConfigureAwait(false);
        }

        private void DispatchOutboundMessage(
            IPEndPoint peer,
            byte[] peerNodeId,
            byte[] peerStaticCompressedPubKey,
            byte[] messagePlaintext)
        {
            var session = _sessionManager.FindSession(peerNodeId, peer);
            if (session != null)
            {
                SendOrdinary(session, messagePlaintext);
                return;
            }
            if (peerStaticCompressedPubKey == null || peerStaticCompressedPubKey.Length != 33)
                throw new InvalidOperationException(
                    "No active session and no peer static pubkey provided — cannot initiate handshake.");

            var packet = _sessionManager.BuildInitialOrdinaryPacket(
                peerNodeId, peer, messagePlaintext, peerStaticCompressedPubKey, LocalEnrEncoded);
            try { _udp.Send(packet, packet.Length, peer); }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }

        private byte[] NextOutboundRequestId()
        {
            var n = (ulong)Interlocked.Increment(ref _outboundReqIdCounter);
            var b = new byte[MaxRequestIdLength];
            BinaryPrimitives.WriteUInt64BigEndian(b, n);
            return b;
        }

        private void AddBannedIp(IPAddress ip)
        {
            _bannedIps[ip] = Interlocked.Increment(ref _banSequence);
            if (_bannedIps.Count <= DevP2PRateLimitConstants.MaxBannedIpsCached) return;

            IPAddress oldest = null;
            long oldestSeq = long.MaxValue;
            foreach (var kvp in _bannedIps)
            {
                if (kvp.Value < oldestSeq)
                {
                    oldestSeq = kvp.Value;
                    oldest = kvp.Key;
                }
            }
            if (oldest != null && !oldest.Equals(ip))
                _bannedIps.TryRemove(oldest, out _);
        }

        private enum LoopSignal
        {
            Proceed,
            SkipIteration,
            StopLoop
        }

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var (signal, result) = await ReceiveDatagramAsync(ct).ConfigureAwait(false);
                if (signal == LoopSignal.StopLoop) return;
                if (signal == LoopSignal.SkipIteration) continue;

                if (!IsAcceptablePacketSize(result.Buffer)) continue;
                if (!TryAdmitInboundSource(result.RemoteEndPoint)) continue;

                if (await DispatchPacketAsync(result.Buffer, result.RemoteEndPoint).ConfigureAwait(false) == LoopSignal.StopLoop)
                    return;
            }
        }

        private async Task<(LoopSignal Signal, UdpReceiveResult Result)> ReceiveDatagramAsync(CancellationToken ct)
        {
            try
            {
                var result = await _udp.ReceiveAsync(ct).ConfigureAwait(false);
                return (LoopSignal.Proceed, result);
            }
            catch (OperationCanceledException) { return (LoopSignal.StopLoop, default); }
            catch (ObjectDisposedException) { return (LoopSignal.StopLoop, default); }
            catch (SocketException)
            {
                try { await Task.Delay(50, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return (LoopSignal.StopLoop, default); }
                return (LoopSignal.SkipIteration, default);
            }
        }

        private static bool IsAcceptablePacketSize(byte[] buffer)
            => buffer != null
                && buffer.Length >= Discv5Packet.MinPacketSize
                && buffer.Length <= Discv5Packet.MaxPacketSize;

        private bool TryAdmitInboundSource(IPEndPoint remoteEndPoint)
        {
            var srcIp = remoteEndPoint?.Address;
            if (srcIp == null) return false;
            if (_bannedIps.ContainsKey(srcIp))
            {
                Interlocked.Increment(ref _droppedInboundCount);
                return false;
            }
            if (!_inboundFilter.TryAcquire(srcIp))
            {
                Interlocked.Increment(ref _droppedInboundCount);
                AddBannedIp(srcIp);
                return false;
            }
            return true;
        }

        private async Task<LoopSignal> DispatchPacketAsync(byte[] buffer, IPEndPoint remoteEndPoint)
        {
            var processed = _sessionManager.Process(buffer, remoteEndPoint);
            if (processed.Kind == Discv5SessionManager.IncomingPacketKind.NeedWhoAreYou)
            {
                try { await _udp.SendAsync(processed.OutgoingBytes, processed.OutgoingBytes.Length, processed.Source).ConfigureAwait(false); }
                catch (SocketException) { }
                catch (ObjectDisposedException) { return LoopSignal.StopLoop; }
                return LoopSignal.SkipIteration;
            }
            if (processed.Kind != Discv5SessionManager.IncomingPacketKind.Decoded) return LoopSignal.SkipIteration;

            try
            {
                HandleDecodedMessage(processed.Session, processed.Message, processed.Source);
            }
            catch (Exception) { }
            return LoopSignal.SkipIteration;
        }

        private void HandleDecodedMessage(Discv5Session session, byte[] plaintext, IPEndPoint from)
        {
            if (plaintext == null || plaintext.Length == 0) return;
            var (type, body) = Discv5MessageEncoder.Unpack(plaintext);

            switch (type)
            {
                case Discv5MessageType.Ping:
                    HandlePing(session, body, from);
                    break;
                case Discv5MessageType.FindNode:
                    HandleFindNode(session, body);
                    break;
                case Discv5MessageType.TalkReq:
                    HandleTalkReq(session, body);
                    break;
                case Discv5MessageType.Pong:
                    HandlePong(session, body);
                    break;
                case Discv5MessageType.Nodes:
                    HandleNodes(session, body);
                    break;
                case Discv5MessageType.TalkResp:
                    HandleTalkResp(session, body);
                    break;
            }
        }

        private static bool TryDecode<TMessage>(byte[] body, Func<byte[], TMessage> decode, out TMessage message)
        {
            try { message = decode(body); return true; }
            catch (Exception) { message = default; return false; }
        }

        private static bool IsValidRequestId(byte[] id, bool allowNull)
            => id == null ? allowNull : id.Length <= MaxRequestIdLength;

        private void HandleTalkResp(Discv5Session session, byte[] body)
        {
            if (!TryDecode(body, Discv5MessageEncoder.DecodeTalkResp, out var resp)) return;
            if (!IsValidRequestId(resp.RequestId, allowNull: true)) return;
            _requestTracker.CompleteTalkResp(session.RemoteNodeId, resp);
        }

        private void HandlePing(Discv5Session session, byte[] body, IPEndPoint from)
        {
            if (!TryDecode(body, Discv5MessageEncoder.DecodePing, out var ping)) return;
            if (!IsValidRequestId(ping.RequestId, allowNull: false)) return;

            var pong = new Discv5PongMessage
            {
                RequestId = ping.RequestId,
                EnrSeq = LocalEnrSequence,
                RecipientIp = from.Address.GetAddressBytes(),
                RecipientPort = (ushort)from.Port
            };
            SendOrdinary(session, Discv5MessageEncoder.EncodePong(pong));
        }

        private void HandlePong(Discv5Session session, byte[] body)
        {
            if (!TryDecode(body, Discv5MessageEncoder.DecodePong, out var pong)) return;
            if (!IsValidRequestId(pong.RequestId, allowNull: false)) return;
            _requestTracker.CompletePong(session.RemoteNodeId, pong);
        }

        private void HandleNodes(Discv5Session session, byte[] body)
        {
            if (!TryDecode(body, Discv5MessageEncoder.DecodeNodes, out var nodes)) return;
            if (!IsValidRequestId(nodes.RequestId, allowNull: false)) return;
            _requestTracker.CompleteNodesChunk(session.RemoteNodeId, nodes);

            if (nodes.Records == null) return;
            foreach (var encoded in nodes.Records)
            {
                if (encoded == null || encoded.Length == 0) continue;
                if (!TryDecode(encoded, EnrRecordEncoder.Decode, out var enr)) continue;
                if (enr.Secp256k1 == null) continue;
                var nodeId = Discv5Crypto.ComputeNodeId(enr.Secp256k1);
                var ip = enr.IP4;
                var udpPort = enr.UdpPort;
                if (ip == null || udpPort == null) continue;
                _routingTable.Upsert(new Discv5RoutingTable.Entry
                {
                    NodeId = nodeId,
                    Address = new IPEndPoint(ip, udpPort.Value),
                    EnrEncoded = encoded
                });
            }
        }

        private void HandleFindNode(Discv5Session session, byte[] body)
        {
            if (!TryDecode(body, Discv5MessageEncoder.DecodeFindNode, out var req)) return;
            if (!IsValidRequestId(req.RequestId, allowNull: false)) return;

            var records = CollectFindNodeRecords(req.Distances);
            var chunks = PackNodesChunks(records);

            var total = chunks.Count > byte.MaxValue ? byte.MaxValue : (byte)chunks.Count;
            foreach (var chunk in chunks)
            {
                var resp = new Discv5NodesMessage
                {
                    RequestId = req.RequestId,
                    Total = total,
                    Records = chunk
                };
                SendOrdinary(session, Discv5MessageEncoder.EncodeNodes(resp));
            }
        }

        private List<byte[]> CollectFindNodeRecords(List<ulong> distances)
        {
            var records = new List<byte[]>();
            if (distances == null) return records;
            foreach (var d in distances)
            {
                if (records.Count >= MaxNodesResponseTotal) break;
                if (d == 0)
                {
                    if (LocalEnrEncoded != null) records.Add(LocalEnrEncoded);
                    continue;
                }
                foreach (var entry in _routingTable.AtDistance((uint)d))
                {
                    if (records.Count >= MaxNodesResponseTotal) break;
                    records.Add(entry.EnrEncoded);
                }
            }
            return records;
        }

        public static List<List<byte[]>> PackNodesChunks(List<byte[]> records)
        {
            var chunks = new List<List<byte[]>>();
            if (records.Count == 0)
            {
                chunks.Add(new List<byte[]>());
                return chunks;
            }
            var current = new List<byte[]>();
            int size = 0;
            foreach (var r in records)
            {
                var len = r?.Length ?? 0;
                if (current.Count > 0 && size + len > NodesRecordsBudgetBytes)
                {
                    chunks.Add(current);
                    current = new List<byte[]>();
                    size = 0;
                }
                current.Add(r);
                size += len;
            }
            if (current.Count > 0) chunks.Add(current);
            return chunks;
        }

        private void HandleTalkReq(Discv5Session session, byte[] body)
        {
            if (!TryDecode(body, Discv5MessageEncoder.DecodeTalkReq, out var req)) return;
            if (!IsValidRequestId(req.RequestId, allowNull: true)) return;

            byte[] response = Array.Empty<byte>();
            if (req.Protocol != null && req.Protocol.Length > 0)
            {
                var protocolKey = System.Text.Encoding.ASCII.GetString(req.Protocol);
                if (_talkHandlers.TryGetValue(protocolKey, out var handler))
                {
                    try
                    {
                        var handlerResponse = handler(req.Request ?? Array.Empty<byte>(), session.RemoteAddr);
                        if (handlerResponse != null) response = handlerResponse;
                    }
                    catch (Exception) { }
                }
            }

            var resp = new Discv5TalkRespMessage
            {
                RequestId = req.RequestId ?? Array.Empty<byte>(),
                Response = response
            };
            SendOrdinary(session, Discv5MessageEncoder.EncodeTalkResp(resp));
        }

        private void SendOrdinary(Discv5Session session, byte[] messagePlaintext)
        {
            var packet = _sessionManager.BuildOrdinaryPacket(session, messagePlaintext);
            try { _udp.Send(packet, packet.Length, session.RemoteAddr); }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }
    }
}
