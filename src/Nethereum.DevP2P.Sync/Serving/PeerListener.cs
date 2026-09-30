using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.Signer;

namespace Nethereum.DevP2P.Sync.Serving
{
    public sealed class PeerListener : IDisposable, IAsyncDisposable
    {
        private readonly EthECKey _localKey;
        private readonly IChainStoreBundle _bundle;
        private readonly PeerListenerOptions _options;
        private readonly Eth68StatusMessage _statusTemplate;
        private readonly ISnapRequestHandler _snapHandler;
        private readonly ILogger<PeerListener> _logger;
        private readonly DevP2PConfig _config;
        private RlpxListener _inner;
        private CancellationTokenSource _cts;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<RlpxConnection, int> _ethPeers = new();

        public int ActivePeers => _inner?.ActivePeers ?? 0;

        public int EthPeerCount => _ethPeers.Count;

        public IPEndPoint LocalEndpoint => _inner?.LocalEndpoint;

        public int Port => _inner?.Port ?? 0;

        public byte[] NodeId => _localKey.GetPubKeyNoPrefix();

        public PeerListener(
            EthECKey localKey,
            IChainStoreBundle bundle,
            PeerListenerOptions options,
            Eth68StatusMessage statusTemplate = null,
            ISnapRequestHandler snapHandler = null,
            ILogger<PeerListener> logger = null)
        {
            _localKey = localKey ?? throw new ArgumentNullException(nameof(localKey));
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _statusTemplate = statusTemplate;
            _snapHandler = snapHandler;
            _logger = logger ?? NullLogger<PeerListener>.Instance;

            if (!_options.MirrorRemoteStatus && _statusTemplate == null)
                throw new ArgumentException(
                    "PeerListenerOptions.MirrorRemoteStatus=false requires a Status template.",
                    nameof(options));

            _config = new DevP2PConfig
            {
                ClientId = _options.ClientId,
                MaxPeers = _options.MaxInboundPeers,
                MaxInboundPerIP = _options.MaxInboundPerIP,
                HandshakeTimeoutMs = _options.HandshakeTimeoutMs,
                TrustedNodeIds = _options.TrustedNodeIds ?? Array.Empty<string>(),
                AdvertiseSnap2 = _options.AdvertiseSnap2,
                NetworkId = _statusTemplate != null ? _statusTemplate.NetworkId : 0,
                GenesisHash = _statusTemplate != null ? _statusTemplate.GenesisHash : null,
            };
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_inner != null)
                throw new InvalidOperationException("PeerListener already started.");

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _inner = new RlpxListener(_localKey, _config);
            _inner.PeerAccepted += OnPeerAccepted;
            _inner.PeerFailed += OnPeerFailed;
            _inner.Start(port: _options.ListenPort, bindAddress: _options.BindAddress ?? IPAddress.Any);

            _logger.LogInformation(
                "PeerListener bound on {Address}:{Port} (NodeId 0x{NodeId})",
                _options.BindAddress ?? IPAddress.Any,
                Port,
                NodeId.ToHex().Substring(0, 16));

            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            if (_inner == null) return;
            try { _cts?.Cancel(); } catch { }
            try { await _inner.StopAsync().ConfigureAwait(false); } catch { }
            _inner.PeerAccepted -= OnPeerAccepted;
            _inner.PeerFailed -= OnPeerFailed;
            _inner = null;
            try { _cts?.Dispose(); } catch { }
            _cts = null;
        }

        private void OnPeerAccepted(object sender, RlpxConnection connection)
        {
            _ = Task.Run(() => HandleSessionAsync(connection, _cts.Token));
        }

        private void OnPeerFailed(object sender, RlpxListenerErrorEventArgs e)
        {
            if (e.Phase == "InboundPerIPCap" || e.Phase == "MaxPeers")
                _logger.LogDebug("Inbound rejected [{Phase}]: {Message}", e.Phase, e.Exception.Message);
            else
                _logger.LogWarning(e.Exception, "Inbound failure [{Phase}]", e.Phase);
        }

        private async Task HandleSessionAsync(RlpxConnection connection, CancellationToken ct)
        {
            string peerKey = connection.RemoteNodeId != null
                ? connection.RemoteNodeId.ToHex()
                : connection.RemoteEndpoint ?? string.Empty;
            bool reportedAdded = false;
            Guid? registryId = null;
            try
            {
                var ethCap = connection.SharedCapabilities.Find(c => c.Name == "eth");
                if (ethCap == null)
                {
                    _logger.LogDebug("Inbound peer {Endpoint} negotiated no eth capability — disconnecting", connection.RemoteEndpoint);
                    try { await connection.DisconnectAsync(DisconnectReason.IncompatibleVersion); } catch { }
                    return;
                }

                var ethOffset = connection.GetCapabilityOffset("eth");
                var height = await _bundle.Blocks.GetHeightAsync().ConfigureAwait(false);
                ulong latestBlock = height < 0 ? 0UL : (ulong)height;

                var localStatus = _options.MirrorRemoteStatus
                    ? null
                    : BuildLocalStatus(ethCap.Version, null);

                if (localStatus != null)
                {
                    var bestHash = await _bundle.Blocks.GetHashByNumberAsync(latestBlock).ConfigureAwait(false);
                    if (bestHash != null && bestHash.Length > 0) localStatus.BestHash = bestHash;
                }
                if (localStatus != null)
                    await connection.SendMessageAsync(
                        ethOffset + Eth68MessageIds.Status,
                        EncodeStatus(ethCap.Version, localStatus, latestBlock),
                        ct).ConfigureAwait(false);

                var (msgId, payload) = await connection.ReceiveMessageAsync(ct).ConfigureAwait(false);
                if (msgId != ethOffset + Eth68MessageIds.Status)
                {
                    _logger.LogDebug("Inbound peer {Endpoint} sent msgId=0x{Id:x2} instead of Status", connection.RemoteEndpoint, msgId);
                    try { await connection.DisconnectAsync(DisconnectReason.ProtocolBreach); } catch { }
                    return;
                }
                var remoteStatus = DecodeStatus(ethCap.Version, payload);

                if (localStatus == null)
                {
                    localStatus = BuildLocalStatus(ethCap.Version, remoteStatus);
                    await connection.SendMessageAsync(
                        ethOffset + Eth68MessageIds.Status,
                        EncodeStatus(ethCap.Version, localStatus, latestBlock),
                        ct).ConfigureAwait(false);
                }

                _logger.LogInformation(
                    "Inbound peer admitted {Endpoint} eth/{Version} chain={Chain}",
                    connection.RemoteEndpoint, ethCap.Version, localStatus.NetworkId);

                if (_options.EthPeerRegistry != null)
                {
                    try { registryId = _options.EthPeerRegistry.Register(connection, ethOffset, remoteStatus); }
                    catch (Exception rex)
                    {
                        _logger.LogDebug(rex, "Registering inbound peer {Endpoint} for push failed", connection.RemoteEndpoint);
                    }
                }

                if (_options.OnInboundPeerAdded != null)
                {
                    reportedAdded = true;
                    try { _options.OnInboundPeerAdded(peerKey); }
                    catch (Exception cbex)
                    {
                        _logger.LogDebug(cbex, "OnInboundPeerAdded handler threw for {Endpoint}", connection.RemoteEndpoint);
                    }
                }

                var ethHandler = new StorageBackedEth68Handler(
                    _bundle.Blocks,
                    _bundle.Transactions,
                    _bundle.Receipts,
                    _bundle.BlockAccessLists,
                    _bundle.Withdrawals,
                    _options.TxPool,
                    null);
                var ethSession = new Eth68ServerSession(connection, ethHandler, localStatus);
                ethSession.BindCapabilityOffset(ethOffset);
                ethSession.BindRemoteStatus(remoteStatus);

                if (_options.OnPooledTransactionHashesReceived != null)
                    ethSession.NewPooledTransactionHashesReceived += (_, msg) =>
                        _options.OnPooledTransactionHashesReceived(msg);
                if (_options.OnTransactionsReceived != null)
                    ethSession.TransactionsReceived += (_, msg) =>
                        _options.OnTransactionsReceived(msg);
                if (_inner.IsTrustedNodeId(connection.RemoteNodeId))
                {
                    if (_options.OnTrustedTransactionsReceivedFrom != null)
                        ethSession.TransactionsReceived += (_, msg) =>
                            _options.OnTrustedTransactionsReceivedFrom(msg, registryId ?? Guid.Empty);
                    else if (_options.OnTrustedTransactionsReceived != null)
                        ethSession.TransactionsReceived += (_, msg) =>
                            _options.OnTrustedTransactionsReceived(msg);
                }
                if (_options.OnNewBlockReceived != null)
                    ethSession.NewBlockReceived += (_, msg) =>
                        _options.OnNewBlockReceived(msg);

                _ethPeers[connection] = ethOffset;

                var snapCap = _options.ServeSnap
                    ? connection.SharedCapabilities.Find(c => c.Name == "snap")
                    : null;

                if (snapCap != null && _snapHandler != null)
                {
                    var snapSession = new Snap1Handler(connection, _snapHandler);
                    var multiplexed = new MultiProtocolRlpxSession(connection, ethSession, snapSession);
                    await multiplexed.RunAsync(_options.IdleTimeout, ct).ConfigureAwait(false);
                }
                else
                {
                    while (connection.IsConnected && !ct.IsCancellationRequested)
                    {
                        int id; byte[] body;
                        if (_options.IdleTimeout > TimeSpan.Zero)
                        {
                            using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                            idleCts.CancelAfter(_options.IdleTimeout);
                            try
                            {
                                (id, body) = await connection.ReceiveMessageAsync(idleCts.Token).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                            {
                                try { await connection.DisconnectAsync(DisconnectReason.UselessPeer).ConfigureAwait(false); } catch { }
                                return;
                            }
                        }
                        else
                        {
                            (id, body) = await connection.ReceiveMessageAsync(ct).ConfigureAwait(false);
                        }
                        var localId = id - ethOffset;
                        if (localId < 0 || localId >= ethCap.Length) continue;
                        await ethSession.HandleEthMessageAsync(localId, body, ct).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Inbound peer {Endpoint} session ended", connection.RemoteEndpoint);
            }
            finally
            {
                _ethPeers.TryRemove(connection, out _);
                if (registryId.HasValue)
                {
                    try { _options.EthPeerRegistry?.Unregister(registryId.Value); }
                    catch (Exception rex)
                    {
                        _logger.LogDebug(rex, "Unregistering inbound peer {Endpoint} failed", connection.RemoteEndpoint);
                    }
                }
                try { await connection.DisconnectAsync(DisconnectReason.ClientQuitting).ConfigureAwait(false); } catch { }
                try { connection.Dispose(); } catch { }
                if (reportedAdded && _options.OnInboundPeerRemoved != null)
                {
                    try { _options.OnInboundPeerRemoved(peerKey); }
                    catch (Exception cbex)
                    {
                        _logger.LogDebug(cbex, "OnInboundPeerRemoved handler threw for {Endpoint}", connection.RemoteEndpoint);
                    }
                }
            }
        }

        private Eth68StatusMessage BuildLocalStatus(int negotiatedVersion, Eth68StatusMessage remoteStatus)
        {
            if (_options.MirrorRemoteStatus)
            {
                return new Eth68StatusMessage
                {
                    ProtocolVersion = negotiatedVersion,
                    NetworkId = remoteStatus.NetworkId,
                    TotalDifficulty = remoteStatus.TotalDifficulty,
                    BestHash = remoteStatus.BestHash,
                    GenesisHash = remoteStatus.GenesisHash,
                    ForkHash = remoteStatus.ForkHash,
                    ForkNext = remoteStatus.ForkNext,
                };
            }

            return new Eth68StatusMessage
            {
                ProtocolVersion = negotiatedVersion,
                NetworkId = _statusTemplate.NetworkId,
                TotalDifficulty = _statusTemplate.TotalDifficulty,
                BestHash = _statusTemplate.BestHash,
                GenesisHash = _statusTemplate.GenesisHash,
                ForkHash = _statusTemplate.ForkHash,
                ForkNext = _statusTemplate.ForkNext,
            };
        }

        private static byte[] EncodeStatus(int version, Eth68StatusMessage s, ulong latestBlock)
        {
            if (version >= 69)
                return Eth69StatusMessageEncoder.Encode(new Eth69StatusMessage
                {
                    ProtocolVersion = version,
                    NetworkId = s.NetworkId,
                    GenesisHash = s.GenesisHash,
                    ForkHash = s.ForkHash,
                    ForkNext = s.ForkNext,
                    EarliestBlock = 0,
                    LatestBlock = latestBlock,
                    LatestBlockHash = s.BestHash,
                });
            return Eth68StatusMessageEncoder.Encode(s);
        }

        private static Eth68StatusMessage DecodeStatus(int version, byte[] payload)
        {
            if (version >= 69)
            {
                var s = Eth69StatusMessageEncoder.Decode(payload);
                return new Eth68StatusMessage
                {
                    ProtocolVersion = s.ProtocolVersion,
                    NetworkId = s.NetworkId,
                    GenesisHash = s.GenesisHash,
                    ForkHash = s.ForkHash,
                    ForkNext = s.ForkNext,
                    BestHash = s.LatestBlockHash,
                };
            }
            return Eth68StatusMessageEncoder.Decode(payload);
        }

        public void Dispose() => StopAsync().GetAwaiter().GetResult();

        public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
    }
}
