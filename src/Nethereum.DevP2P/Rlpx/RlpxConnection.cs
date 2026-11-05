using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Common;
using Nethereum.DevP2P.Crypto;
using Nethereum.Documentation;
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
        private RlpxFrameChannel _frameChannel;
        private readonly RlpxRequestDispatcher _dispatcher;
        private long _nextRequestId;
        private Timer _pingTimer;

        private readonly RlpxPushChannel _pushChannel;
        private readonly InFlightDecodeLimiter _decodeLimiter;

        public bool IsConnected { get; private set; }
        public bool IsDisconnected => Volatile.Read(ref _disconnectedRaised) != 0;
        public HelloMessage RemoteHello { get; private set; }
        public List<P2PCapability> SharedCapabilities { get; private set; }
        public byte[] RemoteNodeId { get; private set; }
        public string RemoteEndpoint { get; private set; }

        private long _lastFrameReceivedUtcTicks = DateTime.UtcNow.Ticks;
        public DateTime LastFrameReceivedUtc =>
            new DateTime(Interlocked.Read(ref _lastFrameReceivedUtcTicks), DateTimeKind.Utc);
        private void StampLastFrameReceived() =>
            Interlocked.Exchange(ref _lastFrameReceivedUtcTicks, DateTime.UtcNow.Ticks);

        public event EventHandler<RlpxPushMessageEventArgs> PushMessageReceived;

        public event EventHandler Disconnected;

        private int _disconnectedRaised;
        public void MarkDisconnected()
        {
            IsConnected = false;
            if (Interlocked.Exchange(ref _disconnectedRaised, 1) != 0) return;
            var handler = Disconnected;
            if (handler == null) return;
            try { handler.Invoke(this, EventArgs.Empty); }
            catch { }
        }

        public RlpxConnection(EthECKey localKey, DevP2PConfig config = null)
        {
            _localKey = localKey;
            _config = config ?? new DevP2PConfig();
            _decodeLimiter = _config.DecodeLimiter ?? InFlightDecodeLimiter.Shared;
            _pushChannel = new RlpxPushChannel(args => PushMessageReceived?.Invoke(this, args), _decodeLimiter);
            _dispatcher = new RlpxRequestDispatcher(
                readFrame: ReadFrameAsync,
                sendFrame: SendFrameAsync,
                handleControlFrame: HandleControlFrameOrContinueAsync,
                onUnsolicited: (msgId, payload, ct) => EnqueuePushAsync(new RlpxPushMessageEventArgs(msgId, payload), ct),
                onDisconnected: MarkDisconnected,
                readTimeoutMs: () => _config.ReadTimeoutMs,
                abandonedTrackingMs: () => _config.AbandonedRequestTrackingMs,
                onFrameReceived: StampLastFrameReceived,
                isFrameAccepted: IsFrameAccepted,
                onProtocolError: () => DisconnectAsync(DisconnectReason.ProtocolBreach),
                isResponseMessage: IsResponseMessage,
                decodeLimiter: _decodeLimiter);
        }

        private bool IsResponseMessage(int msgId)
        {
            var shared = SharedCapabilities;
            if (shared == null || msgId < BaseProtocolLength) return false;
            foreach (var capability in shared)
                if (msgId >= capability.Offset && msgId < capability.Offset + capability.Length)
                    return RlpxResponseCodes.IsResponse(capability.Name, msgId - capability.Offset);
            return false;
        }

        private const int BaseProtocolLength = 0x10;
        public const int MaxSubProtocolMessageSize = 10 * 1024 * 1024;

        private bool IsFrameAccepted(int msgId, int payloadLength)
        {
            if (msgId < BaseProtocolLength) return true;
            var shared = SharedCapabilities;
            if (shared == null) return true;
            foreach (var capability in shared)
                if (msgId >= capability.Offset && msgId < capability.Offset + capability.Length)
                    return payloadLength <= MaxSubProtocolMessageSize
                           || (capability.Name != "eth" && capability.Name != "snap");
            return false;
        }

        [NethereumDocExample(DocSection.DevP2P, "devp2p", "RlpxConnection.ConnectAsync — outbound encrypted connection")]
        public async Task ConnectAsync(string host, int port, byte[] remotePubNoPrefix,
            CancellationToken ct = default)
        {
            RemoteEndpoint = $"{host}:{port}";

            if (ByteUtil.AreEqual(remotePubNoPrefix, _localKey.GetPubKeyNoPrefix()))
                throw new InvalidOperationException(
                    "self-connection: target NodeId equals local NodeId");

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(_config.ConnectTimeoutMs);

            var targetIps = await ResolveTargetIpsAsync(host, connectCts.Token).ConfigureAwait(false);
            targetIps = FilterByNetRestrictOrThrow(host, targetIps);

            _tcp = new TcpClient();
            await _tcp.ConnectAsync(targetIps.ToArray(), port, connectCts.Token);
            _stream = _tcp.GetStream();
            RemoteNodeId = remotePubNoPrefix;

            using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            handshakeCts.CancelAfter(_config.HandshakeTimeoutMs);

            var (authPacket, state) = RlpxHandshake.CreateAuth(_localKey, remotePubNoPrefix);
            await _stream.WriteAsync(authPacket, handshakeCts.Token);

            var ackPacket = await ReadSizePrefixedHandshakePacketAsync(_stream, "ack", handshakeCts.Token);
            var secrets = RlpxHandshake.HandleAck(state, ackPacket);

            _frameChannel = CreateFrameChannel(secrets);

            var localHello = BuildLocalHello();

            await SendFrameAsync(P2PMessageIds.Hello, HelloMessageEncoder.Encode(localHello), handshakeCts.Token);

            var (remoteMsgId, remotePayload) = await ReadFrameAsync(handshakeCts.Token);
            RemoteHello = await ExpectHelloOrRejectAsync(remoteMsgId, remotePayload);

            await CompleteHandshakeAsync(localHello);
        }

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
            }
            return (DisconnectReason)payload[0];
        }

        private HelloMessage BuildLocalHello()
        {
            var capabilities = new List<P2PCapability>();
            for (var ethVersion = 68; ethVersion <= 71 && ethVersion <= _config.MaxEthVersion; ethVersion++)
                capabilities.Add(new P2PCapability { Name = "eth", Version = ethVersion });
            if (_config.AdvertiseSnap1)
                capabilities.Add(new P2PCapability { Name = "snap", Version = 1 });

            if (_config.AdvertiseSnap2)
                capabilities.Add(new P2PCapability { Name = "snap", Version = 2 });

            return new HelloMessage
            {
                ProtocolVersion = 5,
                ClientId = _config.ClientId,
                Capabilities = capabilities,
                ListenPort = 0,
                NodeId = _localKey.GetPubKeyNoPrefix()
            };
        }

        private async Task<byte[]> ReadSizePrefixedHandshakePacketAsync(
            System.IO.Stream stream, string packetKind, CancellationToken cancellationToken)
        {
            var sizeBytes = new byte[2];
            await RlpxStreamReader.ReadExactlyAsync(stream, sizeBytes, cancellationToken);
            var size = (sizeBytes[0] << 8) | sizeBytes[1];
            if (size < RlpxHandshake.MinHandshakePacketSize || size > RlpxHandshake.MaxHandshakePacketSize)
                throw new System.Security.Cryptography.CryptographicException(
                    $"RLPx {packetKind} size {size} out of range [{RlpxHandshake.MinHandshakePacketSize}, {RlpxHandshake.MaxHandshakePacketSize}]");
            var body = new byte[size];
            await RlpxStreamReader.ReadExactlyAsync(stream, body, cancellationToken);
            return sizeBytes.ConcatArrays(body);
        }

        private async Task SendRejectDisconnectAsync(DisconnectReason reason)
        {
            IsConnected = true;
            try { await DisconnectAsync(reason); } catch { }
        }

        private async Task NegotiateSharedCapabilitiesOrDisconnectAsync(HelloMessage localHello)
        {
            SharedCapabilities = CapabilityNegotiator.Negotiate(localHello.Capabilities, RemoteHello.Capabilities);
            if (SharedCapabilities.Count == 0)
            {
                await SendRejectDisconnectAsync(DisconnectReason.UselessPeer);
                throw new InvalidOperationException("No shared capabilities");
            }
        }

        private async Task<HelloMessage> ExpectHelloOrRejectAsync(int remoteMsgId, byte[] remotePayload)
        {
            if (remoteMsgId == P2PMessageIds.Disconnect)
                throw new RlpxPeerRejectedException(DecodeDisconnectReason(remotePayload));
            if (remoteMsgId != P2PMessageIds.Hello)
            {
                await SendRejectDisconnectAsync(DisconnectReason.ProtocolBreach);
                throw new InvalidOperationException(
                    $"Expected Hello (0x00), got 0x{remoteMsgId:x2}");
            }
            HelloMessage hello;
            try
            {
                hello = HelloMessageEncoder.Decode(remotePayload);
            }
            catch (Exception ex)
            {
                await SendRejectDisconnectAsync(DisconnectReason.ProtocolBreach);
                throw new InvalidOperationException("Malformed Hello", ex);
            }

            var rejection = ValidateHello(hello);
            if (rejection.HasValue)
            {
                await SendRejectDisconnectAsync(rejection.Value);
                throw new InvalidOperationException($"Invalid Hello from peer ({rejection.Value})");
            }
            return hello;
        }

        private DisconnectReason? ValidateHello(HelloMessage hello)
        {
            if (hello.NodeId == null || hello.NodeId.Length != 64)
                return DisconnectReason.NullIdentity;
            if (!ByteUtil.AreEqual(hello.NodeId, RemoteNodeId))
                return DisconnectReason.UnexpectedIdentity;
            if (hello.Capabilities.Count > MaxHelloCapabilities
                || hello.ClientId.Length > MaxHelloClientIdLength)
                return DisconnectReason.ProtocolBreach;
            return null;
        }

        private const int MaxHelloCapabilities = 64;
        private const int MaxHelloClientIdLength = 256;

        private async Task CompleteHandshakeAsync(HelloMessage localHello)
        {
            _frameChannel.LiftHandshakeFrameLimit();
            await NegotiateSharedCapabilitiesOrDisconnectAsync(localHello);
            IsConnected = true;
            StartPingKeepalive();
        }

        private void StartPingKeepalive()
        {
            _pingTimer = new Timer(_ => _ = SafePingAsync(), null, _config.PingIntervalMs, _config.PingIntervalMs);
        }

        public async Task AcceptIncomingAsync(TcpClient acceptedTcp, CancellationToken ct = default)
        {
            _tcp = acceptedTcp;
            _stream = _tcp.GetStream();
            RemoteEndpoint = _tcp.Client.RemoteEndPoint?.ToString() ?? "unknown";

            using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            handshakeCts.CancelAfter(_config.HandshakeTimeoutMs);

            var authPacket = await ReadSizePrefixedHandshakePacketAsync(_stream, "auth", handshakeCts.Token);

            var (ackPacket, state, secrets) = RlpxHandshake.HandleAuth(_localKey, authPacket);
            RemoteNodeId = state.RemotePubNoPrefix;

            if (ByteUtil.AreEqual(RemoteNodeId, _localKey.GetPubKeyNoPrefix()))
            {
                await SendRejectDisconnectAsync(DisconnectReason.ConnectedToSelf);
                throw new InvalidOperationException(
                    "self-connection: inbound peer NodeId equals local NodeId");
            }

            await _stream.WriteAsync(ackPacket, handshakeCts.Token);

            _frameChannel = CreateFrameChannel(secrets);

            var localHello = BuildLocalHello();

            var (remoteMsgId, remotePayload) = await ReadFrameAsync(handshakeCts.Token);
            RemoteHello = await ExpectHelloOrRejectAsync(remoteMsgId, remotePayload);

            await SendFrameAsync(P2PMessageIds.Hello, HelloMessageEncoder.Encode(localHello), handshakeCts.Token);

            await CompleteHandshakeAsync(localHello);
        }

        private RlpxFrameChannel CreateFrameChannel(RlpxSecrets secrets)
        {
            return new RlpxFrameChannel(
                _stream,
                new RlpxFrameWriter(secrets.AesSecret, secrets.MacSecret, secrets.EgressMac),
                new RlpxFrameReader(secrets.AesSecret, secrets.MacSecret, secrets.IngressMac),
                _config.FrameWriteTimeoutMs);
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

        private async Task<RlpxControlFrameAction> HandleControlFrameOrContinueAsync(
            int msgId, CancellationToken ct)
        {
            if (msgId == P2PMessageIds.Ping || msgId == P2PMessageIds.Pong)
            {
                if (msgId == P2PMessageIds.Ping)
                    await SendFrameAsync(P2PMessageIds.Pong, Array.Empty<byte>(), ct);

                return RlpxControlFrameAction.Continue;
            }

            if (msgId == P2PMessageIds.Disconnect)
                return RlpxControlFrameAction.PeerDisconnected;

            return RlpxControlFrameAction.PassThrough;
        }

        private async Task<(int msgId, byte[] payload)> ReadNextSubProtocolFrameAsync(
            TimeSpan frameReadTimeout, CancellationToken readCt, CancellationToken controlCt)
        {
            while (true)
            {
                var (msgId, payload) = await ReadFrameWithinAsync(frameReadTimeout, readCt);

                if (!IsFrameAccepted(msgId, payload.Length))
                {
                    await DisconnectAsync(DisconnectReason.ProtocolBreach);
                    throw new IOException($"message id 0x{msgId:x} with {payload.Length} bytes is outside every negotiated capability or over its size cap");
                }

                var action = await HandleControlFrameOrContinueAsync(msgId, controlCt);
                if (action == RlpxControlFrameAction.Continue) continue;
                if (action == RlpxControlFrameAction.PeerDisconnected)
                {
                    MarkDisconnected();
                    throw new IOException("Peer disconnected");
                }

                return (msgId, payload);
            }
        }

        public TimeSpan FrameReadTimeout => TimeSpan.FromMilliseconds(_config.ReadTimeoutMs);

        public async Task<(int msgId, byte[] payload)> ReceiveMessageAsync(CancellationToken ct = default)
        {
            ThrowIfDispatched();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_config.ReadTimeoutMs);

            return await ReadNextSubProtocolFrameAsync(FrameReadTimeout, timeoutCts.Token, ct);
        }

        public async Task<(int msgId, byte[] payload)> ReceiveMessageAsync(
            TimeSpan frameReadTimeout, CancellationToken ct = default)
        {
            ThrowIfDispatched();
            return await ReadNextSubProtocolFrameAsync(frameReadTimeout, ct, ct);
        }

        public ValueTask<InFlightDecodeLease> AcquireDecodeLeaseAsync(int payloadBytes, CancellationToken ct = default)
            => _decodeLimiter.AcquireAsync(payloadBytes, ct);

        public async Task<(int msgId, byte[] payload)> ReceiveStatusAsync(CancellationToken ct = default)
        {
            ThrowIfDispatched();
            using var statusCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            statusCts.CancelAfter(_config.StatusHandshakeTimeoutMs);

            return await ReadNextSubProtocolFrameAsync(FrameReadTimeout, statusCts.Token, ct);
        }

        private void ThrowIfDispatched()
        {
            if (_dispatcher.IsLoopStarted)
                throw new InvalidOperationException(
                    "Connection is in dispatched mode (SendRequestAsync); pull-based ReceiveMessageAsync is disabled on it.");
        }

        public async Task<(int msgId, byte[] payload)> RequestAsync(
            int requestMsgId, byte[] requestPayload,
            int expectedResponseMsgId, CancellationToken ct = default)
        {
            if (_dispatcher.IsLoopStarted)
                throw new InvalidOperationException(
                    "Connection is in dispatched mode (SendRequestAsync); legacy RequestAsync is disabled on it.");
            await SendFrameAsync(requestMsgId, requestPayload, ct);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_config.RequestTimeoutMs);

            while (true)
            {
                var (msgId, payload) = await ReadNextSubProtocolFrameAsync(
                    FrameReadTimeout, timeoutCts.Token, ct);

                if (msgId == expectedResponseMsgId)
                    return (msgId, payload);

                await EnqueuePushAsync(new RlpxPushMessageEventArgs(msgId, payload), timeoutCts.Token);
            }
        }

        private Task EnqueuePushAsync(RlpxPushMessageEventArgs args, CancellationToken ct)
        {
            if (PushMessageReceived == null) return Task.CompletedTask;
            return _pushChannel.EnqueueAsync(args, ct);
        }

        public void StartPushReceiving() => _dispatcher.EnsureLoopStarted();

        public Task<byte[]> SendRequestAsync(
            int sendMsgId, byte[] payload, int expectedResponseMsgId, ulong requestId,
            TimeSpan timeout, CancellationToken ct = default)
            => _dispatcher.SendRequestAsync(
                sendMsgId, payload, expectedResponseMsgId, requestId, timeout, RlpxResponseBound.Unbounded, ct);

        [NethereumDocExample(DocSection.DevP2P, "devp2p", "RlpxConnection.SendRequestAsync — request with a bounded response")]
        public Task<byte[]> SendRequestAsync(
            int sendMsgId, byte[] payload, int expectedResponseMsgId, ulong requestId,
            TimeSpan timeout, RlpxResponseBound bound, CancellationToken ct = default)
            => _dispatcher.SendRequestAsync(sendMsgId, payload, expectedResponseMsgId, requestId, timeout, bound, ct);

        public Task<T> SendRequestAsync<T>(
            int sendMsgId, byte[] payload, int expectedResponseMsgId, ulong requestId,
            TimeSpan timeout, RlpxResponseBound bound, Func<byte[], T> decode, CancellationToken ct = default)
            => _dispatcher.SendRequestAsync(
                sendMsgId, payload, expectedResponseMsgId, requestId, timeout, bound, decode, ct);

        public void StartDispatching() => _dispatcher.EnsureLoopStarted();

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
            try { await SendPingAsync(); }
            catch { }
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
            try
            {
                await _frameChannel.SendFrameAsync(msgId, payload, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is TimeoutException || ex is IOException)
            {
                MarkDisconnected();
                Dispose();
                throw;
            }
        }

        private Task<(int msgId, byte[] payload)> ReadFrameAsync(CancellationToken ct = default)
            => _frameChannel.ReadFrameAsync(ct);

        private async Task<(int msgId, byte[] payload)> ReadFrameWithinAsync(
            TimeSpan frameReadTimeout, CancellationToken ct)
        {
            using var frameCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            frameCts.CancelAfter(frameReadTimeout);
            return await ReadFrameAsync(frameCts.Token);
        }

        private static async Task<List<IPAddress>> ResolveTargetIpsAsync(string host, CancellationToken ct)
        {
            if (IPAddress.TryParse(host, out var literal))
                return new List<IPAddress> { literal };

            var addrs = await System.Net.Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);
            return new List<IPAddress>(addrs);
        }

        private List<IPAddress> FilterByNetRestrictOrThrow(string host, List<IPAddress> targetIps)
        {
            if (_config.NetRestrict.Count == 0) return targetIps;

            var allowed = new List<IPAddress>(targetIps.Count);
            foreach (var ip in targetIps)
            {
                if (_config.NetRestrict.Contains(ip)) allowed.Add(ip);
            }
            if (allowed.Count == 0)
                throw new RlpxNetRestrictedException(host, targetIps);
            return allowed;
        }

        private bool _disposed;
        public bool IsDisposed => _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            MarkDisconnected();
            _dispatcher.Dispose();
            _pushChannel.Dispose();
            _pingTimer?.Dispose();
            _stream?.Dispose();
            _tcp?.Dispose();
            _frameChannel?.Dispose();
        }
    }

    [NethereumDocExample(DocSection.DevP2P, "devp2p", "RlpxPeerRejectedException — remote rejected the handshake")]
    public sealed class RlpxPeerRejectedException : Exception
    {
        public DisconnectReason Reason { get; }
        public RlpxPeerRejectedException(DisconnectReason reason)
            : base($"Peer rejected RLPx handshake with Disconnect(reason={(byte)reason:x2} {reason})")
        {
            Reason = reason;
        }
    }

    [NethereumDocExample(DocSection.DevP2P, "devp2p", "RlpxNetRestrictedException — dial blocked by NetRestrict")]
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
