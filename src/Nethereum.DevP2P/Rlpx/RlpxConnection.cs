using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Nethereum.DevP2P.Common;
using Nethereum.DevP2P.Crypto;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.DevP2P.Rlpx
{
    public class RlpxConnection : IDisposable
    {
        private readonly EthECKey _localKey;
        private readonly DevP2PConfig _config;
        private TcpClient _tcp;
        private NetworkStream _stream;
        private RlpxFrameWriter _writer;
        private RlpxFrameReader _reader;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly SemaphoreSlim _readLock = new(1, 1);
        // Client request/response demultiplexer. When SendRequestAsync is used the
        // connection runs a single background read loop (DispatcherLoopAsync) that delivers
        // each reply to the waiting request by request id, and routes unsolicited gossip to
        // the push channel. Mutually exclusive on a connection with the pull-based
        // ReceiveMessageAsync that server sessions use.
        private readonly ConcurrentDictionary<ulong, PendingRequest> _pendingRequests = new();
        private Task _dispatcherTask;
        private CancellationTokenSource _dispatcherCts;
        private readonly object _dispatcherLock = new object();
        private long _nextRequestId;
        private Timer _pingTimer;

        private sealed class PendingRequest
        {
            public readonly int ExpectedMsgId;
            public readonly TaskCompletionSource<byte[]> Tcs =
                new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            public PendingRequest(int expectedMsgId) => ExpectedMsgId = expectedMsgId;
        }

        /// <summary>
        /// Capacity for the per-connection push-message back-pressure channel.
        /// The eth wire spec
        /// (https://github.com/ethereum/devp2p/blob/master/caps/eth.md) treats
        /// unsolicited pushes (NewBlock, NewBlockHashes, Transactions,
        /// NewPooledTransactionHashes, BlockRangeUpdate) as fire-and-forget.
        /// A subscriber that takes long enough to overflow this queue is
        /// presumed to be slow, not stuck; the oldest pending push is dropped
        /// rather than blocking the read loop or growing memory unbounded.
        /// </summary>
        private const int PushChannelCapacity = 1024;

        private Channel<RlpxPushMessageEventArgs>? _pushChannel;
        private Task? _pushPumpTask;
        private CancellationTokenSource? _pushPumpCts;
        private readonly object _pushPumpLock = new object();

        public bool IsConnected { get; private set; }
        public HelloMessage RemoteHello { get; private set; }
        public List<P2PCapability> SharedCapabilities { get; private set; }
        public byte[] RemoteNodeId { get; private set; }
        public string RemoteEndpoint { get; private set; }

        /// <summary>
        /// Fires for any incoming sub-protocol message that arrives during a
        /// RequestAsync wait but is NOT the expected response (i.e. unsolicited
        /// pushes such as NewBlock, NewBlockHashes, Transactions). Without
        /// subscribing to this event, such messages are silently dropped.
        /// Continuous reception when no Request is in flight requires a
        /// separate background ReceiveMessageAsync loop on the caller side.
        /// </summary>
        public event EventHandler<RlpxPushMessageEventArgs> PushMessageReceived;

        /// <summary>
        /// Fires exactly once when this connection transitions from
        /// <see cref="IsConnected"/>=true to false — disconnect-from-our-side
        /// (<see cref="DisconnectAsync"/>), peer-initiated disconnect, or
        /// transport error during a read/write. Used by <see cref="RlpxListener"/>
        /// to decrement its admitted-peer counter; consumers can use it to
        /// trigger reconnect logic. Idempotent — subsequent disconnects
        /// raise nothing.
        /// </summary>
        public event EventHandler Disconnected;

        private int _disconnectedRaised; // 0 = not yet, 1 = raised
        private void MarkDisconnected()
        {
            IsConnected = false;
            if (Interlocked.Exchange(ref _disconnectedRaised, 1) != 0) return;
            var handler = Disconnected;
            if (handler == null) return;
            try { handler.Invoke(this, EventArgs.Empty); }
            catch { /* never let subscriber exceptions tear down the I/O path */ }
        }

        public RlpxConnection(EthECKey localKey, DevP2PConfig config = null)
        {
            _localKey = localKey;
            _config = config ?? new DevP2PConfig();
        }

        public async Task ConnectAsync(string host, int port, byte[] remotePubNoPrefix,
            CancellationToken ct = default)
        {
            RemoteEndpoint = $"{host}:{port}";

            // Self-connection check: skip the TCP/handshake roundtrip entirely
            // if the enode points at us. Cheaper than discovering it after the
            // Hello exchange.
            if (ByteUtil.AreEqual(remotePubNoPrefix, _localKey.GetPubKeyNoPrefix()))
                throw new InvalidOperationException(
                    "self-connection: target NodeId equals local NodeId");

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(_config.ConnectTimeoutMs);

            // NetRestrict outbound gate: resolve the target host so a CIDR
            // allow-list match can be made against the actual peer IP, not
            // the (possibly DNS) hostname string. With an empty NetRestrict
            // (default), Contains returns true and we connect unchanged. A
            // hostname that resolves to multiple addresses is filtered down
            // to those inside the allow-list; if none match, the dial is
            // skipped via RlpxNetRestrictedException so the caller can mark
            // the peer unreachable without a TCP roundtrip.
            var targetIps = await ResolveTargetIpsAsync(host, connectCts.Token).ConfigureAwait(false);
            if (_config.NetRestrict.Count > 0)
            {
                var allowed = new List<IPAddress>(targetIps.Count);
                foreach (var ip in targetIps)
                {
                    if (_config.NetRestrict.Contains(ip)) allowed.Add(ip);
                }
                if (allowed.Count == 0)
                    throw new RlpxNetRestrictedException(host, targetIps);
                targetIps = allowed;
            }

            _tcp = new TcpClient();
            await _tcp.ConnectAsync(targetIps.ToArray(), port, connectCts.Token);
            _stream = _tcp.GetStream();
            RemoteNodeId = remotePubNoPrefix;

            using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            handshakeCts.CancelAfter(_config.HandshakeTimeoutMs);

            var (authPacket, state) = RlpxHandshake.CreateAuth(_localKey, remotePubNoPrefix);
            await _stream.WriteAsync(authPacket, handshakeCts.Token);

            var ackSizeBytes = new byte[2];
            await ReadExactAsync(_stream, ackSizeBytes, handshakeCts.Token);
            var ackSize = (ackSizeBytes[0] << 8) | ackSizeBytes[1];
            if (ackSize < RlpxHandshake.MinHandshakePacketSize ||
                ackSize > RlpxHandshake.MaxHandshakePacketSize)
            {
                throw new System.Security.Cryptography.CryptographicException(
                    $"RLPx ack size {ackSize} out of range [{RlpxHandshake.MinHandshakePacketSize}, {RlpxHandshake.MaxHandshakePacketSize}]");
            }
            var ackBody = new byte[ackSize];
            await ReadExactAsync(_stream, ackBody, handshakeCts.Token);

            var ackPacket = ackSizeBytes.ConcatArrays(ackBody);
            var secrets = RlpxHandshake.HandleAck(state, ackPacket);

            _writer = new RlpxFrameWriter(secrets.AesSecret, secrets.MacSecret, secrets.EgressMac);
            _reader = new RlpxFrameReader(secrets.AesSecret, secrets.MacSecret, secrets.IngressMac);

            var localHello = new HelloMessage
            {
                ProtocolVersion = 5,
                ClientId = _config.ClientId,
                // Advertise only the eth versions in wide production use today:
                // ETH68/ETH69. ETH70 is a speculative spec change in
                // ethereum/devp2p (not yet shipped by major clients as of 2026);
                // advertising it forces no peer to negotiate it and risks
                // rejection by strict implementations.
                Capabilities = new List<P2PCapability>
                {
                    new() { Name = "eth", Version = 68 },
                    new() { Name = "eth", Version = 69 },
                    new() { Name = "snap", Version = 1 }
                },
                ListenPort = 0,
                NodeId = _localKey.GetPubKeyNoPrefix()
            };

            await SendFrameAsync(P2PMessageIds.Hello, HelloMessageEncoder.Encode(localHello), handshakeCts.Token);

            var (remoteMsgId, remotePayload) = await ReadFrameAsync(handshakeCts.Token);
            if (remoteMsgId == P2PMessageIds.Disconnect)
            {
                var reason = DecodeDisconnectReason(remotePayload);
                throw new RlpxPeerRejectedException(reason);
            }
            if (remoteMsgId != P2PMessageIds.Hello)
            {
                // Mark IsConnected briefly so DisconnectAsync can emit the
                // typed reason over the session keys we already derived. Peer
                // learns we hung up over ProtocolBreach, not a TCP RST.
                IsConnected = true;
                try { await DisconnectAsync(DisconnectReason.ProtocolBreach); } catch { }
                throw new InvalidOperationException(
                    $"Expected Hello (0x00), got 0x{remoteMsgId:x2}");
            }

            RemoteHello = HelloMessageEncoder.Decode(remotePayload);
            SharedCapabilities = CapabilityNegotiator.Negotiate(
                localHello.Capabilities, RemoteHello.Capabilities);

            if (SharedCapabilities.Count == 0)
            {
                IsConnected = true;
                try { await DisconnectAsync(DisconnectReason.UselessPeer); } catch { }
                throw new InvalidOperationException("No shared capabilities");
            }

            IsConnected = true;
            _pingTimer = new Timer(_ => _ = SafePingAsync(), null,
                _config.PingIntervalMs, _config.PingIntervalMs);
        }

        /// <summary>
        /// Decode a Disconnect message payload. Geth sends two flavours:
        /// the spec form <c>[reason_byte]</c> (RLP list of one element), and
        /// the historical short form <c>reason_byte</c> (single byte). We
        /// accept both and return <see cref="DisconnectReason.Requested"/>
        /// for an empty payload (rare but seen in the wild).
        /// </summary>
        private static DisconnectReason DecodeDisconnectReason(byte[] payload)
        {
            if (payload == null || payload.Length == 0) return DisconnectReason.Requested;
            try
            {
                var decoded = Nethereum.RLP.RLP.Decode(payload);
                if (decoded is Nethereum.RLP.RLPCollection coll && coll.Count > 0)
                {
                    var data = coll[0].RLPData;
                    if (data == null || data.Length == 0) return DisconnectReason.Requested;
                    return (DisconnectReason)data[0];
                }
                if (decoded is Nethereum.RLP.RLPItem item && item.RLPData != null && item.RLPData.Length > 0)
                    return (DisconnectReason)item.RLPData[0];
            }
            catch
            {
                // Fall through to raw-byte interpretation.
            }
            return (DisconnectReason)payload[0];
        }

        public async Task AcceptIncomingAsync(TcpClient acceptedTcp, CancellationToken ct = default)
        {
            _tcp = acceptedTcp;
            _stream = _tcp.GetStream();
            RemoteEndpoint = _tcp.Client.RemoteEndPoint?.ToString() ?? "unknown";

            using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            handshakeCts.CancelAfter(_config.HandshakeTimeoutMs);

            var authSizeBytes = new byte[2];
            await ReadExactAsync(_stream, authSizeBytes, handshakeCts.Token);
            var authSize = (authSizeBytes[0] << 8) | authSizeBytes[1];
            if (authSize < RlpxHandshake.MinHandshakePacketSize ||
                authSize > RlpxHandshake.MaxHandshakePacketSize)
            {
                throw new System.Security.Cryptography.CryptographicException(
                    $"RLPx auth size {authSize} out of range [{RlpxHandshake.MinHandshakePacketSize}, {RlpxHandshake.MaxHandshakePacketSize}]");
            }
            var authBody = new byte[authSize];
            await ReadExactAsync(_stream, authBody, handshakeCts.Token);
            var authPacket = authSizeBytes.ConcatArrays(authBody);

            var (ackPacket, state, secrets) = RlpxHandshake.HandleAuth(_localKey, authPacket);
            RemoteNodeId = state.RemotePubNoPrefix;

            // Self-connection: peer dialed us back at our own NodeId. This can
            // happen with a peer cache that contains our own enode or via NAT
            // loop-around. Reject before the Hello exchange.
            if (ByteUtil.AreEqual(RemoteNodeId, _localKey.GetPubKeyNoPrefix()))
            {
                IsConnected = true;
                try { await DisconnectAsync(DisconnectReason.ConnectedToSelf); } catch { }
                throw new InvalidOperationException(
                    "self-connection: inbound peer NodeId equals local NodeId");
            }

            await _stream.WriteAsync(ackPacket, handshakeCts.Token);

            _writer = new RlpxFrameWriter(secrets.AesSecret, secrets.MacSecret, secrets.EgressMac);
            _reader = new RlpxFrameReader(secrets.AesSecret, secrets.MacSecret, secrets.IngressMac);

            var localHello = new HelloMessage
            {
                ProtocolVersion = 5,
                ClientId = _config.ClientId,
                // Advertise only the eth versions in wide production use today:
                // ETH68/ETH69. ETH70 is a speculative spec change in
                // ethereum/devp2p (not yet shipped by major clients as of 2026);
                // advertising it forces no peer to negotiate it and risks
                // rejection by strict implementations.
                Capabilities = new List<P2PCapability>
                {
                    new() { Name = "eth", Version = 68 },
                    new() { Name = "eth", Version = 69 },
                    new() { Name = "snap", Version = 1 }
                },
                ListenPort = 0,
                NodeId = _localKey.GetPubKeyNoPrefix()
            };

            var (remoteMsgId, remotePayload) = await ReadFrameAsync(handshakeCts.Token);
            if (remoteMsgId != P2PMessageIds.Hello)
            {
                if (remoteMsgId == P2PMessageIds.Disconnect)
                    throw new RlpxPeerRejectedException(DecodeDisconnectReason(remotePayload));
                IsConnected = true;
                try { await DisconnectAsync(DisconnectReason.ProtocolBreach); } catch { }
                throw new InvalidOperationException(
                    $"Expected Hello (0x00), got 0x{remoteMsgId:x2}");
            }
            RemoteHello = HelloMessageEncoder.Decode(remotePayload);

            await SendFrameAsync(P2PMessageIds.Hello, HelloMessageEncoder.Encode(localHello), handshakeCts.Token);

            SharedCapabilities = CapabilityNegotiator.Negotiate(
                localHello.Capabilities, RemoteHello.Capabilities);

            if (SharedCapabilities.Count == 0)
            {
                IsConnected = true;
                try { await DisconnectAsync(DisconnectReason.UselessPeer); } catch { }
                throw new InvalidOperationException("No shared capabilities");
            }

            IsConnected = true;
            _pingTimer = new Timer(_ => _ = SafePingAsync(), null,
                _config.PingIntervalMs, _config.PingIntervalMs);
        }

        public int GetCapabilityOffset(string name)
        {
            var cap = SharedCapabilities.Find(c => c.Name == name);
            if (cap == null) throw new InvalidOperationException($"Capability '{name}' not shared");
            return cap.Offset;
        }

        public ulong NextRequestId() => (ulong)Interlocked.Increment(ref _nextRequestId);

        public async Task SendMessageAsync(int msgId, byte[] payload, CancellationToken ct = default)
        {
            await SendFrameAsync(msgId, payload, ct);
        }

        /// <summary>
        /// Outcome of <see cref="HandleControlFrameOrContinueAsync"/>: tells the
        /// caller how to react to a frame that arrived on the read loop.
        /// </summary>
        private enum ControlFrameAction
        {
            /// <summary>Frame was a recognised control frame (Ping/Pong) and was handled — caller should loop and read the next frame.</summary>
            Continue,
            /// <summary>Frame was a Disconnect — caller must surface a connection-closed error.</summary>
            PeerDisconnected,
            /// <summary>Frame is a sub-protocol message — caller should process it.</summary>
            PassThrough
        }

        /// <summary>
        /// Single decision point for Ping/Pong/Disconnect frames. The RLPx base
        /// protocol (https://github.com/ethereum/devp2p/blob/master/rlpx.md)
        /// requires answering each Ping (0x02) with a Pong (0x03) and treating a
        /// Disconnect (0x01) as connection termination. Ping/Pong cadence is NOT
        /// rate-limited. No inner read-deadline is reset on Ping/Pong: liveness is
        /// governed by useful sub-protocol frames within <see cref="DevP2PConfig.ReadTimeoutMs"/>.
        /// </summary>
        private async Task<ControlFrameAction> HandleControlFrameOrContinueAsync(
            int msgId, CancellationToken ct)
        {
            if (msgId == P2PMessageIds.Ping || msgId == P2PMessageIds.Pong)
            {
                // RLPx base-protocol keepalive: answer every Ping with a Pong and
                // ignore Pongs. Ping/Pong cadence is not rate-limited; liveness is
                // governed by the read-deadline on useful sub-protocol frames.
                if (msgId == P2PMessageIds.Ping)
                    await SendFrameAsync(P2PMessageIds.Pong, Array.Empty<byte>(), ct);

                return ControlFrameAction.Continue;
            }

            if (msgId == P2PMessageIds.Disconnect)
                return ControlFrameAction.PeerDisconnected;

            return ControlFrameAction.PassThrough;
        }

        public async Task<(int msgId, byte[] payload)> ReceiveMessageAsync(CancellationToken ct = default)
        {
            if (_dispatcherTask != null)
                throw new InvalidOperationException(
                    "Connection is in dispatched mode (SendRequestAsync); pull-based ReceiveMessageAsync is disabled on it.");
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_config.ReadTimeoutMs);

            while (true)
            {
                var (msgId, payload) = await ReadFrameAsync(timeoutCts.Token);

                var action = await HandleControlFrameOrContinueAsync(msgId, ct);
                if (action == ControlFrameAction.Continue) continue;
                if (action == ControlFrameAction.PeerDisconnected)
                {
                    MarkDisconnected();
                    throw new IOException("Peer disconnected");
                }

                return (msgId, payload);
            }
        }

        public async Task<(int msgId, byte[] payload)> RequestAsync(
            int requestMsgId, byte[] requestPayload,
            int expectedResponseMsgId, CancellationToken ct = default)
        {
            if (_dispatcherTask != null)
                throw new InvalidOperationException(
                    "Connection is in dispatched mode (SendRequestAsync); legacy RequestAsync is disabled on it.");
            await SendFrameAsync(requestMsgId, requestPayload, ct);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_config.RequestTimeoutMs);

            while (true)
            {
                var (msgId, payload) = await ReadFrameAsync(timeoutCts.Token);

                var action = await HandleControlFrameOrContinueAsync(msgId, ct);
                if (action == ControlFrameAction.Continue) continue;
                if (action == ControlFrameAction.PeerDisconnected)
                {
                    MarkDisconnected();
                    throw new IOException("Peer disconnected");
                }

                if (msgId == expectedResponseMsgId)
                    return (msgId, payload);

                // Unsolicited message arrived during the request wait. Surface
                // it via the push event so callers can subscribe instead of
                // dropping it on the floor. Routed through a bounded
                // back-pressure channel so a slow subscriber cannot block the
                // read loop or grow memory unbounded.
                EnqueuePush(new RlpxPushMessageEventArgs(msgId, payload));
            }
        }

        private void EnqueuePush(RlpxPushMessageEventArgs args)
        {
            if (PushMessageReceived == null) return;
            EnsurePushPumpStarted();
            var channel = _pushChannel;
            if (channel == null) return;
            // DropOldest writer never blocks: TryWrite always succeeds when the
            // channel is bounded with FullMode=DropOldest, returning false only
            // if the channel is closed.
            channel.Writer.TryWrite(args);
        }

        /// <summary>
        /// Send a request and await the reply matched by <paramref name="requestId"/> through
        /// the single background dispatcher loop. Unsolicited frames arriving meanwhile are
        /// routed to <see cref="PushMessageReceived"/>. Calling this puts the connection in
        /// dispatched mode — mutually exclusive with the pull-based <see cref="ReceiveMessageAsync"/>
        /// and <see cref="RequestAsync"/> on the same connection.
        /// </summary>
        public async Task<byte[]> SendRequestAsync(
            int sendMsgId, byte[] payload, int expectedResponseMsgId, ulong requestId,
            TimeSpan timeout, CancellationToken ct = default)
        {
            EnsureDispatcherStarted();
            var pending = new PendingRequest(expectedResponseMsgId);
            if (!_pendingRequests.TryAdd(requestId, pending))
                throw new InvalidOperationException($"Duplicate in-flight request id {requestId}");
            try
            {
                await SendFrameAsync(sendMsgId, payload, ct).ConfigureAwait(false);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(timeout);
                using (timeoutCts.Token.Register(() =>
                {
                    if (ct.IsCancellationRequested) pending.Tcs.TrySetCanceled(ct);
                    else pending.Tcs.TrySetException(new TimeoutException(
                        $"No response (msgId=0x{expectedResponseMsgId:x2}) for request {requestId} within {timeout.TotalSeconds:0}s"));
                }))
                {
                    return await pending.Tcs.Task.ConfigureAwait(false);
                }
            }
            finally
            {
                _pendingRequests.TryRemove(requestId, out _);
            }
        }

        private void EnsureDispatcherStarted()
        {
            if (_dispatcherTask != null) return;
            lock (_dispatcherLock)
            {
                if (_dispatcherTask != null) return;
                _dispatcherCts = new CancellationTokenSource();
                _dispatcherTask = Task.Run(() => DispatcherLoopAsync(_dispatcherCts.Token));
            }
        }

        /// <summary>
        /// The connection's single reader in dispatched mode. Ping/Pong/Disconnect are
        /// handled inline; a sub-protocol reply is delivered to the waiting request by
        /// request id; anything else is unsolicited gossip routed to the push channel.
        /// </summary>
        private async Task DispatcherLoopAsync(CancellationToken ct)
        {
            Exception fault;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int msgId; byte[] payload;
                    using (var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        readCts.CancelAfter(_config.ReadTimeoutMs);
                        (msgId, payload) = await ReadFrameAsync(readCts.Token).ConfigureAwait(false);
                    }

                    var action = await HandleControlFrameOrContinueAsync(msgId, ct).ConfigureAwait(false);
                    if (action == ControlFrameAction.Continue) continue;
                    if (action == ControlFrameAction.PeerDisconnected)
                    {
                        MarkDisconnected();
                        throw new IOException("Peer disconnected");
                    }

                    if (Nethereum.RLP.RLP.TryReadLeadingListUInt64(payload, out var reqId)
                        && _pendingRequests.TryGetValue(reqId, out var pending)
                        && pending.ExpectedMsgId == msgId)
                    {
                        _pendingRequests.TryRemove(reqId, out _);
                        pending.Tcs.TrySetResult(payload);
                    }
                    else
                    {
                        EnqueuePush(new RlpxPushMessageEventArgs(msgId, payload));
                    }
                }
                fault = new IOException("connection read loop ended");
            }
            catch (Exception ex)
            {
                fault = ex;
            }
            FaultAllPending(fault);
        }

        private void FaultAllPending(Exception ex)
        {
            foreach (var reqId in _pendingRequests.Keys)
                if (_pendingRequests.TryRemove(reqId, out var pending))
                    pending.Tcs.TrySetException(ex);
        }

        private void EnsurePushPumpStarted()
        {
            if (_pushPumpTask != null) return;
            lock (_pushPumpLock)
            {
                if (_pushPumpTask != null) return;
                _pushChannel = Channel.CreateBounded<RlpxPushMessageEventArgs>(
                    new BoundedChannelOptions(PushChannelCapacity)
                    {
                        FullMode = BoundedChannelFullMode.DropOldest,
                        SingleReader = true,
                        SingleWriter = true
                    });
                _pushPumpCts = new CancellationTokenSource();
                _pushPumpTask = Task.Run(() => PushPumpAsync(_pushChannel!.Reader, _pushPumpCts!.Token));
            }
        }

        private async Task PushPumpAsync(ChannelReader<RlpxPushMessageEventArgs> reader, CancellationToken ct)
        {
            try
            {
                while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
                {
                    while (reader.TryRead(out var args))
                    {
                        var handler = PushMessageReceived;
                        if (handler == null) continue;
                        try { handler.Invoke(this, args); }
                        catch { /* subscriber bug — wire stays alive */ }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        public class RlpxPushMessageEventArgs : EventArgs
        {
            public int MessageId { get; }
            public byte[] Payload { get; }
            public RlpxPushMessageEventArgs(int msgId, byte[] payload)
            {
                MessageId = msgId;
                Payload = payload;
            }
        }

        public async Task DisconnectAsync(DisconnectReason reason = DisconnectReason.ClientQuitting)
        {
            try
            {
                var payload = Nethereum.RLP.RLP.EncodeList(
                    Nethereum.RLP.RLP.EncodeElement(new[] { (byte)reason }));
                await SendFrameAsync(P2PMessageIds.Disconnect, payload);
            }
            catch { }
            finally
            {
                MarkDisconnected();
                Dispose();
            }
        }

        private async Task SafePingAsync()
        {
            // Outer catch guards against rare exceptions from the disconnect path
            // (timer disposal, state mutation) so the Timer thread never surfaces
            // an unobserved Task fault to the AppDomain.
            try { await SendPingAsync(); }
            catch { /* swallow — connection is already torn down */ }
        }

        private async Task SendPingAsync()
        {
            if (!IsConnected) { _pingTimer?.Dispose(); return; }
            try
            {
                await SendFrameAsync(P2PMessageIds.Ping, Nethereum.RLP.RLP.EncodeList());
            }
            catch { MarkDisconnected(); _pingTimer?.Dispose(); }
        }

        private async Task SendFrameAsync(int msgId, byte[] payload, CancellationToken ct = default)
        {
            await _writeLock.WaitAsync(ct);
            try
            {
                var frame = _writer.WriteFrame(msgId, payload);
                await _stream.WriteAsync(frame, ct);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private async Task<(int msgId, byte[] payload)> ReadFrameAsync(CancellationToken ct = default)
        {
            await _readLock.WaitAsync(ct);
            try
            {
                var headerBlock = new byte[32];
                await ReadExactAsync(_stream, headerBlock, ct);

                var frameSize = _reader.ReadHeader(headerBlock);
                var framePaddedSize = ((frameSize + 15) / 16) * 16;

                var bodyBlock = new byte[framePaddedSize + 16];
                await ReadExactAsync(_stream, bodyBlock, ct);

                return _reader.ReadBody(frameSize, bodyBlock);
            }
            finally
            {
                _readLock.Release();
            }
        }

        private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct = default)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = await stream.ReadAsync(buffer, offset, buffer.Length - offset, ct);
                if (read == 0) throw new IOException("Connection closed");
                offset += read;
            }
        }

        /// <summary>
        /// Resolve <paramref name="host"/> to one or more <see cref="IPAddress"/>
        /// entries, treating an IP literal as its own single-element result.
        /// Used by the outbound dial path so the NetRestrict CIDR check can
        /// run against the actual peer IP before any TCP work begins.
        /// </summary>
        private static async Task<List<IPAddress>> ResolveTargetIpsAsync(string host, CancellationToken ct)
        {
            if (IPAddress.TryParse(host, out var literal))
                return new List<IPAddress> { literal };

            var addrs = await System.Net.Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);
            return new List<IPAddress>(addrs);
        }

        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            MarkDisconnected();
            try { _dispatcherCts?.Cancel(); } catch { }
            FaultAllPending(new ObjectDisposedException(nameof(RlpxConnection)));
            try { _pushPumpCts?.Cancel(); } catch { }
            try { _pushChannel?.Writer.TryComplete(); } catch { }
            _pingTimer?.Dispose();
            _stream?.Dispose();
            _tcp?.Dispose();
            _writeLock.Dispose();
            _readLock.Dispose();
            try { _pushPumpCts?.Dispose(); } catch { }
            try { _dispatcherCts?.Dispose(); } catch { }
        }
    }

    /// <summary>
    /// Thrown when a peer responds to our Hello with a Disconnect message
    /// (or sends a Disconnect before we even sent Hello). Carries the
    /// <see cref="DisconnectReason"/> so the caller can decide whether to
    /// retry with a different peer or surface the rejection to the user.
    /// </summary>
    public sealed class RlpxPeerRejectedException : Exception
    {
        public DisconnectReason Reason { get; }
        public RlpxPeerRejectedException(DisconnectReason reason)
            : base($"Peer rejected RLPx handshake with Disconnect(reason={(byte)reason:x2} {reason})")
        {
            Reason = reason;
        }
    }

    /// <summary>
    /// Thrown by <see cref="RlpxConnection"/> when a peer exceeds the per-peer
    /// Ping/Pong frame budget enforced inside the read loop. The connection is
    /// closed with <see cref="Nethereum.Model.P2P.DisconnectReason.ProtocolBreach"/>
    /// before this exception is surfaced so the peer learns the reason for the
    /// hang-up over the established session keys.
    /// </summary>
    public sealed class RlpxPingFloodException : IOException
    {
        public RlpxPingFloodException()
            : base("Peer exceeded RLPx Ping/Pong rate limit")
        {
        }
    }

    /// <summary>
    /// Thrown by <see cref="RlpxConnection.ConnectAsync"/> when none of the
    /// target host's resolved IP addresses fall inside the configured
    /// <see cref="Nethereum.DevP2P.Netutil.NetRestrict"/> allow-list. Mirrors
    /// the standard NetRestrict outbound rejection. The
    /// dialer should treat the peer as unreachable for the run rather than
    /// retry, since the configured restriction is static for the process.
    /// </summary>
    public sealed class RlpxNetRestrictedException : Exception
    {
        public string Host { get; }
        public IReadOnlyList<IPAddress> ResolvedAddresses { get; }

        public RlpxNetRestrictedException(string host, IReadOnlyList<IPAddress> resolvedAddresses)
            : base($"Outbound dial to '{host}' blocked: none of its {resolvedAddresses.Count} resolved address(es) match NetRestrict allow-list.")
        {
            Host = host;
            ResolvedAddresses = resolvedAddresses;
        }
    }
}
