using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Common.Utilities;
using DotNetty.Handlers.Timeout;
using DotNetty.Handlers.Tls;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.P2P;
using Nethereum.Signer;

namespace Nethereum.AppChain.P2P.DotNetty
{
    public class DotNettyTransport : IP2PTransport, IAsyncDisposable
    {
        private readonly DotNettyConfig _config;
        private readonly ILogger<DotNettyTransport>? _logger;
        private readonly ConcurrentDictionary<string, PeerSession> _sessions = new();
        private readonly ConcurrentDictionary<string, PeerInfo> _knownPeers = new();
        private readonly ConcurrentDictionary<string, int> _connectionsPerIp = new();
        private readonly Channel<(PeerSession, P2PMessage)> _messageChannel;
        private readonly EthECKey? _nodeKey;
        private Task? _messageProcessingTask;

        private IEventLoopGroup? _bossGroup;
        private IEventLoopGroup? _workerGroup;
        private IChannel? _serverChannel;
        private Bootstrap? _clientBootstrap;
        private X509Certificate2? _tlsCertificate;
        private CancellationTokenSource? _cts;
        private Task? _peerDiscoveryTask;
        private Task? _maintenanceTask;
        private bool _disposed;
        private bool _isRunning;

        public string NodeId { get; }
        public bool IsRunning => _isRunning;
        public IReadOnlyCollection<string> ConnectedPeers =>
            _sessions.Values.Where(s => s.State == PeerState.Connected).Select(s => s.PeerId).ToList().AsReadOnly();
        public long ChainId => _config.ChainId;

        public event EventHandler<P2PMessageEventArgs>? MessageReceived;
        public event EventHandler<PeerEventArgs>? PeerConnected;
        public event EventHandler<PeerEventArgs>? PeerDisconnected;

        public DotNettyTransport(DotNettyConfig? config = null, ILogger<DotNettyTransport>? logger = null)
        {
            _config = config ?? DotNettyConfig.Default;
            _logger = logger;

            _messageChannel = Channel.CreateBounded<(PeerSession, P2PMessage)>(
                new BoundedChannelOptions(_config.MessageQueueCapacity)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = false,
                    SingleWriter = false
                });

            if (!string.IsNullOrEmpty(_config.NodePrivateKey))
            {
                _nodeKey = new EthECKey(_config.NodePrivateKey);
                NodeId = _nodeKey.GetPublicAddress().ToLowerInvariant();
            }
            else
            {
                _nodeKey = EthECKey.GenerateKey();
                NodeId = _nodeKey.GetPublicAddress().ToLowerInvariant();
            }

            _logger?.LogInformation("P2P Transport initialized with NodeId: {NodeId}", NodeId);
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            if (_config.UseTls)
            {
                LoadTlsCertificate();
            }

            _bossGroup = new MultithreadEventLoopGroup(1);
            _workerGroup = new MultithreadEventLoopGroup(_config.WorkerThreads > 0 ? _config.WorkerThreads : Environment.ProcessorCount);

            var serverBootstrap = new ServerBootstrap()
                .Group(_bossGroup, _workerGroup)
                .Channel<TcpServerSocketChannel>()
                .Option(ChannelOption.SoBacklog, 100)
                .Option(ChannelOption.SoReuseaddr, true)
                .ChildOption(ChannelOption.TcpNodelay, true)
                .ChildOption(ChannelOption.SoKeepalive, true)
                .ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
                {
                    InitializePipeline(channel, isServer: true);
                }));

            var endpoint = new IPEndPoint(IPAddress.Parse(_config.ListenAddress), _config.ListenPort);
            _serverChannel = await serverBootstrap.BindAsync(endpoint);

            _logger?.LogInformation("P2P server listening on {Endpoint}, TLS: {UseTls}", endpoint, _config.UseTls);

            _clientBootstrap = new Bootstrap()
                .Group(_workerGroup)
                .Channel<TcpSocketChannel>()
                .Option(ChannelOption.TcpNodelay, true)
                .Option(ChannelOption.SoKeepalive, true)
                .Option(ChannelOption.ConnectTimeout, TimeSpan.FromMilliseconds(_config.ConnectionTimeoutMs))
                .Handler(new ActionChannelInitializer<ISocketChannel>(channel =>
                {
                    InitializePipeline(channel, isServer: false);
                }));

            _isRunning = true;

            _messageProcessingTask = RunMessageProcessingLoopAsync(_cts.Token);
            _peerDiscoveryTask = RunPeerDiscoveryLoopAsync(_cts.Token);
            _maintenanceTask = RunMaintenanceLoopAsync(_cts.Token);

            foreach (var bootstrapNode in _config.BootstrapNodes)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ConnectAsync(bootstrapNode, bootstrapNode);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to connect to bootstrap node {Node}", bootstrapNode);
                    }
                }, cancellationToken);
            }
        }

        public async Task StopAsync()
        {
            if (_disposed) return;
            _isRunning = false;
            try { _cts?.Cancel(); } catch (ObjectDisposedException) { }

            _messageChannel.Writer.TryComplete();

            foreach (var session in _sessions.Values.ToList())
            {
                try
                {
                    await SendDisconnectAsync(session, DisconnectReason.ClientQuit);
                    await session.Channel.CloseAsync();
                }
                catch { }
            }

            _sessions.Clear();
            _connectionsPerIp.Clear();

            if (_serverChannel != null)
            {
                await _serverChannel.CloseAsync();
            }

            if (_messageProcessingTask != null)
            {
                try { await _messageProcessingTask; } catch (OperationCanceledException) { }
            }

            if (_peerDiscoveryTask != null)
            {
                try { await _peerDiscoveryTask; } catch (OperationCanceledException) { }
            }

            if (_maintenanceTask != null)
            {
                try { await _maintenanceTask; } catch (OperationCanceledException) { }
            }

            if (_bossGroup != null)
            {
                await _bossGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
            }

            if (_workerGroup != null)
            {
                await _workerGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
            }

            _tlsCertificate?.Dispose();
            _cts?.Dispose();
            _logger?.LogInformation("P2P transport stopped");
        }

        public async Task ConnectAsync(string peerId, string endpoint)
        {
            if (_clientBootstrap == null)
                throw new InvalidOperationException("Transport not started");

            if (_sessions.Values.Any(s => s.Endpoint == endpoint && s.State != PeerState.Disconnected))
            {
                _logger?.LogDebug("Already connected or connecting to endpoint {Endpoint}", endpoint);
                return;
            }

            var connectedCount = _sessions.Values.Count(s => s.State == PeerState.Connected);
            if (connectedCount >= _config.MaxConnections)
            {
                _logger?.LogWarning("Maximum connections ({Max}) reached, cannot connect to {Endpoint}", _config.MaxConnections, endpoint);
                return;
            }

            var (host, port) = ParseEndpoint(endpoint);
            var remoteEndpoint = new IPEndPoint(IPAddress.Parse(host), port);

            _logger?.LogDebug("Connecting to {Endpoint}", endpoint);

            var tempPeerId = GenerateTempPeerId(endpoint);
            var session = new PeerSession
            {
                PeerId = tempPeerId,
                Endpoint = endpoint,
                IsOutbound = true,
                State = PeerState.Connecting,
                ConnectedAt = DateTimeOffset.UtcNow
            };

            _sessions[tempPeerId] = session;

            try
            {
                var channel = await _clientBootstrap.ConnectAsync(remoteEndpoint);
                session.Channel = channel;
                channel.GetAttribute(SessionKey).Set(session);

                session.State = PeerState.Handshaking;
                await SendHelloAsync(session);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to connect to {Endpoint}", endpoint);
                _sessions.TryRemove(tempPeerId, out _);
                throw;
            }
        }

        public async Task DisconnectAsync(string peerId)
        {
            if (_sessions.TryGetValue(peerId, out var session))
            {
                await SendDisconnectAsync(session, DisconnectReason.Requested);
                await CloseSessionAsync(session);
            }
        }

        public async Task BroadcastAsync(P2PMessage message, CancellationToken cancellationToken = default)
        {
            var connectedSessions = _sessions.Values
                .Where(s => s.State == PeerState.Connected)
                .ToList();

            var data = SerializeMessage(message);
            var tasks = connectedSessions.Select(async session =>
            {
                try
                {
                    if (session.Channel.Active)
                    {
                        await session.Channel.WriteAndFlushAsync(Unpooled.WrappedBuffer(data));
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to broadcast to peer {PeerId}", session.PeerId);
                }
            });

            await Task.WhenAll(tasks);
        }

        public async Task SendAsync(string peerId, P2PMessage message, CancellationToken cancellationToken = default)
        {
            if (!_sessions.TryGetValue(peerId, out var session))
            {
                throw new InvalidOperationException($"Not connected to peer {peerId}");
            }

            if (session.State != PeerState.Connected && message.Type != P2PMessageType.Hello &&
                message.Type != P2PMessageType.AuthChallenge && message.Type != P2PMessageType.AuthResponse &&
                message.Type != P2PMessageType.Disconnect)
            {
                throw new InvalidOperationException($"Peer {peerId} is not in connected state");
            }

            var data = SerializeMessage(message);
            await session.Channel.WriteAndFlushAsync(Unpooled.WrappedBuffer(data));
        }

        public bool IsConnected(string peerId)
        {
            return _sessions.TryGetValue(peerId, out var session) &&
                   session.State == PeerState.Connected &&
                   session.Channel.Active;
        }

        public PeerInfo[] GetKnownPeers(int maxCount = 25)
        {
            return _knownPeers.Values
                .OrderByDescending(p => p.ReputationScore)
                .ThenByDescending(p => p.LastSeen)
                .Take(maxCount)
                .ToArray();
        }

        private void InitializePipeline(ISocketChannel channel, bool isServer)
        {
            var pipeline = channel.Pipeline;

            if (_config.UseTls && _tlsCertificate != null)
            {
                if (isServer)
                {
                    var tlsSettings = new ServerTlsSettings(_tlsCertificate);
                    pipeline.AddLast("tls", new TlsHandler(tlsSettings));
                }
                else
                {
                    var tlsSettings = new ClientTlsSettings(_config.TlsTargetHost ?? "localhost");
                    pipeline.AddLast("tls", new TlsHandler(tlsSettings));
                }
            }

            pipeline.AddLast("frameDecoder", new LengthFieldBasedFrameDecoder(
                _config.MaxMessageSize, 0, 4, 0, 4));
            pipeline.AddLast("frameEncoder", new LengthFieldPrepender(4));
            pipeline.AddLast("idleStateHandler", new IdleStateHandler(
                _config.IdleTimeoutSeconds, _config.IdleTimeoutSeconds / 2, 0));
            pipeline.AddLast("handler", new P2PChannelHandler(this, isServer, _logger));
        }

        private void LoadTlsCertificate()
        {
            if (!string.IsNullOrEmpty(_config.TlsCertificatePath))
            {
                if (!string.IsNullOrEmpty(_config.TlsCertificatePassword))
                {
                    _tlsCertificate = new X509Certificate2(_config.TlsCertificatePath, _config.TlsCertificatePassword);
                }
                else
                {
                    _tlsCertificate = new X509Certificate2(_config.TlsCertificatePath);
                }
                _logger?.LogInformation("Loaded TLS certificate from {Path}", _config.TlsCertificatePath);
            }
            else
            {
                _tlsCertificate = GenerateSelfSignedCertificate();
                _logger?.LogInformation("Generated self-signed TLS certificate for development");
            }
        }

        private static X509Certificate2 GenerateSelfSignedCertificate()
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=Nethereum P2P Node",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, false));

            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    false));

            var certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(1));

            return new X509Certificate2(
                certificate.Export(X509ContentType.Pfx),
                (string?)null,
                X509KeyStorageFlags.Exportable);
        }

        internal void OnChannelActive(IChannelHandlerContext ctx, bool isServer)
        {
            if (isServer)
            {
                var remoteAddress = ctx.Channel.RemoteAddress?.ToString() ?? "unknown";

                var ipAddress = ExtractIpAddress(remoteAddress);
                var currentIpConnections = _connectionsPerIp.GetOrAdd(ipAddress, 0);
                if (currentIpConnections >= _config.MaxConnectionsPerIp)
                {
                    _logger?.LogWarning("Rejecting connection from {Address}: max connections per IP ({Max}) reached",
                        remoteAddress, _config.MaxConnectionsPerIp);
                    var disconnectMsg = new DisconnectMessage { Reason = DisconnectReason.TooManyPeers };
                    var data = SerializeMessage(new P2PMessage(P2PMessageType.Disconnect, disconnectMsg.Serialize()));
                    ctx.WriteAndFlushAsync(Unpooled.WrappedBuffer(data));
                    ctx.CloseAsync();
                    return;
                }

                var connectedCount = _sessions.Values.Count(s => s.State == PeerState.Connected || s.State == PeerState.Handshaking);

                if (connectedCount >= _config.MaxConnections)
                {
                    _logger?.LogWarning("Rejecting connection from {Address}: max connections reached", remoteAddress);
                    var disconnectMsg = new DisconnectMessage { Reason = DisconnectReason.TooManyPeers };
                    var data = SerializeMessage(new P2PMessage(P2PMessageType.Disconnect, disconnectMsg.Serialize()));
                    ctx.WriteAndFlushAsync(Unpooled.WrappedBuffer(data));
                    ctx.CloseAsync();
                    return;
                }

                _connectionsPerIp.AddOrUpdate(ipAddress, 1, (_, count) => count + 1);

                var tempPeerId = GenerateTempPeerId(remoteAddress);
                var session = new PeerSession
                {
                    PeerId = tempPeerId,
                    Endpoint = remoteAddress,
                    Channel = ctx.Channel,
                    IsOutbound = false,
                    State = PeerState.Handshaking,
                    ConnectedAt = DateTimeOffset.UtcNow,
                    IpAddress = ipAddress
                };

                _sessions[tempPeerId] = session;
                ctx.Channel.GetAttribute(SessionKey).Set(session);

                _logger?.LogDebug("Inbound connection from {Address}, waiting for Hello", remoteAddress);
            }
        }

        private static string ExtractIpAddress(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint))
                return "unknown";

            var bracketEnd = endpoint.IndexOf(']');
            if (bracketEnd > 0)
            {
                return endpoint[1..bracketEnd];
            }

            var colonIndex = endpoint.LastIndexOf(':');
            if (colonIndex > 0)
            {
                return endpoint[..colonIndex];
            }

            return endpoint;
        }

        internal void OnChannelInactive(IChannelHandlerContext ctx)
        {
            var session = ctx.Channel.GetAttribute(SessionKey).Get();
            if (session != null)
            {
                _ = CloseSessionAsync(session);
            }
        }

        internal void OnMessageReceived(IChannelHandlerContext ctx, byte[] data)
        {
            var session = ctx.Channel.GetAttribute(SessionKey).Get();
            if (session == null)
            {
                _logger?.LogWarning("Received message from unregistered channel");
                ctx.CloseAsync();
                return;
            }

            try
            {
                var message = DeserializeMessage(data);
                session.LastActivity = DateTimeOffset.UtcNow;

                if (!_messageChannel.Writer.TryWrite((session, message)))
                {
                    _logger?.LogWarning("Message queue full, dropping message from {PeerId}", session.PeerId);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to deserialize message from {PeerId}", session.PeerId);
                _ = SendDisconnectAsync(session, DisconnectReason.ProtocolBreach);
                _ = CloseSessionAsync(session);
            }
        }

        internal void OnIdleState(IChannelHandlerContext ctx, IdleState state)
        {
            var session = ctx.Channel.GetAttribute(SessionKey).Get();
            if (session == null) return;

            if (state == IdleState.ReaderIdle)
            {
                var idleTime = DateTimeOffset.UtcNow - session.LastActivity;
                if (idleTime > TimeSpan.FromSeconds(_config.IdleTimeoutSeconds * 2))
                {
                    _logger?.LogWarning("Peer {PeerId} timed out after {Seconds}s of inactivity",
                        session.PeerId, idleTime.TotalSeconds);
                    _ = SendDisconnectAsync(session, DisconnectReason.Timeout);
                    _ = CloseSessionAsync(session);
                    return;
                }

                _logger?.LogDebug("Sending ping to idle peer {PeerId}", session.PeerId);
                var pingData = SerializeMessage(P2PMessage.Ping());
                ctx.WriteAndFlushAsync(Unpooled.WrappedBuffer(pingData));
                session.PendingPing = true;
                session.LastPingTime = DateTimeOffset.UtcNow;
            }
            else if (state == IdleState.WriterIdle)
            {
                var pingData = SerializeMessage(P2PMessage.Ping());
                ctx.WriteAndFlushAsync(Unpooled.WrappedBuffer(pingData));
            }
        }

        private async Task HandleMessageAsync(PeerSession session, P2PMessage message)
        {
            switch (message.Type)
            {
                case P2PMessageType.Hello:
                    await HandleHelloAsync(session, message.Payload);
                    break;

                case P2PMessageType.AuthChallenge:
                    await HandleAuthChallengeAsync(session, message.Payload);
                    break;

                case P2PMessageType.AuthResponse:
                    await HandleAuthResponseAsync(session, message.Payload);
                    break;

                case P2PMessageType.Disconnect:
                    HandleDisconnect(session, message.Payload);
                    break;

                case P2PMessageType.Ping:
                    await HandlePingAsync(session);
                    break;

                case P2PMessageType.Pong:
                    HandlePong(session);
                    break;

                case P2PMessageType.GetPeers:
                    await HandleGetPeersAsync(session, message.Payload);
                    break;

                case P2PMessageType.Peers:
                    HandlePeers(session, message.Payload);
                    break;

                default:
                    if (session.State == PeerState.Connected)
                    {
                        MessageReceived?.Invoke(this, new P2PMessageEventArgs(session.PeerId, message));
                    }
                    else
                    {
                        _logger?.LogWarning("Received application message from non-connected peer {PeerId}", session.PeerId);
                    }
                    break;
            }
        }

        private async Task HandleHelloAsync(PeerSession session, byte[] payload)
        {
            HelloMessage hello;
            try
            {
                hello = HelloMessage.Deserialize(payload);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Invalid Hello message from {PeerId}", session.PeerId);
                await SendDisconnectAsync(session, DisconnectReason.ProtocolBreach);
                await CloseSessionAsync(session);
                return;
            }

            if (hello.ProtocolVersion != HelloMessage.CurrentProtocolVersion)
            {
                _logger?.LogWarning("Incompatible protocol version {Version} from {Endpoint}",
                    hello.ProtocolVersion, session.Endpoint);
                await SendDisconnectAsync(session, DisconnectReason.IncompatibleProtocol);
                await CloseSessionAsync(session);
                return;
            }

            if (hello.ChainId != _config.ChainId)
            {
                _logger?.LogWarning("Chain ID mismatch: expected {Expected}, got {Actual} from {Endpoint}",
                    _config.ChainId, hello.ChainId, session.Endpoint);
                await SendDisconnectAsync(session, DisconnectReason.ChainIdMismatch);
                await CloseSessionAsync(session);
                return;
            }

            if (hello.NodeId == NodeId)
            {
                _logger?.LogDebug("Disconnecting self-connection to {Endpoint}", session.Endpoint);
                await SendDisconnectAsync(session, DisconnectReason.SameIdentity);
                await CloseSessionAsync(session);
                return;
            }

            var existingSession = _sessions.Values.FirstOrDefault(s =>
                s.PeerId == hello.NodeId && s.State == PeerState.Connected);
            if (existingSession != null)
            {
                _logger?.LogDebug("Already connected to {NodeId}, closing duplicate", hello.NodeId);
                await SendDisconnectAsync(session, DisconnectReason.AlreadyConnected);
                await CloseSessionAsync(session);
                return;
            }

            var oldPeerId = session.PeerId;
            session.PeerId = hello.NodeId;
            session.RemoteHello = hello;
            session.ClientVersion = hello.ClientVersion;

            _sessions.TryRemove(oldPeerId, out _);
            _sessions[hello.NodeId] = session;

            _logger?.LogInformation("Received Hello from {NodeId} ({Client}) at {Endpoint}",
                hello.NodeId, hello.ClientVersion, session.Endpoint);

            if (!session.IsOutbound)
            {
                await SendHelloAsync(session);
            }

            if (_config.RequireAuthentication)
            {
                session.State = PeerState.Authenticating;
                await SendAuthChallengeAsync(session);
            }
            else
            {
                await CompleteHandshakeAsync(session);
            }
        }

        private async Task HandleAuthChallengeAsync(PeerSession session, byte[] payload)
        {
            if (session.State != PeerState.Authenticating && session.State != PeerState.Handshaking)
            {
                _logger?.LogWarning("Unexpected AuthChallenge from {PeerId} in state {State}",
                    session.PeerId, session.State);
                return;
            }

            var challenge = AuthChallengeMessage.Deserialize(payload);

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(now - challenge.Timestamp) > 30)
            {
                _logger?.LogWarning("AuthChallenge timestamp too old from {PeerId}", session.PeerId);
                await SendDisconnectAsync(session, DisconnectReason.AuthenticationFailed);
                await CloseSessionAsync(session);
                return;
            }

            if (_nodeKey == null)
            {
                _logger?.LogError("Cannot respond to auth challenge: no node key configured");
                await SendDisconnectAsync(session, DisconnectReason.AuthenticationFailed);
                await CloseSessionAsync(session);
                return;
            }

            var hash = Nethereum.Util.Sha3Keccack.Current.CalculateHash(challenge.Challenge);
            var signature = _nodeKey.SignAndCalculateV(hash);

            var sigBytes = new byte[65];
            Array.Copy(signature.R, 0, sigBytes, 0, 32);
            Array.Copy(signature.S, 0, sigBytes, 32, 32);
            sigBytes[64] = (byte)(signature.V[0] - 27);

            var response = new AuthResponseMessage
            {
                Signature = sigBytes,
                Address = NodeId
            };

            var message = new P2PMessage(P2PMessageType.AuthResponse, response.Serialize());
            var data = SerializeMessage(message);
            await session.Channel.WriteAndFlushAsync(Unpooled.WrappedBuffer(data));

            session.State = PeerState.Authenticating;
        }

        private async Task HandleAuthResponseAsync(PeerSession session, byte[] payload)
        {
            if (session.State != PeerState.Authenticating)
            {
                _logger?.LogWarning("Unexpected AuthResponse from {PeerId} in state {State}",
                    session.PeerId, session.State);
                return;
            }

            if (session.AuthChallenge == null)
            {
                _logger?.LogWarning("Received AuthResponse without pending challenge from {PeerId}", session.PeerId);
                await SendDisconnectAsync(session, DisconnectReason.ProtocolBreach);
                await CloseSessionAsync(session);
                return;
            }

            var response = AuthResponseMessage.Deserialize(payload);

            try
            {
                var hash = Nethereum.Util.Sha3Keccack.Current.CalculateHash(session.AuthChallenge);

                var r = new byte[32];
                var s = new byte[32];
                Array.Copy(response.Signature, 0, r, 0, 32);
                Array.Copy(response.Signature, 32, s, 0, 32);

                var recoveredKey = EthECKey.RecoverFromSignature(
                    EthECDSASignatureFactory.FromComponents(r, s, response.Signature[64]),
                    hash);

                var recoveredAddress = recoveredKey.GetPublicAddress().ToLowerInvariant();

                if (recoveredAddress != session.PeerId.ToLowerInvariant())
                {
                    _logger?.LogWarning("Auth signature mismatch: expected {Expected}, recovered {Recovered}",
                        session.PeerId, recoveredAddress);
                    await SendDisconnectAsync(session, DisconnectReason.AuthenticationFailed);
                    await CloseSessionAsync(session);
                    return;
                }

                if (_config.AllowedPeers.Count > 0 && !_config.AllowedPeers.Contains(recoveredAddress))
                {
                    _logger?.LogWarning("Peer {PeerId} not in allowlist", recoveredAddress);
                    await SendDisconnectAsync(session, DisconnectReason.AuthenticationFailed);
                    await CloseSessionAsync(session);
                    return;
                }

                session.IsAuthenticated = true;
                session.AuthenticatedAddress = recoveredAddress;

                _logger?.LogInformation("Peer {PeerId} authenticated successfully", session.PeerId);

                await CompleteHandshakeAsync(session);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Auth verification failed for {PeerId}", session.PeerId);
                await SendDisconnectAsync(session, DisconnectReason.AuthenticationFailed);
                await CloseSessionAsync(session);
            }
        }

        private void HandleDisconnect(PeerSession session, byte[] payload)
        {
            var disconnect = DisconnectMessage.Deserialize(payload);
            _logger?.LogInformation("Peer {PeerId} disconnected: {Reason} - {Details}",
                session.PeerId, disconnect.Reason, disconnect.Details);
            _ = CloseSessionAsync(session);
        }

        private async Task HandlePingAsync(PeerSession session)
        {
            var pongData = SerializeMessage(P2PMessage.Pong());
            await session.Channel.WriteAndFlushAsync(Unpooled.WrappedBuffer(pongData));
        }

        private void HandlePong(PeerSession session)
        {
            if (session.PendingPing)
            {
                session.PendingPing = false;
                session.Latency = (int)(DateTimeOffset.UtcNow - session.LastPingTime).TotalMilliseconds;
                _logger?.LogDebug("Received pong from {PeerId}, latency: {Latency}ms", session.PeerId, session.Latency);
            }
        }

        private async Task HandleGetPeersAsync(PeerSession session, byte[] payload)
        {
            if (session.State != PeerState.Connected)
                return;

            var request = GetPeersMessage.Deserialize(payload);
            var peers = GetKnownPeers(Math.Min(request.MaxPeers, 25))
                .Where(p => p.NodeId != session.PeerId)
                .ToArray();

            var response = new PeersMessage { Peers = peers };
            var message = new P2PMessage(P2PMessageType.Peers, response.Serialize());
            var data = SerializeMessage(message);
            await session.Channel.WriteAndFlushAsync(Unpooled.WrappedBuffer(data));

            _logger?.LogDebug("Sent {Count} peers to {PeerId}", peers.Length, session.PeerId);
        }

        private void HandlePeers(PeerSession session, byte[] payload)
        {
            if (session.State != PeerState.Connected)
                return;

            var peersMsg = PeersMessage.Deserialize(payload);

            foreach (var peer in peersMsg.Peers)
            {
                if (peer.NodeId == NodeId)
                    continue;

                if (_sessions.Values.Any(s => s.PeerId == peer.NodeId))
                    continue;

                _knownPeers[peer.NodeId] = peer;
            }

            _logger?.LogDebug("Received {Count} peers from {PeerId}", peersMsg.Peers.Length, session.PeerId);
        }

        private async Task SendHelloAsync(PeerSession session)
        {
            var hello = new HelloMessage
            {
                ProtocolVersion = HelloMessage.CurrentProtocolVersion,
                NodeId = NodeId,
                ChainId = _config.ChainId,
                ListenPort = _config.ListenPort,
                Capabilities = new[] { "eth/68", "snap/1" },
                ClientVersion = "Nethereum/1.0",
                PublicKey = _nodeKey?.GetPubKey() ?? Array.Empty<byte>()
            };

            var message = new P2PMessage(P2PMessageType.Hello, hello.Serialize());
            var data = SerializeMessage(message);
            await session.Channel.WriteAndFlushAsync(Unpooled.WrappedBuffer(data));
        }

        private async Task SendAuthChallengeAsync(PeerSession session)
        {
            var challenge = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(challenge);

            session.AuthChallenge = challenge;

            var challengeMsg = new AuthChallengeMessage
            {
                Challenge = challenge,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            var message = new P2PMessage(P2PMessageType.AuthChallenge, challengeMsg.Serialize());
            var data = SerializeMessage(message);
            await session.Channel.WriteAndFlushAsync(Unpooled.WrappedBuffer(data));
        }

        private async Task SendDisconnectAsync(PeerSession session, DisconnectReason reason, string? details = null)
        {
            try
            {
                var disconnect = new DisconnectMessage { Reason = reason, Details = details };
                var message = new P2PMessage(P2PMessageType.Disconnect, disconnect.Serialize());
                var data = SerializeMessage(message);
                await session.Channel.WriteAndFlushAsync(Unpooled.WrappedBuffer(data));
            }
            catch { }
        }

        private async Task CompleteHandshakeAsync(PeerSession session)
        {
            session.State = PeerState.Connected;

            var (host, port) = ParseEndpoint(session.Endpoint);
            var peerInfo = new PeerInfo
            {
                NodeId = session.PeerId,
                Address = host,
                Port = session.RemoteHello?.ListenPort ?? port,
                LastSeen = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ReputationScore = 50
            };
            _knownPeers[session.PeerId] = peerInfo;

            _logger?.LogInformation("Handshake complete with {PeerId} at {Endpoint}, authenticated: {Auth}",
                session.PeerId, session.Endpoint, session.IsAuthenticated);

            PeerConnected?.Invoke(this, new PeerEventArgs(session.PeerId, session.Endpoint));

            await RequestPeersAsync(session);
        }

        private async Task RequestPeersAsync(PeerSession session)
        {
            try
            {
                var request = new GetPeersMessage { MaxPeers = 25 };
                var message = new P2PMessage(P2PMessageType.GetPeers, request.Serialize());
                var data = SerializeMessage(message);
                await session.Channel.WriteAndFlushAsync(Unpooled.WrappedBuffer(data));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to request peers from {PeerId}", session.PeerId);
            }
        }

        private async Task CloseSessionAsync(PeerSession session)
        {
            if (session.State == PeerState.Disconnected)
                return;

            var wasConnected = session.State == PeerState.Connected;
            session.State = PeerState.Disconnected;

            _sessions.TryRemove(session.PeerId, out _);

            if (!string.IsNullOrEmpty(session.IpAddress))
            {
                _connectionsPerIp.AddOrUpdate(session.IpAddress, 0, (_, count) => Math.Max(0, count - 1));
            }

            try
            {
                if (session.Channel.Active)
                {
                    await session.Channel.CloseAsync();
                }
            }
            catch { }

            if (wasConnected)
            {
                PeerDisconnected?.Invoke(this, new PeerEventArgs(session.PeerId, session.Endpoint));
            }

            _logger?.LogInformation("Session closed for {PeerId}", session.PeerId);
        }

        private async Task RunMessageProcessingLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var (session, message) in _messageChannel.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        await HandleMessageAsync(session, message);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error handling message from {PeerId}", session.PeerId);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fatal error in message processing loop");
            }
        }

        private async Task RunPeerDiscoveryLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_config.PeerDiscoveryIntervalMs, cancellationToken);

                    var connectedCount = _sessions.Values.Count(s => s.State == PeerState.Connected);
                    if (connectedCount >= _config.MaxConnections)
                        continue;

                    var connectedPeers = _sessions.Values
                        .Where(s => s.State == PeerState.Connected)
                        .ToList();

                    foreach (var session in connectedPeers)
                    {
                        await RequestPeersAsync(session);
                    }

                    var targetConnections = Math.Min(_config.MaxConnections, _config.TargetConnections);
                    var neededConnections = targetConnections - connectedCount;

                    if (neededConnections > 0)
                    {
                        var candidatePeers = _knownPeers.Values
                            .Where(p => !_sessions.Values.Any(s => s.PeerId == p.NodeId))
                            .OrderByDescending(p => p.ReputationScore)
                            .Take(neededConnections)
                            .ToList();

                        foreach (var peer in candidatePeers)
                        {
                            try
                            {
                                var endpoint = $"{peer.Address}:{peer.Port}";
                                await ConnectAsync(peer.NodeId, endpoint);
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogDebug(ex, "Failed to connect to discovered peer {NodeId}", peer.NodeId);
                                if (_knownPeers.TryGetValue(peer.NodeId, out var peerInfo))
                                {
                                    peerInfo.ReputationScore = Math.Max(0, peerInfo.ReputationScore - 10);
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in peer discovery loop");
                }
            }
        }

        private async Task RunMaintenanceLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(60000, cancellationToken);

                    var now = DateTimeOffset.UtcNow;
                    var staleSessions = _sessions.Values
                        .Where(s => s.State == PeerState.Handshaking &&
                                   (now - s.ConnectedAt) > TimeSpan.FromSeconds(_config.HandshakeTimeoutSeconds))
                        .ToList();

                    foreach (var session in staleSessions)
                    {
                        _logger?.LogWarning("Handshake timeout for {PeerId}", session.PeerId);
                        await SendDisconnectAsync(session, DisconnectReason.Timeout);
                        await CloseSessionAsync(session);
                    }

                    var oldPeers = _knownPeers.Values
                        .Where(p => !_sessions.Values.Any(s => s.PeerId == p.NodeId) &&
                                   (now.ToUnixTimeSeconds() - p.LastSeen) > 86400)
                        .Select(p => p.NodeId)
                        .ToList();

                    foreach (var peerId in oldPeers)
                    {
                        _knownPeers.TryRemove(peerId, out _);
                    }

                    _logger?.LogDebug("Maintenance: {Connected} connected, {Known} known peers",
                        _sessions.Values.Count(s => s.State == PeerState.Connected),
                        _knownPeers.Count);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in maintenance loop");
                }
            }
        }

        private static (string host, int port) ParseEndpoint(string endpoint)
        {
            var lastColon = endpoint.LastIndexOf(':');
            if (lastColon > 0 && int.TryParse(endpoint[(lastColon + 1)..], out var port))
            {
                return (endpoint[..lastColon], port);
            }
            throw new ArgumentException($"Invalid endpoint format: {endpoint}");
        }

        private static string GenerateTempPeerId(string endpoint)
        {
            var hash = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(endpoint + DateTimeOffset.UtcNow.Ticks));
            return "temp_" + BitConverter.ToString(hash[..8]).Replace("-", "").ToLowerInvariant();
        }

        private static byte[] SerializeMessage(P2PMessage message)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write((byte)message.Type);
            writer.Write(message.Timestamp);
            writer.Write(message.Nonce);
            writer.Write(message.SourcePeerId ?? "");
            writer.Write(message.Payload.Length);
            writer.Write(message.Payload);

            if (message.Signature != null)
            {
                writer.Write(true);
                writer.Write(message.Signature.Length);
                writer.Write(message.Signature);
            }
            else
            {
                writer.Write(false);
            }

            return ms.ToArray();
        }

        private static P2PMessage DeserializeMessage(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            var message = new P2PMessage
            {
                Type = (P2PMessageType)reader.ReadByte(),
                Timestamp = reader.ReadInt64(),
                Nonce = reader.ReadUInt64(),
                SourcePeerId = reader.ReadString()
            };

            if (string.IsNullOrEmpty(message.SourcePeerId))
                message.SourcePeerId = null;

            var payloadLen = reader.ReadInt32();
            message.Payload = reader.ReadBytes(payloadLen);

            var hasSig = reader.ReadBoolean();
            if (hasSig)
            {
                var sigLen = reader.ReadInt32();
                message.Signature = reader.ReadBytes(sigLen);
            }

            return message;
        }

        private static readonly AttributeKey<PeerSession> SessionKey = AttributeKey<PeerSession>.ValueOf("Session");

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            StopAsync().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;
            await StopAsync();
            GC.SuppressFinalize(this);
        }
    }

    internal class PeerSession
    {
        public string PeerId { get; set; } = "";
        public string Endpoint { get; set; } = "";
        public IChannel Channel { get; set; } = null!;
        public bool IsOutbound { get; set; }
        public PeerState State { get; set; } = PeerState.Connecting;
        public DateTimeOffset ConnectedAt { get; set; }
        public DateTimeOffset LastActivity { get; set; } = DateTimeOffset.UtcNow;
        public HelloMessage? RemoteHello { get; set; }
        public string? ClientVersion { get; set; }
        public byte[]? AuthChallenge { get; set; }
        public bool IsAuthenticated { get; set; }
        public string? AuthenticatedAddress { get; set; }
        public bool PendingPing { get; set; }
        public DateTimeOffset LastPingTime { get; set; }
        public int Latency { get; set; }
        public string? IpAddress { get; set; }
    }

    internal class P2PChannelHandler : ChannelHandlerAdapter
    {
        private readonly DotNettyTransport _transport;
        private readonly bool _isServer;
        private readonly ILogger? _logger;

        public P2PChannelHandler(DotNettyTransport transport, bool isServer, ILogger? logger)
        {
            _transport = transport;
            _isServer = isServer;
            _logger = logger;
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            _transport.OnChannelActive(context, _isServer);
            base.ChannelActive(context);
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            _transport.OnChannelInactive(context);
            base.ChannelInactive(context);
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            if (message is IByteBuffer buffer)
            {
                try
                {
                    var data = new byte[buffer.ReadableBytes];
                    buffer.ReadBytes(data);
                    _transport.OnMessageReceived(context, data);
                }
                finally
                {
                    buffer.Release();
                }
            }
        }

        public override void UserEventTriggered(IChannelHandlerContext context, object evt)
        {
            if (evt is IdleStateEvent idleEvent)
            {
                _transport.OnIdleState(context, idleEvent.State);
            }
            base.UserEventTriggered(context, evt);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            _logger?.LogError(exception, "Channel exception");
            context.CloseAsync();
        }
    }
}
