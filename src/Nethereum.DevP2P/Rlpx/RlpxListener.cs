using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.Signer;

namespace Nethereum.DevP2P.Rlpx
{
    /// <summary>
    /// Accepts inbound RLPx connections, completes the server-side handshake +
    /// Hello exchange, and raises a PeerAccepted event for each successful peer.
    /// Pair with a server-side eth/68 dispatch loop to act as a DevP2P node
    /// (e.g., AppChain sequencer publishing blocks, or a node serving block requests).
    ///
    /// DoS hardening:
    /// <list type="bullet">
    ///   <item><see cref="DevP2PConfig.MaxInboundPerIP"/> caps concurrent inbound
    ///     handshakes from any single IP. Sockets past the cap are closed before
    ///     the handshake reads a single byte.</item>
    ///   <item><see cref="DevP2PConfig.MaxPeers"/> caps total accepted peers.
    ///     Trusted peers (<see cref="DevP2PConfig.TrustedNodeIds"/>) bypass this
    ///     cap, but only after handshake reveals the remote node id — so the
    ///     per-IP throttle still gates the work the listener actually does.</item>
    /// </list>
    /// </summary>
    public class RlpxListener : IDisposable
    {
        private readonly EthECKey _localKey;
        private readonly DevP2PConfig _config;
        private readonly ConcurrentDictionary<IPAddress, int> _inboundByIp = new();
        // Per-/24 IPv4 subnet (or /48 IPv6 prefix) admission counter. Keyed by
        // the subnet network bytes (3 for v4, 6 for v6) joined with the family
        // byte so v4 and v6 keys never collide. Defends against an attacker
        // controlling an entire subnet from establishing MaxInboundPerIP × 256
        // concurrent inbound peers without ever tripping per-IP.
        private readonly ConcurrentDictionary<string, int> _inboundBySubnet = new();
        private readonly HashSet<string> _trustedNodeIds;
        private int _activePeers;
        private TcpListener? _listener;
        private CancellationTokenSource? _acceptCts;
        private Task? _acceptTask;
        // Tracks in-flight per-peer handshake tasks so StopAsync can await
        // them with a bounded timeout. Without this, a Stop while crypto is
        // running can outlive the listener and operate on disposed state.
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, Task> _handshakeTasks = new();
        private static readonly TimeSpan HandshakeStopTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Currently admitted peers (handshake + Hello complete, not yet
        /// disconnected). Trusted peers DO count toward this — they bypass
        /// the cap on admission, but every admitted peer is in the count
        /// thereafter. Exposed for tests and metrics.
        /// </summary>
        public int ActivePeers => Volatile.Read(ref _activePeers);

        /// <summary>Snapshot of current inbound count per remote IP. Exposed for metrics.</summary>
        public int CountInboundForIp(IPAddress ip)
            => _inboundByIp.TryGetValue(ip, out var n) ? n : 0;

        public IPEndPoint? LocalEndpoint => _listener?.LocalEndpoint as IPEndPoint;
        public int Port => LocalEndpoint?.Port ?? 0;
        public byte[] NodeId => _localKey.GetPubKeyNoPrefix();

        public event EventHandler<RlpxConnection>? PeerAccepted;
        public event EventHandler<RlpxListenerErrorEventArgs>? PeerFailed;

        public RlpxListener(EthECKey localKey, DevP2PConfig config)
        {
            _localKey = localKey ?? throw new ArgumentNullException(nameof(localKey));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            // Precompute the trusted-id set once. Node ids are 64-byte
            // secp256k1 pubkeys, hex-encoded without the "enode://" prefix
            // and without "0x" — same format as RlpxConnection.RemoteNodeId
            // produces via ToHex(). Case-insensitive so operators can paste
            // upper or lower hex from configs without subtle bugs.
            _trustedNodeIds = new HashSet<string>(
                (config.TrustedNodeIds ?? Array.Empty<string>())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim().ToLowerInvariant()),
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True if <paramref name="remoteNodeId"/> is in
        /// <see cref="DevP2PConfig.TrustedNodeIds"/>. Trusted peers bypass the
        /// <see cref="DevP2PConfig.MaxPeers"/> cap on admission. The per-IP
        /// throttle is NOT bypassed — that runs pre-handshake when the node
        /// id is still unknown, so operators must size
        /// <see cref="DevP2PConfig.MaxInboundPerIP"/> to leave room for
        /// trusted peers.
        /// </summary>
        public bool IsTrustedNodeId(byte[]? remoteNodeId)
            => remoteNodeId != null
            && _trustedNodeIds.Count > 0
            && _trustedNodeIds.Contains(remoteNodeId.ToHex().ToLowerInvariant());

        public void Start(int port = 0, IPAddress? bindAddress = null)
        {
            if (_listener != null)
                throw new InvalidOperationException("Listener already started");

            _listener = new TcpListener(bindAddress ?? IPAddress.Loopback, port);
            _listener.Start();

            _acceptCts = new CancellationTokenSource();
            _acceptTask = Task.Run(() => AcceptLoopAsync(_acceptCts.Token));
        }

        public async Task StopAsync()
        {
            if (_acceptCts == null) return;
            _acceptCts.Cancel();
            _listener?.Stop();
            try { if (_acceptTask != null) await _acceptTask; } catch { }

            // Drain any in-flight handshake tasks with a bounded timeout so
            // their cryptographic operations and socket writes can't outlive
            // the listener / operate on disposed state. Anything that hasn't
            // settled after HandshakeStopTimeout is abandoned.
            var pending = _handshakeTasks.Values.ToArray();
            if (pending.Length > 0)
            {
                try { await Task.WhenAny(Task.WhenAll(pending), Task.Delay(HandshakeStopTimeout)); }
                catch { }
            }

            _listener = null;
            _acceptCts.Dispose();
            _acceptCts = null;
            _acceptTask = null;
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient tcp;
                try
                {
                    tcp = await _listener!.AcceptTcpClientAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    PeerFailed?.Invoke(this, new RlpxListenerErrorEventArgs("AcceptTcp", ex));
                    continue;
                }

                // Per-IP throttle: reject sockets past the cap before the handshake reads a byte,
                // so an attacker IP cannot exhaust threads / memory by repeatedly
                // opening connections faster than handshakes time out.
                IPAddress? remoteIp = null;
                try { remoteIp = (tcp.Client.RemoteEndPoint as IPEndPoint)?.Address; }
                catch { /* socket may already be gone */ }

                if (remoteIp == null)
                {
                    // Couldn't resolve remote endpoint — be conservative, drop it.
                    try { tcp.Close(); } catch { }
                    continue;
                }

                // NetRestrict CIDR allow-list gate. Runs BEFORE the per-IP throttle so an
                // out-of-VPC peer never even reserves a per-IP slot. Empty list
                // means "no restriction" — Contains returns true and we fall
                // through to the throttle. For AppChain VPC deployments this
                // closes the socket on outside-VPC peers without spending any
                // handshake CPU.
                if (!_config.NetRestrict.Contains(remoteIp))
                {
                    PeerFailed?.Invoke(this, new RlpxListenerErrorEventArgs(
                        "InboundNetRestrict",
                        new InvalidOperationException(
                            $"Rejected inbound from {remoteIp}: not in NetRestrict allow-list.")));
                    try { tcp.Close(); } catch { }
                    continue;
                }

                // Reserve a slot for this IP. AddOrUpdate is atomic so concurrent
                // accepts on the same IP cannot all squeeze through past the cap.
                bool admitted = false;
                _inboundByIp.AddOrUpdate(
                    remoteIp,
                    _ => { admitted = true; return 1; },
                    (_, n) =>
                    {
                        if (n >= _config.MaxInboundPerIP) return n;
                        admitted = true;
                        return n + 1;
                    });

                if (!admitted)
                {
                    PeerFailed?.Invoke(this, new RlpxListenerErrorEventArgs(
                        "InboundPerIPCap",
                        new InvalidOperationException(
                            $"Rejected inbound from {remoteIp}: per-IP cap {_config.MaxInboundPerIP} reached.")));
                    try { tcp.Close(); } catch { }
                    continue;
                }

                // Reserve a slot for the /24 (or /48 IPv6) subnet. Run after the
                // per-IP gate so a single offending IP gets the cheaper rejection
                // first; per-subnet catches the Sybil/eclipse case where an
                // attacker spreads across an entire subnet without ever tripping
                // per-IP. Setting MaxInboundPerSubnet=0 disables the check.
                string? subnetKey = GetSubnetKey(remoteIp);
                bool subnetAdmitted = subnetKey == null || _config.MaxInboundPerSubnet <= 0;
                if (!subnetAdmitted)
                {
                    _inboundBySubnet.AddOrUpdate(
                        subnetKey,
                        _ => { subnetAdmitted = true; return 1; },
                        (_, n) =>
                        {
                            if (n >= _config.MaxInboundPerSubnet) return n;
                            subnetAdmitted = true;
                            return n + 1;
                        });
                }

                if (!subnetAdmitted)
                {
                    // Roll back the per-IP slot acquired above so a rejected
                    // subnet admission doesn't leak a per-IP reservation.
                    ReleaseInboundIpSlot(remoteIp);
                    PeerFailed?.Invoke(this, new RlpxListenerErrorEventArgs(
                        "InboundPerSubnetCap",
                        new InvalidOperationException(
                            $"Rejected inbound from {remoteIp}: per-subnet cap {_config.MaxInboundPerSubnet} reached.")));
                    try { tcp.Close(); } catch { }
                    continue;
                }

                var taskId = Guid.NewGuid();
                var handshakeTask = Task.Run(async () =>
                {
                    try { await HandlePeerAsync(tcp, remoteIp, subnetKey, ct).ConfigureAwait(false); }
                    finally { _handshakeTasks.TryRemove(taskId, out _); }
                });
                _handshakeTasks[taskId] = handshakeTask;
            }
        }

        private async Task HandlePeerAsync(TcpClient tcp, IPAddress remoteIp, string? subnetKey, CancellationToken ct)
        {
            // We only increment _activePeers AFTER the admission gate so the
            // counter reflects fully-admitted peers (geth's len(peers) in
            // postHandshakeChecks). In-flight handshakes are bounded by
            // _inboundByIp / MaxInboundPerIP instead.
            bool admittedToPeerSet = false;
            var connection = new RlpxConnection(_localKey, _config);
            try
            {
                await connection.AcceptIncomingAsync(tcp, ct);

                // MaxPeers admission. RemoteNodeId is populated by
                // AcceptIncomingAsync, so the trusted bypass check happens
                // here — not pre-handshake (we don't know the node id yet)
                // and not via IP (node id ≠ IP).
                bool trusted = IsTrustedNodeId(connection.RemoteNodeId);
                int currentPeers = Volatile.Read(ref _activePeers);
                if (!trusted && _config.MaxPeers > 0 && currentPeers >= _config.MaxPeers)
                {
                    // Polite disconnect — the peer learns it's not a network
                    // failure but a "we're full" rejection (geth uses the same
                    // reason in postHandshakeChecks).
                    try { await connection.DisconnectAsync(DisconnectReason.TooManyPeers); }
                    catch { /* peer already gone or write timed out */ }
                    PeerFailed?.Invoke(this, new RlpxListenerErrorEventArgs(
                        "MaxPeers",
                        new InvalidOperationException(
                            $"Rejected non-trusted inbound (peer count {currentPeers} >= MaxPeers {_config.MaxPeers}).")));
                    return;
                }

                Interlocked.Increment(ref _activePeers);
                admittedToPeerSet = true;
                // Subscribe to the connection's Disconnected event so we
                // decrement when the PEER actually goes away — not when
                // HandlePeerAsync exits (which happens immediately after
                // handshake because ownership transfers to the consumer
                // via PeerAccepted). Without this, ActivePeers would
                // collapse to 0 right after the handshake and MaxPeers
                // admission would always pass.
                connection.Disconnected += OnConnectionDisconnected;
                PeerAccepted?.Invoke(this, connection);
            }
            catch (Exception ex)
            {
                try { tcp.Close(); } catch { }
                PeerFailed?.Invoke(this, new RlpxListenerErrorEventArgs("Handshake", ex));
            }
            finally
            {
                // Note: _activePeers is NOT decremented here. It tracks
                // admitted peers (post-handshake, pre-disconnect); the
                // decrement happens in OnConnectionDisconnected. For
                // rejected/failed handshakes, admittedToPeerSet stays
                // false and we never incremented.
                // Release the per-IP slot (and per-subnet slot, if one was
                // reserved). We decrement-or-clear in a tight CAS loop: if
                // the counter is at 1 we try to remove the entry outright;
                // otherwise we decrement. Either way, a concurrent accept on
                // the same IP/subnet either sees the decremented value or
                // the post-removal "first connection again" path, never an
                // inconsistent state.
                ReleaseInboundIpSlot(remoteIp);
                if (subnetKey != null) ReleaseInboundSubnetSlot(subnetKey);
            }
        }

        private void ReleaseInboundIpSlot(IPAddress remoteIp)
        {
            while (true)
            {
                if (!_inboundByIp.TryGetValue(remoteIp, out var n)) break;
                if (n <= 1)
                {
                    var kvp = new System.Collections.Generic.KeyValuePair<IPAddress, int>(remoteIp, n);
                    if (((System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<IPAddress, int>>)_inboundByIp).Remove(kvp))
                        break;
                }
                else if (_inboundByIp.TryUpdate(remoteIp, n - 1, n))
                {
                    break;
                }
            }
        }

        private void ReleaseInboundSubnetSlot(string subnetKey)
        {
            while (true)
            {
                if (!_inboundBySubnet.TryGetValue(subnetKey, out var n)) break;
                if (n <= 1)
                {
                    var kvp = new System.Collections.Generic.KeyValuePair<string, int>(subnetKey, n);
                    if (((System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<string, int>>)_inboundBySubnet).Remove(kvp))
                        break;
                }
                else if (_inboundBySubnet.TryUpdate(subnetKey, n - 1, n))
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Returns a stable string key for the /24 IPv4 subnet (or /48 IPv6
        /// prefix) that <paramref name="ip"/> belongs to. v4 and v6 keys are
        /// prefixed with a family discriminator so they never collide. Returns
        /// null for unsupported address families.
        /// </summary>
        private static string? GetSubnetKey(IPAddress ip)
        {
            var bytes = ip.GetAddressBytes();
            if (bytes.Length == 4)
                return "4:" + bytes[0] + "." + bytes[1] + "." + bytes[2];
            if (bytes.Length == 16)
                return "6:" + bytes[0] + "." + bytes[1] + "." + bytes[2] + "."
                            + bytes[3] + "." + bytes[4] + "." + bytes[5];
            return null;
        }

        private void OnConnectionDisconnected(object? sender, EventArgs e)
        {
            // Decrement the admitted-peer counter. Idempotent because
            // RlpxConnection.MarkDisconnected only fires the event once.
            Interlocked.Decrement(ref _activePeers);
            if (sender is RlpxConnection conn)
            {
                conn.Disconnected -= OnConnectionDisconnected;
            }
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
