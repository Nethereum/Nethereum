using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.DevP2P.NodeDb;
using Nethereum.DevP2P.Peering;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Maintains a target number of healthy eth-handshaken peers in a
    /// background dial loop. The dialer pulls candidates from a bounded
    /// channel, gates concurrency, skips recently-dialed and banned enodes,
    /// and replaces peers on disconnect. Combine with
    /// <see cref="Nethereum.DevP2P.NodeDb.PersistentPeerCache"/> for
    /// score-ordered candidate prioritisation; on its own the pool tracks
    /// only banned vs not-banned plus the optional useless-peer floor.
    /// </summary>
    public sealed class PeerPoolManager : IPeerPool
    {
        private readonly IPeerHandshakeWorker _handshake;
        private readonly PeerPoolOptions _options;
        private readonly string[] _bootnodes;
        private readonly ILogger<PeerPoolManager> _logger;
        private readonly PersistentPeerCache? _peerCache;
        // Optional geth-style outbound dial scheduler (concurrent-cap +
        // inbound/outbound ratio + recent-dial history + trusted bypass).
        // When null the pool falls back to its built-in concurrency /
        // cooldown / banned gates only; layering is additive so existing
        // tests and call sites are unaffected.
        private readonly DialScheduler? _dialScheduler;
        private readonly HashSet<string> _trustedDialKeys;

        private readonly Channel<string> _candidates;
        private readonly SemaphoreSlim _dialConcurrency;
        private readonly ConcurrentDictionary<string, DateTimeOffset> _banned =
            new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateTimeOffset> _recentDials =
            new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);

        // Bounded-sweep cadence: prune _recentDials and _banned every
        // 60 seconds to stop both maps growing unbounded over multi-hour
        // syncs. _recentDials entries older than 5x the per-host re-dial
        // gate are dropped (they're guaranteed not in cooldown). _banned
        // entries older than BannedRetention age out (geth-style: bans
        // are not permanent — a peer that was bad an hour ago may be
        // running clean code now).
        private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan BannedRetention = TimeSpan.FromHours(1);
        private DateTimeOffset _lastSweepAt = DateTimeOffset.UtcNow;
        private readonly ConcurrentDictionary<string, byte> _inFlightDials =
            new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<Guid, IEthPeer> _activeByPoolId =
            new ConcurrentDictionary<Guid, IEthPeer>();
        private readonly ConcurrentDictionary<string, Guid> _activeByEnode =
            new ConcurrentDictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        private CancellationTokenSource? _lifetime;
        private Task? _dialLoop;
        private int _started;

        // Token bucket for outbound dial rate limiting. Tokens refill at
        // _options.DialBudgetPerSecond per second up to the same cap, so a
        // brief idle period earns enough budget to satisfy a burst when a
        // new batch of candidates lands. 0 budget disables the gate.
        private double _dialTokens;
        private DateTimeOffset _lastTokenRefill;
        private readonly object _tokenBucketLock = new object();

        public event EventHandler<IEthPeer>? PeerAdded;
        public event EventHandler<IEthPeer>? PeerRemoved;

        public IReadOnlyCollection<IEthPeer> ActivePeers => _activeByPoolId.Values.ToList();
        public int TargetPeerCount => _options.TargetPeerCount;

        public PeerPoolManager(
            IPeerHandshakeWorker handshake,
            PeerPoolOptions options,
            string[]? bootnodes = null,
            ILogger<PeerPoolManager>? logger = null,
            PersistentPeerCache? peerCache = null,
            DialScheduler? dialScheduler = null,
            IEnumerable<string>? trustedDialKeys = null)
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

            _candidates = Channel.CreateBounded<string>(new BoundedChannelOptions(_options.CandidateQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false,
            });
            _dialConcurrency = new SemaphoreSlim(_options.MaxConcurrentDials, _options.MaxConcurrentDials);

            // Start the bucket at the full burst so the very first batch of
            // candidates after StartAsync is not rate-limited unnecessarily.
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
            return Task.CompletedTask;
        }

        /// <summary>
        /// Read the cached score for an enode. Returns <see cref="PeerScore.Unknown"/>
        /// when the persistent peer cache is absent or has no record of the enode.
        /// Stage 3's FetchRequestScheduler uses this to break ties when choosing
        /// a peer for a request batch.
        /// </summary>
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

        /// <summary>
        /// Enqueue an enode for the dial loop to consider. Used by external
        /// discovery sources (Stage 1+: discv4 FINDNODE callbacks, DNS tree
        /// resolution, persistent peer cache refills) to feed candidates
        /// without re-instantiating the pool. Banned enodes / already-active /
        /// in-flight / on-cooldown are still rejected at dial time by
        /// TryClaimDialSlot — this method only writes to the channel.
        /// </summary>
        public bool EnqueueCandidate(string enode)
        {
            if (string.IsNullOrWhiteSpace(enode)) return false;
            return _candidates.Writer.TryWrite(enode);
        }

        public Task BanAndDropAsync(string enode, string reason, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(enode)) return Task.CompletedTask;
            _banned[enode] = DateTimeOffset.UtcNow;
            _logger.LogWarning("peer banned: {Host} reason={Reason}", MainnetPeerSession.ParseHost(enode), reason);

            if (_activeByEnode.TryGetValue(enode, out var peerId)
                && _activeByPoolId.TryGetValue(peerId, out var peer))
            {
                try { peer.Connection?.Dispose(); }
                catch { /* already gone */ }
            }
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
            catch { /* already cancelled */ }
            if (_dialLoop is not null)
            {
                try { await _dialLoop.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { _logger.LogError(ex, "dial loop terminated"); }
            }
            foreach (var peer in _activeByPoolId.Values)
            {
                try { peer.Connection?.Dispose(); } catch { }
            }
            _activeByPoolId.Clear();
            _activeByEnode.Clear();
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
                while (_activeByPoolId.Count + _inFlightDials.Count >= _options.TargetPeerCount)
                {
                    try { await Task.Delay(50, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                }

                // 1) Block until at least one candidate is available, then
                //    opportunistically drain whatever else has piled up so we
                //    can rank them by score before dialing.
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

                // 2) Sort by descending cached score. Unknown / cache-absent
                //    peers fall to the end so warm-cache wins are dialed first
                //    on restart and known-bad-success-ratio peers are tried last.
                if (batch.Count > 1) RankBatchByScoreDescending(batch);

                foreach (var candidate in batch)
                {
                    if (ct.IsCancellationRequested) return;
                    if (!TryClaimDialSlot(candidate)) continue;

                    // 3) Token bucket — gate the global outbound dial rate so
                    //    we don't hammer the network even when the pool dips
                    //    and the candidate stream is hot.
                    try { await AwaitDialTokenAsync(ct).ConfigureAwait(false); }
                    catch (OperationCanceledException)
                    {
                        _inFlightDials.TryRemove(candidate, out _);
                        return;
                    }

                    // 4) Geth-style dial scheduler — concurrent-cap +
                    //    inbound/outbound ratio + recent-dial history +
                    //    trusted bypass. Optional; when null the pool falls
                    //    through to the per-host gates only.
                    DialCandidate? schedCandidate = null;
                    if (_dialScheduler is not null)
                    {
                        schedCandidate = new DialCandidate(
                            candidate, _trustedDialKeys.Contains(candidate));
                        bool reserved;
                        try
                        {
                            reserved = await _dialScheduler
                                .TryReserveSlotAsync(schedCandidate, ct)
                                .ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            _inFlightDials.TryRemove(candidate, out _);
                            return;
                        }
                        if (!reserved)
                        {
                            // Suppressed by history / ratio — skip; the
                            // candidate will be re-enqueued by external
                            // discovery sources when admissible again.
                            _inFlightDials.TryRemove(candidate, out _);
                            continue;
                        }
                    }

                    try { await _dialConcurrency.WaitAsync(ct).ConfigureAwait(false); }
                    catch (OperationCanceledException)
                    {
                        if (_dialScheduler is not null && schedCandidate is not null)
                            _dialScheduler.ReleaseSlot(schedCandidate, DialOutcome.Failure);
                        _inFlightDials.TryRemove(candidate, out _);
                        return;
                    }

                    var enode = candidate;
                    var capturedSchedCandidate = schedCandidate;
                    _ = Task.Run(() => DialOneAsync(enode, capturedSchedCandidate, ct), ct);
                }
            }
        }

        private void RankBatchByScoreDescending(List<string> batch)
        {
            // GetScore returns PeerScore.Unknown when the cache is absent or
            // has no record — those compare equal so insertion order is stable
            // among unknowns.
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
                    // Sleep until exactly one token will be available.
                    waitMs = Math.Max(1.0, (1.0 - _dialTokens) * 1000.0 / _options.DialBudgetPerSecond);
                }
                try { await Task.Delay(TimeSpan.FromMilliseconds(waitMs), ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
            }
        }

        private bool TryClaimDialSlot(string enode)
        {
            var isTrusted = _trustedDialKeys.Contains(enode);
            if (!isTrusted
                && _banned.TryGetValue(enode, out var bannedAt)
                && (DateTimeOffset.UtcNow - bannedAt) < BannedRetention)
                return false;
            if (_activeByEnode.ContainsKey(enode)) return false;
            if (_inFlightDials.ContainsKey(enode)) return false;
            if (!isTrusted
                && _recentDials.TryGetValue(enode, out var when)
                && (DateTimeOffset.UtcNow - when) < _options.EffectivePerHostReDialGate)
                return false;
            return _inFlightDials.TryAdd(enode, 0);
        }

        private async Task DialOneAsync(
            string enode, DialCandidate? schedCandidate, CancellationToken ct)
        {
            var outcome = DialOutcome.Failure;
            try
            {
                _recentDials[enode] = DateTimeOffset.UtcNow;
                var peer = await _handshake.HandshakeAsync(
                    enode, _options.EffectiveDialTimeout, _options.MinPeerLatestBlock, ct)
                    .ConfigureAwait(false);

                _activeByPoolId[peer.Id] = peer;
                _activeByEnode[enode] = peer.Id;
                peer.Disconnected += OnPeerDisconnected;
                _peerCache?.RecordSuccess(enode);
                _dialScheduler?.OnPeerConnected(enode, PeerDirection.Outbound);
                outcome = DialOutcome.Success;
                _logger.LogInformation("peer added: {Host} eth/{EthVersion} latest={LatestBlock}",
                    MainnetPeerSession.ParseHost(enode), peer.EthVersion, peer.PeerLatestBlock);
                PeerAdded?.Invoke(this, peer);
            }
            catch (MainnetPeerSession.UselessPeerException ex)
            {
                _banned[enode] = DateTimeOffset.UtcNow;
                _peerCache?.RecordFailure(enode);
                _logger.LogWarning("useless peer banned: {Host} reason={Reason}",
                    MainnetPeerSession.ParseHost(enode), ex.Message);
            }
            catch (OperationCanceledException) { /* shutdown */ }
            catch (Exception ex)
            {
                _peerCache?.RecordFailure(enode);
                _logger.LogDebug("dial failed: {Host} error={ErrorType}: {Error}",
                    MainnetPeerSession.ParseHost(enode), ex.GetType().Name, ex.Message);
            }
            finally
            {
                if (schedCandidate is not null && _dialScheduler is not null)
                    _dialScheduler.ReleaseSlot(schedCandidate, outcome);
                _inFlightDials.TryRemove(enode, out _);
                _dialConcurrency.Release();
            }
        }

        private void OnPeerDisconnected(object? sender, IEthPeer peer)
        {
            if (!_activeByPoolId.TryRemove(peer.Id, out _)) return;
            var enode = peer.Enode;
            if (!string.IsNullOrEmpty(enode))
            {
                _activeByEnode.TryRemove(enode, out _);
                _dialScheduler?.OnPeerDisconnected(enode, PeerDirection.Outbound);
            }
            _logger.LogInformation("peer removed: {Host}", MainnetPeerSession.ParseHost(enode));
            PeerRemoved?.Invoke(this, peer);

            if (!string.IsNullOrEmpty(enode) && _trustedDialKeys.Contains(enode))
            {
                _recentDials.TryRemove(enode, out _);
                if (_candidates.Writer.TryWrite(enode))
                    _logger.LogInformation("trusted peer disconnect: re-enqueued for redial {Host}",
                        MainnetPeerSession.ParseHost(enode));
            }
        }
    }
}
