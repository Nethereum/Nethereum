using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.Signer;

namespace Nethereum.DevP2P.Rlpx
{
    public class RlpxListener : IDisposable
    {
        private readonly EthECKey _localKey;
        private readonly DevP2PConfig _config;
        private readonly RlpxInboundAdmission _admission;
        private readonly HashSet<string> _trustedNodeIds;
        private readonly RlpxPeerSlots _peerSlots;
        private TcpListener? _listener;
        private CancellationTokenSource? _acceptCts;
        private Task? _acceptTask;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, Task> _handshakeTasks = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<RlpxConnection, AdmittedPeer> _admittedConnections = new();
        private readonly SemaphoreSlim _pendingSlots;
        private static readonly TimeSpan AcceptFailureMinDelay = TimeSpan.FromMilliseconds(5);
        private static readonly TimeSpan AcceptFailureMaxDelay = TimeSpan.FromSeconds(1);

        private readonly record struct AdmittedPeer(IPAddress RemoteIp, string? SubnetKey);
        private static readonly TimeSpan HandshakeStopTimeout = TimeSpan.FromSeconds(5);

        public int ActivePeers => _peerSlots.Active;

        public int CountInboundForIp(IPAddress ip)
            => _admission.CountInboundForIp(ip);

        public int PendingHandshakeCount => _handshakeTasks.Count;

        public IPEndPoint? LocalEndpoint => _listener?.LocalEndpoint as IPEndPoint;
        public int Port => LocalEndpoint?.Port ?? 0;
        public byte[] NodeId => _localKey.GetPubKeyNoPrefix();

        public event EventHandler<RlpxConnection>? PeerAccepted;
        public event EventHandler<RlpxListenerErrorEventArgs>? PeerFailed;

        private void RaisePeerFailed(string phase, Exception ex)
        {
            try { PeerFailed?.Invoke(this, new RlpxListenerErrorEventArgs(phase, ex)); } catch { }
        }

        private void RaisePeerAccepted(RlpxConnection connection)
        {
            PeerAccepted?.Invoke(this, connection);
        }

        public RlpxListener(EthECKey localKey, DevP2PConfig config)
        {
            _localKey = localKey ?? throw new ArgumentNullException(nameof(localKey));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _admission = new RlpxInboundAdmission(_config);
            _peerSlots = new RlpxPeerSlots(_config.MaxPeers);
            _pendingSlots = new SemaphoreSlim(_config.MaxPendingPeers > 0 ? _config.MaxPendingPeers : int.MaxValue);
            _trustedNodeIds = new HashSet<string>(
                (config.TrustedNodeIds ?? Array.Empty<string>())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim().ToLowerInvariant()),
                StringComparer.OrdinalIgnoreCase);
        }

        public bool IsTrustedNodeId(byte[]? remoteNodeId)
            => remoteNodeId != null
            && _trustedNodeIds.Count > 0
            && _trustedNodeIds.Contains(remoteNodeId.ToHex().ToLowerInvariant());

        [NethereumDocExample(DocSection.DevP2P, "devp2p", "RlpxListener.Start — accept inbound peers")]
        public void Start(int port = 0, IPAddress? bindAddress = null)
        {
            if (_listener != null)
                throw new InvalidOperationException("Listener already started");

            _listener = new TcpListener(bindAddress ?? IPAddress.Loopback, port);
            _listener.Start();

            _acceptCts = new CancellationTokenSource();
            _acceptTask = Task.Run(() => AcceptLoopAsync(_acceptCts.Token));
        }

        [NethereumDocExample(DocSection.DevP2P, "devp2p", "RlpxListener.StopAsync — stop the listener")]
        public async Task StopAsync()
        {
            var acceptCts = Interlocked.Exchange(ref _acceptCts, null);
            if (acceptCts == null) return;

            acceptCts.Cancel();
            _listener?.Stop();
            var acceptTask = _acceptTask;
            try { if (acceptTask != null) await acceptTask.ConfigureAwait(false); } catch { }

            var pending = _handshakeTasks.Values.ToArray();
            if (pending.Length > 0)
            {
                try { await Task.WhenAny(Task.WhenAll(pending), Task.Delay(HandshakeStopTimeout)).ConfigureAwait(false); }
                catch { }
            }

            _listener = null;
            acceptCts.Dispose();
            _acceptTask = null;
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            var failureDelay = TimeSpan.Zero;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await _pendingSlots.WaitAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                TcpClient tcp;
                try
                {
                    tcp = await _listener!.AcceptTcpClientAsync(ct);
                    failureDelay = TimeSpan.Zero;
                }
                catch (OperationCanceledException)
                {
                    _pendingSlots.Release();
                    return;
                }
                catch (Exception ex)
                {
                    _pendingSlots.Release();
                    RaisePeerFailed("AcceptTcp", ex);
                    failureDelay = NextAcceptFailureDelay(failureDelay);
                    try { await Task.Delay(failureDelay, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                    continue;
                }

                var remoteIp = ResolveRemoteIp(tcp);
                if (remoteIp == null)
                {
                    try { tcp.Close(); } catch { }
                    _pendingSlots.Release();
                    continue;
                }

                if (!_config.NetRestrict.Contains(remoteIp))
                {
                    try { tcp.Close(); } catch { }
                    _pendingSlots.Release();
                    RaisePeerFailed(
                        "InboundNetRestrict",
                        new InvalidOperationException(
                            $"Rejected inbound from {remoteIp}: not in NetRestrict allow-list."));
                    continue;
                }

                var reservation = _admission.TryReserve(remoteIp);
                if (!reservation.Admitted)
                {
                    try { tcp.Close(); } catch { }
                    _pendingSlots.Release();
                    if (reservation.Reason == InboundRejectReason.PerIpCap)
                    {
                        RaisePeerFailed(
                            "InboundPerIPCap",
                            new InvalidOperationException(
                                $"Rejected inbound from {remoteIp}: per-IP cap {_config.MaxInboundPerIP} reached."));
                    }
                    else
                    {
                        RaisePeerFailed(
                            "InboundPerSubnetCap",
                            new InvalidOperationException(
                                $"Rejected inbound from {remoteIp}: per-subnet cap {_config.MaxInboundPerSubnet} reached."));
                    }
                    continue;
                }

                SpawnHandshake(tcp, remoteIp, reservation.SubnetKey, ct);
            }
        }

        private static TimeSpan NextAcceptFailureDelay(TimeSpan previous)
        {
            if (previous == TimeSpan.Zero) return AcceptFailureMinDelay;
            var doubled = TimeSpan.FromTicks(previous.Ticks * 2);
            return doubled > AcceptFailureMaxDelay ? AcceptFailureMaxDelay : doubled;
        }

        private void SpawnHandshake(TcpClient tcp, IPAddress remoteIp, string? subnetKey, CancellationToken ct)
        {
            var taskId = Guid.NewGuid();
            var registered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var handshakeTask = Task.Run(async () =>
            {
                await registered.Task.ConfigureAwait(false);
                try { await HandlePeerAsync(tcp, remoteIp, subnetKey, ct).ConfigureAwait(false); }
                finally
                {
                    _handshakeTasks.TryRemove(taskId, out _);
                    _pendingSlots.Release();
                }
            });
            try { _handshakeTasks[taskId] = handshakeTask; }
            finally { registered.TrySetResult(true); }
        }

        private static IPAddress? ResolveRemoteIp(TcpClient tcp)
        {
            try { return (tcp.Client.RemoteEndPoint as IPEndPoint)?.Address; }
            catch { return null; }
        }

        private async Task HandlePeerAsync(TcpClient tcp, IPAddress remoteIp, string? subnetKey, CancellationToken ct)
        {
            var connection = new RlpxConnection(_localKey, _config);
            var reservationTransferred = false;
            try
            {
                await connection.AcceptIncomingAsync(tcp, ct);

                bool trusted = IsTrustedNodeId(connection.RemoteNodeId);
                if (!_peerSlots.TryAcquire(trusted))
                {
                    try { await connection.DisconnectAsync(DisconnectReason.TooManyPeers); }
                    catch { }
                    RaisePeerFailed(
                        "MaxPeers",
                        new InvalidOperationException(
                            $"Rejected non-trusted inbound (peer count >= MaxPeers {_config.MaxPeers})."));
                    return;
                }

                _admittedConnections[connection] = new AdmittedPeer(remoteIp, subnetKey);
                reservationTransferred = true;
                connection.Disconnected += OnConnectionDisconnected;
                if (!connection.IsConnected)
                {
                    ReleaseAndDispose(connection);
                    return;
                }
                try
                {
                    RaisePeerAccepted(connection);
                }
                catch
                {
                    ReleaseAndDispose(connection);
                    throw;
                }
            }
            catch (Exception ex)
            {
                try { tcp.Close(); } catch { }
                RaisePeerFailed("Handshake", ex);
            }
            finally
            {
                if (!reservationTransferred)
                    _admission.Release(remoteIp, subnetKey);
            }
        }

        private void ReleaseAdmittedPeer(RlpxConnection connection)
        {
            if (_admittedConnections.TryRemove(connection, out var admitted))
            {
                _peerSlots.Release();
                connection.Disconnected -= OnConnectionDisconnected;
                _admission.Release(admitted.RemoteIp, admitted.SubnetKey);
            }
        }

        private void ReleaseAndDispose(RlpxConnection connection)
        {
            ReleaseAdmittedPeer(connection);
            try { connection.Dispose(); } catch { }
        }

        private void OnConnectionDisconnected(object? sender, EventArgs e)
        {
            if (sender is RlpxConnection conn)
                ReleaseAndDispose(conn);
        }

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
        }
    }

    public class RlpxListenerErrorEventArgs : EventArgs
    {
        public string Phase { get; }
        public Exception Exception { get; }

        public RlpxListenerErrorEventArgs(string phase, Exception ex)
        {
            Phase = phase;
            Exception = ex;
        }
    }
}
