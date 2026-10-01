using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.DevP2P;
using Nethereum.DevP2P.NodeDb;
using Nethereum.DevP2P.Netutil;
using Nethereum.DevP2P.Peering;

namespace Nethereum.DevP2P.Sync.Peering
{
    public sealed class PeerPoolManager : IPeerPool
    {
        private readonly IPeerHandshakeWorker _handshake;
        private readonly PeerPoolOptions _options;
        private readonly string[] _bootnodes;
        private readonly ILogger<PeerPoolManager> _logger;
        private readonly PersistentPeerCache? _peerCache;
        private readonly DialScheduler? _dialScheduler;
        private readonly HashSet<string> _trustedDialKeys;
        private readonly HashSet<string> _trustedNodeIds;

        private readonly SubnetTracker? _subnetTracker;
        private readonly ConcurrentDictionary<string, IPAddress> _peerAddressByEnode =
            new ConcurrentDictionary<string, IPAddress>(StringComparer.OrdinalIgnoreCase);

        private readonly Channel<string> _candidates;
        private readonly SemaphoreSlim _dialConcurrency;
        private readonly ConcurrentDictionary<string, DateTimeOffset> _banned =
            new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateTimeOffset> _recentDials =
            new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);

        private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan BannedRetention = TimeSpan.FromSeconds(30);
        private DateTimeOffset _lastSweepAt = DateTimeOffset.UtcNow;
        private readonly ConcurrentDictionary<string, byte> _inFlightDials =
            new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<Guid, IEthPeer> _activeByPoolId =
            new ConcurrentDictionary<Guid, IEthPeer>();
        private readonly ConcurrentDictionary<string, Guid> _activeByEnode =
            new ConcurrentDictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<Guid, DateTime> _connectedAtUtc =
            new ConcurrentDictionary<Guid, DateTime>();
        private readonly ConcurrentDictionary<Guid, DateTime> _lastSuccessUtc =
            new ConcurrentDictionary<Guid, DateTime>();
        private readonly Func<DateTime> _utcNow;

        public static readonly TimeSpan ResponsiveGrace = TimeSpan.FromMinutes(2);
        public static readonly TimeSpan NewPeerGrace = TimeSpan.FromSeconds(60);
        public static readonly TimeSpan TransportLivenessWindow = TimeSpan.FromSeconds(45);
        public const int HardCapAboveTarget = 4;
        public const int MinKeptActivePeers = 3;

        private CancellationTokenSource? _lifetime;
        private Task? _dialLoop;
        private Task? _trustedKeeperLoop;
        private int _started;

        private double _dialTokens;
        private DateTimeOffset _lastTokenRefill;
        private readonly object _tokenBucketLock = new object();

        public event EventHandler<IEthPeer>? PeerAdded;
        public event EventHandler<IEthPeer>? PeerRemoved;

        public IReadOnlyCollection<IEthPeer> ActivePeers => _activeByPoolId.Values.ToList();

        public IReadOnlyList<IEthPeer> ActivePeersByPreference
            => OrderByPreference(_activeByPoolId.Values, enode => GetScore(enode).ComputedScore);

        public static IReadOnlyList<IEthPeer> OrderByPreference(
            IEnumerable<IEthPeer> peers, Func<string, double> scoreOf)
            => peers
                .OrderByDescending(p => p.IsTrusted)
                .ThenByDescending(p => scoreOf(p.Enode))
                .ToList();

        public int TargetPeerCount => _options.TargetPeerCount;

        public bool IsPeerActive(Guid peerId) => _activeByPoolId.ContainsKey(peerId);

        public PeerPoolManager(
            IPeerHandshakeWorker handshake,
            PeerPoolOptions options,
            string[]? bootnodes = null,
            ILogger<PeerPoolManager>? logger = null,
            PersistentPeerCache? peerCache = null,
            DialScheduler? dialScheduler = null,
            IEnumerable<string>? trustedDialKeys = null,
            Func<DateTime>? utcNow = null)
        {
            _handshake = handshake ?? throw new ArgumentNullException(nameof(handshake));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _bootnodes = bootnodes ?? Array.Empty<string>();
            _logger = logger ?? NullLogger<PeerPoolManager>.Instance;
            _peerCache = peerCache;
            _dialScheduler = dialScheduler;
            _trustedDialKeys = new HashSet<string>(
                trustedDialKeys ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            _trustedNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in _trustedDialKeys)
                if (TryGetNodeId(key, out var nodeId))
                    _trustedNodeIds.Add(nodeId);
            _utcNow = utcNow ?? (() => DateTime.UtcNow);

            if (_options.MaxPeersPerIPv4Subnet > 0 || _options.MaxPeersPerIPv6Subnet > 0)
            {
                _subnetTracker = new SubnetTracker(
                    maxPerIPv4Subnet: _options.MaxPeersPerIPv4Subnet,
                    ipv4Prefix: _options.IPv4SubnetPrefix,
                    maxPerIPv6Subnet: _options.MaxPeersPerIPv6Subnet,
                    ipv6Prefix: _options.IPv6SubnetPrefix);
            }

            _candidates = Channel.CreateBounded<string>(new BoundedChannelOptions(_options.CandidateQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false,
            });
            _dialConcurrency = new SemaphoreSlim(_options.MaxConcurrentDials, _options.MaxConcurrentDials);

            _dialTokens = Math.Max(0, _options.DialBudgetPerSecond);
            _lastTokenRefill = DateTimeOffset.UtcNow;
        }

        public Task StartAsync(CancellationToken ct)
        {
            if (Interlocked.Exchange(ref _started, 1) == 1) return Task.CompletedTask;

            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);

            if (_peerCache is not null)
            {
                foreach (var enode in _peerCache.GetPreferredEnodes(_options.CandidateQueueCapacity))
                    TryEnqueueCandidate(enode);
            }
            foreach (var enode in _bootnodes)
                TryEnqueueCandidate(enode);

            _dialLoop = Task.Run(() => DialLoopAsync(_lifetime.Token), _lifetime.Token);
            _trustedKeeperLoop = Task.Run(() => TrustedPeerKeeperLoopAsync(_lifetime.Token), _lifetime.Token);
            return Task.CompletedTask;
        }

        private async Task TrustedPeerKeeperLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(_options.EffectiveTrustedRedialInterval, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }

                foreach (var enode in _trustedDialKeys)
                {
                    var result = await TryDialCandidateAsync(enode, ct).ConfigureAwait(false);
                    if (result == DialClaimResult.Canceled) return;
                    if (result == DialClaimResult.Dialed)
                        _logger.LogInformation("trusted peer keeper: dialing directly {Host}",
                            SyncPeerSession.ParseHost(enode));
                }

                await SweepUnresponsivePeersAsync(ct).ConfigureAwait(false);
            }
        }

        private bool IsResponsiveActive(IEthPeer peer, DateTime now)
        {
            if (_lastSuccessUtc.TryGetValue(peer.Id, out var lastSuccess)
                && (now - lastSuccess) < ResponsiveGrace)
                return true;
            if (_connectedAtUtc.TryGetValue(peer.Id, out var connectedAt)
                && (now - connectedAt) < NewPeerGrace)
                return true;
            return (now - peer.LastFrameReceivedUtc) < TransportLivenessWindow;
        }

        private int ResponsiveActiveCount()
        {
            var now = _utcNow();
            var count = 0;
            foreach (var peer in _activeByPoolId.Values)
                if (IsResponsiveActive(peer, now)) count++;
            return count;
        }

        private bool ShouldPauseDialing()
        {
            var inFlight = _inFlightDials.Count;
            if (_activeByPoolId.Count + inFlight >= _options.TargetPeerCount + HardCapAboveTarget)
                return true;
            return ResponsiveActiveCount() + inFlight >= _options.TargetPeerCount;
        }

        private async Task SweepUnresponsivePeersAsync(CancellationToken ct)
        {
            var now = _utcNow();
            var floor = Math.Min(MinKeptActivePeers, _options.TargetPeerCount);
            foreach (var peerId in _activeByPoolId.Keys)
            {
                if (ct.IsCancellationRequested) return;
                if (!_activeByPoolId.TryGetValue(peerId, out var peer)) continue;
                if (peer.IsTrusted || IsTrustedEnode(peer.Enode)) continue;
                if (IsResponsiveActive(peer, now)) continue;
                if (_activeByPoolId.Count <= floor) continue;
                await DropAsync(peerId, "unresponsive past grace", ct).ConfigureAwait(false);
            }
        }

        public void ReportSuccess(Guid peerId)
            => _lastSuccessUtc[peerId] = _utcNow();

        public int GetResponsiveActiveCountForTest() => ResponsiveActiveCount();

        public bool ShouldPauseDialingForTest() => ShouldPauseDialing();

        public Task SweepUnresponsivePeersForTestAsync(CancellationToken ct)
            => SweepUnresponsivePeersAsync(ct);

        public bool IsBannedForTest(string enode) => _banned.ContainsKey(enode);

        private static bool TryGetNodeId(string enode, out string nodeId)
        {
            nodeId = string.Empty;
            if (string.IsNullOrWhiteSpace(enode)) return false;
            try
            {
                nodeId = EnodeUrl.Parse(enode).PeerId;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool IsTrustedEnode(string enode)
            => _trustedNodeIds.Count > 0
            && TryGetNodeId(enode, out var nodeId)
            && _trustedNodeIds.Contains(nodeId);

        private static string ActiveKeyFor(string enode)
            => TryGetNodeId(enode, out var nodeId) ? nodeId : enode;

        public PeerScore GetScore(string enode)
        {
            if (_peerCache is null || string.IsNullOrWhiteSpace(enode)) return PeerScore.Unknown;
            if (!_peerCache.TryGetEntry(enode, out var entry) || entry is null) return PeerScore.Unknown;

            var lastSeen = entry.LastSeenUnix > 0
                ? DateTimeOffset.FromUnixTimeSeconds(entry.LastSeenUnix)
                : DateTimeOffset.MinValue;
            var ageSeconds = Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeSeconds() - entry.LastSeenUnix);
            var recency = 1.0 / (1.0 + ageSeconds / 3600.0);
            var ratio = (1.0 + entry.SuccessfulConnects) / (1.0 + entry.FailedConnects);
            return new PeerScore(entry.SuccessfulConnects, entry.FailedConnects, lastSeen, recency * ratio);
        }

        public bool EnqueueCandidate(string enode)
        {
            if (string.IsNullOrWhiteSpace(enode)) return false;
            return _candidates.Writer.TryWrite(enode);
        }

        public Task BanAndDropAsync(string enode, string reason, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(enode)) return Task.CompletedTask;
            _banned[enode] = DateTimeOffset.UtcNow;
            _logger.LogWarning("peer banned: {Host} reason={Reason}", SyncPeerSession.ParseHost(enode), reason);

            if (_activeByEnode.TryGetValue(ActiveKeyFor(enode), out var peerId)
                && _activeByPoolId.TryGetValue(peerId, out var peer))
            {
                try { peer.Connection?.Dispose(); }
                catch { }
            }
            return Task.CompletedTask;
        }

        public Task DropAsync(Guid peerId, string reason, CancellationToken ct)
        {
            if (!_activeByPoolId.TryGetValue(peerId, out var peer)) return Task.CompletedTask;
            _logger.LogWarning("peer dropped (unresponsive): {Host} reason={Reason} trusted={Trusted}",
                SyncPeerSession.ParseHost(peer.Enode), reason, peer.IsTrusted || IsTrustedEnode(peer.Enode));
            try { peer.Connection?.Dispose(); }
            catch { }
            return Task.CompletedTask;
        }

        public Task ClearAllBansAsync()
        {
            _banned.Clear();
            _logger.LogInformation("ban list cleared");
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            if (_lifetime is null) return;
            try { _lifetime.Cancel(); }
            catch { }
            if (_dialLoop is not null)
            {
                try { await _dialLoop.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { _logger.LogError(ex, "dial loop terminated"); }
            }
            if (_trustedKeeperLoop is not null)
            {
                try { await _trustedKeeperLoop.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { _logger.LogError(ex, "trusted peer keeper terminated"); }
            }
            foreach (var peer in _activeByPoolId.Values)
            {
                try { peer.Connection?.Dispose(); } catch { }
            }
            _activeByPoolId.Clear();
            _activeByEnode.Clear();
            _connectedAtUtc.Clear();
            _lastSuccessUtc.Clear();
            _lifetime.Dispose();
            _dialConcurrency.Dispose();
        }

        private void TryEnqueueCandidate(string enode)
        {
            if (string.IsNullOrWhiteSpace(enode)) return;
            _candidates.Writer.TryWrite(enode);
        }

        private void MaybeSweepMaps()
        {
            var now = DateTimeOffset.UtcNow;
            if ((now - _lastSweepAt) < SweepInterval) return;
            _lastSweepAt = now;

            var recentEvictThreshold = _options.EffectivePerHostReDialGate.TotalSeconds * 5.0;
            int recentDropped = 0, bannedDropped = 0;

            foreach (var kv in _recentDials)
            {
                if ((now - kv.Value).TotalSeconds > recentEvictThreshold
                    && _recentDials.TryRemove(kv.Key, out _))
                    recentDropped++;
            }
            foreach (var kv in _banned)
            {
                if ((now - kv.Value) > BannedRetention
                    && _banned.TryRemove(kv.Key, out _))
                    bannedDropped++;
            }

            if (recentDropped > 0 || bannedDropped > 0)
            {
                _logger.LogDebug(
                    "pool maps swept: recentDials -{Recent} (size now {RecentSize}), banned -{Banned} (size now {BannedSize})",
                    recentDropped, _recentDials.Count, bannedDropped, _banned.Count);
            }
        }

        private async Task DialLoopAsync(CancellationToken ct)
        {
            var reader = _candidates.Reader;
            var batch = new List<string>(16);
            while (!ct.IsCancellationRequested)
            {
                MaybeSweepMaps();
                while (ShouldPauseDialing())
                {
                    try { await Task.Delay(50, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                }

                string? first = null;
                try
                {
                    if (!reader.TryRead(out first))
                    {
                        if (!await reader.WaitToReadAsync(ct).ConfigureAwait(false)) return;
                        reader.TryRead(out first);
                    }
                }
                catch (OperationCanceledException) { return; }
                if (first is null) continue;

                batch.Clear();
                batch.Add(first);
                while (batch.Count < 64 && reader.TryRead(out var extra) && extra is not null)
                    batch.Add(extra);

                if (batch.Count > 1) RankBatchByScoreDescending(batch);

                foreach (var candidate in batch)
                {
                    if (ct.IsCancellationRequested) return;
                    if (ShouldPauseDialing()) break;
                    var result = await TryDialCandidateAsync(candidate, ct).ConfigureAwait(false);
                    if (result == DialClaimResult.Canceled) return;
                }
            }
        }

        private enum DialClaimResult { Dialed, Skipped, Canceled }

        private async Task<DialClaimResult> TryDialCandidateAsync(string candidate, CancellationToken ct)
        {
            if (!TryClaimDialSlot(candidate)) return DialClaimResult.Skipped;

            var isTrustedCandidate = IsTrustedEnode(candidate);
            if (!TryReserveSubnetSlot(candidate, isTrustedCandidate, out var subnetAddress))
            {
                _logger.LogDebug(
                    "subnet diversity rejected {Host}", SyncPeerSession.ParseHost(candidate));
                _inFlightDials.TryRemove(ActiveKeyFor(candidate), out _);
                return DialClaimResult.Skipped;
            }
            if (subnetAddress != null)
                _peerAddressByEnode[candidate] = subnetAddress;

            try { await AwaitDialTokenAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                ReleaseSubnetSlot(candidate);
                _inFlightDials.TryRemove(ActiveKeyFor(candidate), out _);
                return DialClaimResult.Canceled;
            }

            DialCandidate? schedCandidate = null;
            if (_dialScheduler is not null)
            {
                schedCandidate = new DialCandidate(
                    candidate, IsTrustedEnode(candidate));
                bool reserved;
                try
                {
                    reserved = await _dialScheduler
                        .TryReserveSlotAsync(schedCandidate, ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    ReleaseSubnetSlot(candidate);
                    _inFlightDials.TryRemove(ActiveKeyFor(candidate), out _);
                    return DialClaimResult.Canceled;
                }
                if (!reserved)
                {
                    ReleaseSubnetSlot(candidate);
                    _inFlightDials.TryRemove(ActiveKeyFor(candidate), out _);
                    return DialClaimResult.Skipped;
                }
            }

            try { await _dialConcurrency.WaitAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                if (_dialScheduler is not null && schedCandidate is not null)
                    _dialScheduler.ReleaseSlot(schedCandidate, DialOutcome.Failure);
                ReleaseSubnetSlot(candidate);
                _inFlightDials.TryRemove(ActiveKeyFor(candidate), out _);
                return DialClaimResult.Canceled;
            }

            var enode = candidate;
            var capturedSchedCandidate = schedCandidate;
            _ = Task.Run(() => DialOneAsync(enode, capturedSchedCandidate, ct), ct);
            return DialClaimResult.Dialed;
        }

        private void RankBatchByScoreDescending(List<string> batch)
        {
            batch.Sort((a, b) => GetScore(b).ComputedScore.CompareTo(GetScore(a).ComputedScore));
        }

        private async Task AwaitDialTokenAsync(CancellationToken ct)
        {
            if (_options.DialBudgetPerSecond <= 0) return;
            var burst = (double)_options.DialBudgetPerSecond;
            while (true)
            {
                double waitMs;
                lock (_tokenBucketLock)
                {
                    var now = DateTimeOffset.UtcNow;
                    var elapsed = (now - _lastTokenRefill).TotalSeconds;
                    if (elapsed > 0)
                    {
                        _dialTokens = Math.Min(burst, _dialTokens + elapsed * _options.DialBudgetPerSecond);
                        _lastTokenRefill = now;
                    }
                    if (_dialTokens >= 1.0)
                    {
                        _dialTokens -= 1.0;
                        return;
                    }
                    waitMs = Math.Max(1.0, (1.0 - _dialTokens) * 1000.0 / _options.DialBudgetPerSecond);
                }
                try { await Task.Delay(TimeSpan.FromMilliseconds(waitMs), ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
            }
        }

        private bool TryClaimDialSlot(string enode)
        {
            var isTrusted = IsTrustedEnode(enode);
            if (!isTrusted
                && _banned.TryGetValue(enode, out var bannedAt)
                && (DateTimeOffset.UtcNow - bannedAt) < BannedRetention)
                return false;
            if (_activeByEnode.ContainsKey(ActiveKeyFor(enode))) return false;
            if (_inFlightDials.ContainsKey(ActiveKeyFor(enode))) return false;
            if (!isTrusted
                && _recentDials.TryGetValue(ActiveKeyFor(enode), out var when)
                && (DateTimeOffset.UtcNow - when) < _options.EffectivePerHostReDialGate)
                return false;
            return _inFlightDials.TryAdd(ActiveKeyFor(enode), 0);
        }

        private bool TryReserveSubnetSlot(string enode, bool isTrusted, out IPAddress? resolvedAddress)
        {
            resolvedAddress = null;
            if (_subnetTracker == null) return true;
            if (isTrusted) return true;

            try
            {
                var host = SyncPeerSession.ParseHost(enode);
                var colonIdx = host.IndexOf(':');
                if (colonIdx >= 0) host = host.Substring(0, colonIdx);
                if (!IPAddress.TryParse(host, out var addr)) return true;
                resolvedAddress = addr;
                return _subnetTracker.TryAdd(addr);
            }
            catch
            {
                return true;
            }
        }

        private async Task DialOneAsync(
            string enode, DialCandidate? schedCandidate, CancellationToken ct)
        {
            var outcome = DialOutcome.Failure;
            try
            {
                _recentDials[ActiveKeyFor(enode)] = DateTimeOffset.UtcNow;
                var peer = await _handshake.HandshakeAsync(
                    enode, _options.EffectiveDialTimeout, _options.MinPeerLatestBlock, ct)
                    .ConfigureAwait(false);

                var mp = peer as SyncPeerSession;
                if (mp != null && IsTrustedEnode(enode)) mp.MarkTrusted();

                peer.Disconnected += OnPeerDisconnected;

                _activeByEnode[ActiveKeyFor(enode)] = peer.Id;
                _activeByPoolId[peer.Id] = peer;
                _connectedAtUtc[peer.Id] = _utcNow();
                _lastSuccessUtc[peer.Id] = _utcNow();
                _peerCache?.RecordSuccess(enode);
                _dialScheduler?.OnPeerConnected(enode, PeerDirection.Outbound);
                outcome = DialOutcome.Success;
                _logger.LogInformation("peer added: {Host} eth/{EthVersion} latest={LatestBlock} caps=[{Caps}] supportsSnap={SupportsSnap} client={Client} trusted={Trusted}",
                    SyncPeerSession.ParseHost(enode), peer.EthVersion, peer.PeerLatestBlock,
                    mp?.CapabilitiesDescription ?? "?", mp?.SupportsSnap ?? false, mp?.PeerClientId ?? "?", peer.IsTrusted);
                PeerAdded?.Invoke(this, peer);

                if (peer.Connection != null && peer.Connection.IsDisconnected)
                    OnPeerDisconnected(this, peer);
            }
            catch (SyncPeerSession.UselessPeerException ex)
            {
                var trusted = IsTrustedEnode(enode);
                if (!trusted)
                    _banned[enode] = DateTimeOffset.UtcNow;
                _peerCache?.RecordFailure(enode);
                _logger.LogWarning("useless peer{TrustedNote}: {Host} reason={Reason}",
                    trusted ? " (trusted, not banned)" : " banned",
                    SyncPeerSession.ParseHost(enode), ex.Message);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _peerCache?.RecordFailure(enode);
                _logger.LogDebug("dial failed: {Host} error={ErrorType}: {Error}",
                    SyncPeerSession.ParseHost(enode), ex.GetType().Name, ex.Message);
            }
            finally
            {
                if (schedCandidate is not null && _dialScheduler is not null)
                    _dialScheduler.ReleaseSlot(schedCandidate, outcome);
                if (outcome != DialOutcome.Success)
                    ReleaseSubnetSlot(enode);
                _inFlightDials.TryRemove(ActiveKeyFor(enode), out _);
                _dialConcurrency.Release();
            }
        }

        private void ReleaseSubnetSlot(string enode)
        {
            if (_subnetTracker == null) return;
            if (_peerAddressByEnode.TryRemove(enode, out var addr) && addr != null)
                _subnetTracker.Remove(addr);
        }

        private void OnPeerDisconnected(object? sender, IEthPeer peer)
        {
            if (!_activeByPoolId.TryRemove(peer.Id, out _)) return;
            _connectedAtUtc.TryRemove(peer.Id, out _);
            _lastSuccessUtc.TryRemove(peer.Id, out _);
            var enode = peer.Enode;
            if (!string.IsNullOrEmpty(enode))
            {
                _activeByEnode.TryRemove(ActiveKeyFor(enode), out _);
                _dialScheduler?.OnPeerDisconnected(enode, PeerDirection.Outbound);
                ReleaseSubnetSlot(enode);
            }
            _logger.LogInformation("peer removed: {Host}", SyncPeerSession.ParseHost(enode));
            PeerRemoved?.Invoke(this, peer);

            if (!string.IsNullOrEmpty(enode) && IsTrustedEnode(enode))
            {
                _recentDials.TryRemove(ActiveKeyFor(enode), out _);
                if (_candidates.Writer.TryWrite(enode))
                    _logger.LogInformation("trusted peer disconnect: re-enqueued for redial {Host}",
                        SyncPeerSession.ParseHost(enode));
            }

            try { peer.Connection?.Dispose(); }
            catch { }
        }
    }
}
