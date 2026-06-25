using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Strategies;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.Signer;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Hosted RLPx serving composition: binds a TCP port, accepts inbound
    /// peers, drives the eth/68+69 server handshake, and dispatches incoming
    /// requests to <see cref="StorageBackedEth68Handler"/> backed by the
    /// caller's <see cref="IChainStoreBundle"/>. When the negotiated
    /// capability set also includes snap/1, the session is multiplexed
    /// through <see cref="MultiProtocolRlpxSession"/>.
    /// <para>
    /// This type is the production-policy + DI-friendly composition layer
    /// over the wire primitives in <c>Nethereum.DevP2P.Rlpx.RlpxListener</c>.
    /// SyncNode, AppChain validators and mainnet relays all instantiate
    /// this rather than wiring the primitives by hand.
    /// </para>
    /// </summary>
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

        /// <summary>Number of currently admitted peers. Mirrors the inner
        /// listener's counter — exposed for metrics.</summary>
        public int ActivePeers => _inner?.ActivePeers ?? 0;

        /// <summary>Actual local TCP endpoint after Start. Null before Start.</summary>
        public IPEndPoint LocalEndpoint => _inner?.LocalEndpoint;

        /// <summary>Actual bound TCP port after Start. Useful when the
        /// caller passed <c>ListenPort=0</c> for OS-assigned.</summary>
        public int Port => _inner?.Port ?? 0;

        /// <summary>64-byte secp256k1 pubkey identifying this node on the
        /// wire — what an enode:// URL embeds.</summary>
        public byte[] NodeId => _localKey.GetPubKeyNoPrefix();

        /// <summary>
        /// Construct a listener serving from <paramref name="bundle"/>.
        /// <paramref name="statusTemplate"/> is required when
        /// <see cref="PeerListenerOptions.MirrorRemoteStatus"/> is false —
        /// it carries the chain identity we assert to peers. When mirroring,
        /// the template is optional and only used as a fallback.
        /// <paramref name="snapHandler"/> is optional; when null and
        /// <see cref="PeerListenerOptions.ServeSnap"/> is true, snap/1 is
        /// advertised but its requests are answered with empty responses.
        /// </summary>
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
                NetworkId = _statusTemplate != null ? _statusTemplate.NetworkId : 0,
                GenesisHash = _statusTemplate != null ? _statusTemplate.GenesisHash : null,
            };
        }

        /// <summary>
        /// Bind the listener and start the accept loop. Returns when the
        /// socket is bound — peers handshake asynchronously in the
        /// background. Idempotent: a second call after stop rebuilds the
        /// listener.
        /// </summary>
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
            // Per-IP throttle + MaxPeers rejection log lines would spam the
            // logger under a slow-loris attack; demote them to debug.
            if (e.Phase == "InboundPerIPCap" || e.Phase == "MaxPeers")
                _logger.LogDebug("Inbound rejected [{Phase}]: {Message}", e.Phase, e.Exception.Message);
            else
                _logger.LogWarning(e.Exception, "Inbound failure [{Phase}]", e.Phase);
        }

        private async Task HandleSessionAsync(RlpxConnection connection, CancellationToken ct)
        {
            // Stable peer key for lifecycle callbacks (dial scheduler ratio,
            // metrics). Prefer node-id hex when available; fall back to the
            // remote endpoint string for the cap-rejected path.
            string peerKey = connection.RemoteNodeId != null
                ? connection.RemoteNodeId.ToHex()
                : connection.RemoteEndpoint ?? string.Empty;
            bool reportedAdded = false;
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

                // Status exchange. We always RECEIVE remote Status first so we
                // can mirror it back if MirrorRemoteStatus is on. Either order
                // is wire-legal — receiving first costs us nothing.
                var (msgId, payload) = await connection.ReceiveMessageAsync(ct).ConfigureAwait(false);
                if (msgId != ethOffset + Eth68MessageIds.Status)
                {
                    _logger.LogDebug("Inbound peer {Endpoint} sent msgId=0x{Id:x2} instead of Status", connection.RemoteEndpoint, msgId);
                    try { await connection.DisconnectAsync(DisconnectReason.ProtocolBreach); } catch { }
                    return;
                }
                var remoteStatus = Eth68StatusMessageEncoder.Decode(payload);

                var localStatus = BuildLocalStatus(ethCap.Version, remoteStatus);
                await connection.SendMessageAsync(
                    ethOffset + Eth68MessageIds.Status,
                    Eth68StatusMessageEncoder.Encode(localStatus),
                    ct).ConfigureAwait(false);

                _logger.LogInformation(
                    "Inbound peer admitted {Endpoint} eth/{Version} chain={Chain}",
                    connection.RemoteEndpoint, ethCap.Version, localStatus.NetworkId);

                // Lifecycle event for ratio counters / metrics. Fires only
                // after the eth Status round-trip succeeds — pre-handshake
                // rejections (NetRestrict / per-IP / per-subnet / MaxPeers)
                // already log via OnPeerFailed.
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
                    txPool: null);
                var ethSession = new Eth68ServerSession(connection, ethHandler, localStatus);
                // Status exchange already happened above; stamp the eth
                // capability offset directly so RunAsync can dispatch.
                ethSession.BindCapabilityOffset(ethOffset);

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
                    // Eth-only loop. We call HandleEthMessageAsync directly so
                    // unexpected (non-eth) message ids don't crash the dispatch.
                    // IdleTimeout protects against silent peers occupying inbound
                    // slots indefinitely (slow-loris).
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
                        // Anything outside the eth range we silently ignore
                        // per the devp2p spec — peer may have advertised a
                        // capability we don't serve.
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
                // Echo remote's chain identifiers so the peer accepts our
                // reply regardless of our actual head. They'll disconnect on
                // first failed data request when we serve outside their
                // history, but the listener-side code paths get exercised
                // and from-genesis followers can use us.
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

        public void Dispose() => StopAsync().GetAwaiter().GetResult();

        public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
    }
}
