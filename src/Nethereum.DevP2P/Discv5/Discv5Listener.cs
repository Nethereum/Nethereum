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
    /// <summary>
    /// discv5 UDP socket loop with auto-respond enabled. Drives the session
    /// state machine (<see cref="Discv5SessionManager"/>), answers Ping,
    /// FindNode and TalkReq messages, maintains a Kademlia routing table
    /// (<see cref="Discv5RoutingTable"/>), and reciprocally pings newly-handshaked
    /// peers so they admit us to their tables.
    /// </summary>
    public class Discv5Listener : IDisposable, IAsyncDisposable
    {
        /// <summary>How long <see cref="Dispose"/> waits for the read loop to drain before forcing teardown.</summary>
        public const int DisposeTimeoutMs = 250;

        /// <summary>Per <c>discv5-wire.md §"Common Message Types"</c>: request IDs are 0–8 bytes.</summary>
        public const int MaxRequestIdLength = 8;

        /// <summary>
        /// Delay before sending the reciprocal Ping that follows a successful handshake.
        /// A peer that has just received our WHOAREYOU reply expects its original
        /// Ping's Pong as the next packet from us; sending the reciprocal Ping ahead
        /// of that Pong can race the peer's response demultiplexer. ~200 ms guarantees
        /// the Pong lands first on local links while staying inside typical peer read
        /// deadlines.
        /// </summary>
        public const int ReciprocalPingDelayMs = 200;

        /// <summary>
        /// Maximum total ENR records a single FINDNODE handler returns across all
        /// NODES packets. Peers enforce a cumulative cap on the responses they will
        /// accept for one FINDNODE call; sending more than this is wasted bandwidth
        /// and risks the peer dropping packets past the threshold.
        /// </summary>
        public const int MaxNodesResponseTotal = 16;

        /// <summary>
        /// Soft byte-size budget for the encoded ENR records packed into one NODES
        /// message. discv5 packets are capped at 1280 bytes — leaving headroom for
        /// the type byte, request-id, total field, RLP list framing, and the outer
        /// packet header yields a safe ~900-byte budget for the records list itself.
        /// </summary>
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

        /// <summary>
        /// Inbound discovery datagrams dropped by the per-source-IP rate limiter
        /// or the banned-IP LRU before any crypto work. Test/diagnostic surface.
        /// </summary>
        public long DroppedInboundCount => Interlocked.Read(ref _droppedInboundCount);

        /// <summary>
        /// True when <paramref name="ip"/> is currently in the banned-IP LRU and
        /// inbound traffic from it is being short-circuited before the rate
        /// limiter. Test/diagnostic surface.
        /// </summary>
        public bool IsBanned(IPAddress ip) => ip != null && _bannedIps.ContainsKey(ip);

        /// <summary>Tracks outbound Ping / FindNode requests awaiting Pong / Nodes responses.</summary>
        public Discv5RequestTracker RequestTracker => _requestTracker;

        /// <summary>Kademlia routing table populated as peers successfully handshake with us.</summary>
        public Discv5RoutingTable Routing => _routingTable;

        /// <summary>RLP-encoded ENR record for this node — required for FindNode(distance=0) self-return.</summary>
        public byte[] LocalEnrEncoded { get; set; }

        /// <summary>Local ENR sequence number — echoed back to peers in our Pong responses.</summary>
        public ulong LocalEnrSequence { get; set; } = 1;

        /// <summary>UDP endpoint we're bound to, or null before <see cref="Start"/>.</summary>
        public IPEndPoint LocalEndpoint => (IPEndPoint)_udp?.Client?.LocalEndPoint;

        /// <summary>Bound UDP port, or 0 before <see cref="Start"/>.</summary>
        public int Port => LocalEndpoint?.Port ?? 0;

        /// <summary>32-byte discv5 node id (<c>keccak256(pubkey-x ‖ pubkey-y)</c>).</summary>
        public byte[] NodeId => _sessionManager.LocalNodeId;

        /// <summary>
        /// Register a handler for incoming TALKREQ messages addressed to the
        /// supplied <paramref name="protocol"/> id. The handler receives the
        /// raw request payload plus the peer endpoint and returns the response
        /// bytes to send back. Returning null surfaces as an empty TalkResp.
        /// Protocol identifiers are matched as ASCII byte strings per
        /// discv5-wire.md §"TALKREQ message" — internally keyed by the
        /// UTF-8 string form so look-up stays cheap.
        /// </summary>
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
            catch (Exception) { /* swallow on disposal */ }
        }

        public void Dispose()
        {
            // Sync IDisposable for `using` ergonomics — block bounded by DisposeTimeoutMs.
            // Production callers should prefer `await using` / DisposeAsync.
            _cts?.Cancel();
            _udp?.Close();
            try { _readLoop?.Wait(DisposeTimeoutMs); }
            catch (AggregateException) { /* read loop already faulted */ }
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
                catch (Exception) { /* listener may already be stopping */ }
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

        /// <summary>
        /// Dial a peer with PING and await the matching PONG. If no session
        /// exists yet, kicks off the WHOAREYOU handshake — the original Ping
        /// payload is re-emitted under the established session key by the
        /// session manager.
        /// </summary>
        /// <param name="peer">UDP endpoint of the peer.</param>
        /// <param name="peerNodeId">32-byte discv5 node id of the peer.</param>
        /// <param name="peerStaticCompressedPubKey">33-byte compressed secp256k1 static pubkey from the peer's ENR. Required for ECDH if we need to handshake.</param>
        /// <param name="timeout">Request-level timeout.</param>
        /// <param name="ct">External cancellation.</param>
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

        /// <summary>
        /// Dial a peer with FINDNODE for the supplied <paramref name="distances"/>
        /// and await the aggregated NODES response. Distance 0 returns the peer's
        /// own ENR (self). Multi-packet NODES replies are reassembled by the
        /// request tracker using the <c>total</c> field in the first chunk.
        /// </summary>
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

            // Hint = 1 chunk; the peer's first reply rebinds to the authoritative total.
            var task = _requestTracker.RegisterFindNode(
                peerNodeId, requestId, expectedTotalHint: 1, timeout, ct);
            DispatchOutboundMessage(peer, peerNodeId, peerStaticCompressedPubKey, msg);
            return await task.ConfigureAwait(false);
        }

        /// <summary>
        /// Dial a peer with TALKREQ for an out-of-band sub-protocol and await
        /// the matching TALKRESP. If no session exists yet, the WHOAREYOU
        /// handshake runs first and the original TalkReq payload is re-emitted
        /// under the established session key.
        /// </summary>
        /// <param name="peer">UDP endpoint of the peer.</param>
        /// <param name="peerNodeId">32-byte discv5 node id of the peer.</param>
        /// <param name="peerStaticCompressedPubKey">33-byte compressed secp256k1 static pubkey from the peer's ENR. Required for ECDH if we need to handshake.</param>
        /// <param name="protocol">Sub-protocol identifier (ASCII per discv5-wire.md §"TALKREQ message"; max 8 bytes by spec).</param>
        /// <param name="payload">Opaque sub-protocol request bytes.</param>
        /// <param name="timeout">Request-level timeout.</param>
        /// <param name="ct">External cancellation.</param>
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
            catch (SocketException) { /* peer gone */ }
            catch (ObjectDisposedException) { /* listener stopping */ }
        }

        private byte[] NextOutboundRequestId()
        {
            // 8-byte counter-derived request id (spec max length). A monotonic counter
            // is cheaper than RNG and adequate for a server that never tracks responses
            // to its own outbound messages.
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

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                UdpReceiveResult result;
                try { result = await _udp.ReceiveAsync(ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
                catch (ObjectDisposedException) { return; }
                catch (SocketException)
                {
                    // A single ICMP port-unreachable surfaces as SocketException
                    // on UdpClient.ReceiveAsync but the socket remains usable.
                    // Returning here would silently kill discovery for the rest
                    // of the process lifetime. Brief backoff then continue —
                    // catastrophic failures (socket disposed) are caught above.
                    try { await Task.Delay(50, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                    continue;
                }

                // discv5-wire.md §"Packet Encoding" min/max packet sizes — drop
                // silently below the floor (cannot possibly parse) or above the
                // 1280-byte ceiling (cannot have been produced by a conformant
                // peer). Cheap fast-path rejection before the crypto layer.
                if (result.Buffer == null
                    || result.Buffer.Length < Discv5Packet.MinPacketSize
                    || result.Buffer.Length > Discv5Packet.MaxPacketSize)
                {
                    continue;
                }

                // Per-source-IP rate limit and banned-IP LRU run BEFORE any
                // session-layer work to bound the cost of a flood at well under
                // one core regardless of how the attacker frames the packet.
                // The discv5 wire spec does not mandate a numeric rate limit;
                // the design and constants are sigp/discv5-derived (see
                // Common/DevP2PRateLimitConstants).
                var srcIp = result.RemoteEndPoint?.Address;
                if (srcIp == null)
                {
                    continue;
                }
                if (_bannedIps.ContainsKey(srcIp))
                {
                    Interlocked.Increment(ref _droppedInboundCount);
                    continue;
                }
                if (!_inboundFilter.TryAcquire(srcIp))
                {
                    Interlocked.Increment(ref _droppedInboundCount);
                    AddBannedIp(srcIp);
                    continue;
                }

                var processed = _sessionManager.Process(result.Buffer, result.RemoteEndPoint);
                if (processed.Kind == Discv5SessionManager.IncomingPacketKind.NeedWhoAreYou)
                {
                    try { await _udp.SendAsync(processed.OutgoingBytes, processed.OutgoingBytes.Length, processed.Source).ConfigureAwait(false); }
                    catch (SocketException) { /* peer gone */ }
                    catch (ObjectDisposedException) { return; }
                    continue;
                }
                if (processed.Kind != Discv5SessionManager.IncomingPacketKind.Decoded) continue;

                try
                {
                    HandleDecodedMessage(processed.Session, processed.Message, processed.Source);
                }
                catch (Exception) { /* one peer's bad input must never kill the read loop */ }
            }
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

        private void HandleTalkResp(Discv5Session session, byte[] body)
        {
            Discv5TalkRespMessage resp;
            try { resp = Discv5MessageEncoder.DecodeTalkResp(body); }
            catch (Exception) { return; }
            if (resp.RequestId != null && resp.RequestId.Length > MaxRequestIdLength) return;
            _requestTracker.CompleteTalkResp(session.RemoteNodeId, resp);
        }

        private void HandlePing(Discv5Session session, byte[] body, IPEndPoint from)
        {
            Discv5PingMessage ping;
            try { ping = Discv5MessageEncoder.DecodePing(body); }
            catch (Exception) { return; }
            if (ping.RequestId == null || ping.RequestId.Length > MaxRequestIdLength) return;

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
            Discv5PongMessage pong;
            try { pong = Discv5MessageEncoder.DecodePong(body); }
            catch (Exception) { return; }
            if (pong.RequestId == null || pong.RequestId.Length > MaxRequestIdLength) return;
            _requestTracker.CompletePong(session.RemoteNodeId, pong);
        }

        private void HandleNodes(Discv5Session session, byte[] body)
        {
            Discv5NodesMessage nodes;
            try { nodes = Discv5MessageEncoder.DecodeNodes(body); }
            catch (Exception) { return; }
            if (nodes.RequestId == null || nodes.RequestId.Length > MaxRequestIdLength) return;
            _requestTracker.CompleteNodesChunk(session.RemoteNodeId, nodes);

            // Also upsert each received ENR into the routing table so subsequent
            // FindNode iterations have more peers to query.
            if (nodes.Records == null) return;
            foreach (var encoded in nodes.Records)
            {
                if (encoded == null || encoded.Length == 0) continue;
                EnrRecord enr;
                try { enr = EnrRecordEncoder.Decode(encoded); }
                catch (Exception) { continue; }
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
            Discv5FindNodeMessage req;
            try { req = Discv5MessageEncoder.DecodeFindNode(body); }
            catch (Exception) { return; }
            if (req.RequestId == null || req.RequestId.Length > MaxRequestIdLength) return;

            var records = CollectFindNodeRecords(req.Distances);
            var chunks = PackNodesChunks(records);

            // total is a uint8 per discv5-wire.md §NODES — chunk count cannot
            // exceed 255. PackNodesChunks already bounds the record set, so
            // clamping defensively here just prevents overflow if its bounds
            // are ever relaxed.
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
                    // Distance 0 = "self" per discv5-wire.md §FINDNODE.
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
            // Empty result-set still emits one (zero-record) packet so the requester's
            // total counter doesn't sit at zero forever.
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
                // Start a new chunk only when adding this record would exceed the
                // soft byte budget AND the current chunk isn't empty (we always
                // pack at least one record per chunk so a single oversize ENR
                // still gets sent rather than dropped).
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
            Discv5TalkReqMessage req;
            try { req = Discv5MessageEncoder.DecodeTalkReq(body); }
            catch (Exception) { return; }
            // Per spec a zero-length request id is allowed — only reject if it exceeds the cap.
            if (req.RequestId != null && req.RequestId.Length > MaxRequestIdLength) return;

            byte[] response = Array.Empty<byte>();
            if (req.Protocol != null && req.Protocol.Length > 0)
            {
                // Treat protocol bytes as ASCII per discv5-wire.md §"TALKREQ message".
                var protocolKey = System.Text.Encoding.ASCII.GetString(req.Protocol);
                if (_talkHandlers.TryGetValue(protocolKey, out var handler))
                {
                    try
                    {
                        var handlerResponse = handler(req.Request ?? Array.Empty<byte>(), session.RemoteAddr);
                        if (handlerResponse != null) response = handlerResponse;
                    }
                    catch (Exception) { /* handler failures degrade to empty TalkResp */ }
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
            catch (SocketException) { /* peer gone or routing failure */ }
            catch (ObjectDisposedException) { /* listener stopping */ }
        }
    }
}
