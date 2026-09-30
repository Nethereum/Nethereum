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
        private void MarkDisconnected()
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
            _pushChannel = new RlpxPushChannel(args => PushMessageReceived?.Invoke(this, args));
            _dispatcher = new RlpxRequestDispatcher(
                readFrame: ReadFrameAsync,
                sendFrame: SendFrameAsync,
                handleControlFrame: HandleControlFrameOrContinueAsync,
                onUnsolicited: (msgId, payload) => EnqueuePush(new RlpxPushMessageEventArgs(msgId, payload)),
                onDisconnected: MarkDisconnected,
                readTimeoutMs: () => _config.ReadTimeoutMs,
                onFrameReceived: StampLastFrameReceived);
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
            var capabilities = new List<P2PCapability>
            {
                new() { Name = "eth", Version = 68 },
                new() { Name = "eth", Version = 69 },
                new() { Name = "eth", Version = 70 },
                new() { Name = "eth", Version = 71 },
                new() { Name = "snap", Version = 1 }
            };

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
            return HelloMessageEncoder.Decode(remotePayload);
        }

        private async Task CompleteHandshakeAsync(HelloMessage localHello)
        {
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
                new RlpxFrameReader(secrets.AesSecret, secrets.MacSecret, secrets.IngressMac));
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
            CancellationToken readCt, CancellationToken controlCt)
        {
            while (true)
            {
                var (msgId, payload) = await ReadFrameAsync(readCt);

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

        public async Task<(int msgId, byte[] payload)> ReceiveMessageAsync(CancellationToken ct = default)
        {
            if (_dispatcher.IsLoopStarted)
                throw new InvalidOperationException(
                    "Connection is in dispatched mode (SendRequestAsync); pull-based ReceiveMessageAsync is disabled on it.");
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_config.ReadTimeoutMs);

            return await ReadNextSubProtocolFrameAsync(timeoutCts.Token, ct);
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
                var (msgId, payload) = await ReadNextSubProtocolFrameAsync(timeoutCts.Token, ct);

                if (msgId == expectedResponseMsgId)
                    return (msgId, payload);

                EnqueuePush(new RlpxPushMessageEventArgs(msgId, payload));
            }
        }

        private void EnqueuePush(RlpxPushMessageEventArgs args)
        {
            if (PushMessageReceived == null) return;
            _pushChannel.Enqueue(args);
        }

        public void StartPushReceiving() => _dispatcher.EnsureLoopStarted();

        public Task<byte[]> SendRequestAsync(
            int sendMsgId, byte[] payload, int expectedResponseMsgId, ulong requestId,
            TimeSpan timeout, CancellationToken ct = default)
            => _dispatcher.SendRequestAsync(sendMsgId, payload, expectedResponseMsgId, requestId, timeout, ct);

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

        private Task SendFrameAsync(int msgId, byte[] payload, CancellationToken ct = default)
            => _frameChannel.SendFrameAsync(msgId, payload, ct);

        private Task<(int msgId, byte[] payload)> ReadFrameAsync(CancellationToken ct = default)
            => _frameChannel.ReadFrameAsync(ct);

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
