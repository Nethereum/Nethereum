using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv4;
using Nethereum.DevP2P.Dns;
using Nethereum.DevP2P.NodeDb;
using Nethereum.DevP2P.Sync;
using Nethereum.Model;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P.SyncNode
{
    /// <summary>
    /// Resilient wrapper around <see cref="MainnetPeerSession"/>. Holds the current
    /// peer connection, and on transient errors (timeout, disconnect, malformed
    /// response) disposes it and dials the next bootnode in a round-robin queue
    /// before retrying. Non-transient errors (e.g. header chain breaks raised by
    /// the caller after a successful response) are not retried — those bubble up.
    /// </summary>
    internal sealed class RotatingPeerSession : IAsyncDisposable
    {
        private readonly List<string> _peers;
        private readonly HashSet<string> _seenPeers;
        // Banned for the lifetime of this run (e.g. peers reporting Latest=0,
        // peers behind our chain head). Cleared on process restart so peers
        // that catch up can be re-tried.
        private readonly HashSet<string> _bannedPeers = new(StringComparer.OrdinalIgnoreCase);
        private readonly string[] _seedBootnodes;
        private readonly TimeSpan _peerTimeout;
        private readonly int _maxAttempts;
        private readonly Action<string> _log;
        private readonly PeerDiscoveryService _discovery;
        private readonly EnrTreeResolver _dnsResolver;
        private readonly PersistentPeerCache _peerCache;
        private MainnetPeerSession _current;
        private string _currentEnode;
        private bool _dnsSeeded;
        private int _nextIdx;

        /// <summary>
        /// Minimum reported peer head required to accept a connection.
        /// Set from outside (e.g. SyncNode current block + buffer) so the
        /// rotator never accepts peers that can't actually move us forward.
        /// </summary>
        public ulong MinPeerLatestBlock { get; set; }

        public string PeerHost => _current?.PeerHost;
        public string PeerClientId => _current?.PeerClientId;
        public int EthVersion => _current?.EthVersion ?? 0;
        public ulong PeerLatestBlock => _current?.PeerLatestBlock ?? 0UL;
        public uint PeerForkHash => _current?.PeerForkHash ?? 0u;

        public RotatingPeerSession(
            string[] bootnodes, TimeSpan peerTimeout, int maxAttempts, Action<string> log,
            string peerCachePath = null)
        {
            if (bootnodes == null || bootnodes.Length == 0)
                throw new ArgumentException("At least one bootnode required", nameof(bootnodes));
            if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
            _seedBootnodes = bootnodes;
            _peers = new List<string>();
            _seenPeers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _peerTimeout = peerTimeout;
            _maxAttempts = maxAttempts;
            _log = log ?? (_ => { });
            _discovery = new PeerDiscoveryService(log);
            _dnsResolver = new EnrTreeResolver(log);

            // Persistent peer cache first (known-good peers from prior runs).
            if (!string.IsNullOrEmpty(peerCachePath))
            {
                _peerCache = new PersistentPeerCache(peerCachePath, log);
                _peerCache.Load();
                foreach (var enode in _peerCache.GetPreferredEnodes(maxCount: 200))
                {
                    if (_seenPeers.Add(enode)) _peers.Add(enode);
                }
                if (_peers.Count > 0) _log($"Peer cache seeded {_peers.Count} known-good enodes.");
            }
            // Static bootnodes go in next as a guaranteed-known fallback.
            foreach (var enode in bootnodes)
            {
                if (_seenPeers.Add(enode)) _peers.Add(enode);
            }
        }

        private async Task SeedFromDnsAsync(CancellationToken ct)
        {
            if (_dnsSeeded) return;
            _dnsSeeded = true;
            // Resolve ALL Ethereum Foundation ENR trees in parallel.
            // Geth resolves the same three (all / snap / les) by default.
            // Mainnet dial-success rate is ~0.3%, so peer-pool diversity is
            // the key lever for finding a connectable canonical peer.
            _log($"Seeding peer pool from {EnrTreeResolver.MainnetEnrTrees.Length} EIP-1459 DNS trees …");
            int totalAdded = 0;
            foreach (var tree in EnrTreeResolver.MainnetEnrTrees)
            {
                try
                {
                    var dnsEnodes = await _dnsResolver.ResolveAsync(
                        tree, TimeSpan.FromSeconds(5), maxLeaves: 200, ct);
                    int added = 0;
                    lock (_peers)
                    {
                        foreach (var enode in dnsEnodes)
                        {
                            if (_seenPeers.Add(enode)) { _peers.Add(enode); added++; }
                        }
                    }
                    totalAdded += added;
                }
                catch (Exception ex)
                {
                    _log($"  DNS seed failed for {tree.Substring(tree.IndexOf('@') + 1)}: {ex.GetType().Name}: {ex.Message}");
                }
            }
            _log($"  DNS seed added {totalAdded} enodes total (pool now {_peers.Count}).");
        }

        public Task ConnectAsync(CancellationToken ct) => EnsureConnectedAsync(ct);

        public Task<List<BlockHeader>> GetHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct)
            => InvokeWithRotationAsync(p => p.GetHeadersAsync(startBlock, limit, ct), ct);

        public Task<List<BlockBody>> GetBodiesAsync(List<byte[]> hashes, CancellationToken ct)
            => InvokeWithRotationAsync(p => p.GetBodiesAsync(hashes, ct), ct);

        private async Task<T> InvokeWithRotationAsync<T>(Func<MainnetPeerSession, Task<T>> op, CancellationToken ct)
        {
            Exception last = null;
            for (int attempt = 1; attempt <= _maxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                await EnsureConnectedAsync(ct);
                try
                {
                    return await op(_current);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (IsTransient(ex))
                {
                    last = ex;
                    _log($"  peer error ({_current?.PeerHost}): {ex.GetType().Name}: {ex.Message} — rotating to next peer (attempt {attempt}/{_maxAttempts})");
                    await DropCurrentAsync();
                }
            }
            throw new InvalidOperationException(
                $"Peer call failed after {_maxAttempts} rotations. Last: {last?.Message}", last);
        }

        private async Task EnsureConnectedAsync(CancellationToken ct)
        {
            if (_current != null) return;

            // First-call seeding: pull the live EIP-1459 ENR tree before we
            // start dialing. Cheap (3 DNS lookups) and bumps the pool from 4
            // to ~150-200 fresh enodes, so we don't hammer the same Geth
            // bootnodes on every retry.
            await SeedFromDnsAsync(ct);

            // Outer loop: when the entire pool is exhausted, back off (lets local
            // TCP / ISP rate-limits drain) and also run a discv4 FINDNODE pass
            // against the seed bootnodes to expand the pool with fresh enodes.
            int round = 0;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                Exception last = null;
                int poolSize;
                lock (_peers) { poolSize = _peers.Count; }
                for (int n = 0; n < poolSize; n++)
                {
                    ct.ThrowIfCancellationRequested();
                    string enode;
                    lock (_peers)
                    {
                        if (_peers.Count == 0) break;
                        enode = _peers[_nextIdx % _peers.Count];
                        _nextIdx = (_nextIdx + 1) % _peers.Count;
                    }
                    if (_bannedPeers.Contains(enode)) continue;  // skip useless peers banned for this run
                    var host = MainnetPeerSession.ParseHost(enode);
                    try
                    {
                        _log($"Dialing {host} …");
                        _current = await MainnetPeerSession.ConnectAsync(enode, _peerTimeout, ct, MinPeerLatestBlock);
                        _currentEnode = enode;
                        _peerCache?.RecordSuccess(enode);
                        _log($"Connected to {_current.PeerHost} via eth/{_current.EthVersion}");
                        _log($"  ClientId     = '{_current.PeerClientId}'");
                        _log($"  Latest block = {_current.PeerLatestBlock:N0}");
                        _log($"  ForkHash     = 0x{_current.PeerForkHash:x8}");
                        return;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (MainnetPeerSession.UselessPeerException ex)
                    {
                        // Peer reported a head behind ours (or Latest=0). Ban
                        // for this run so the rotator never re-tries it.
                        _bannedPeers.Add(enode);
                        _peerCache?.RecordFailure(enode);
                        _log($"  {host} useless ({ex.Message}) — banned for this run");
                        last = ex;
                    }
                    catch (Exception ex)
                    {
                        _log($"  {host} failed: {ex.GetType().Name}: {ex.Message}");
                        _peerCache?.RecordFailure(enode);
                        last = ex;
                    }
                }

                round++;
                if (round >= _maxAttempts)
                {
                    throw new InvalidOperationException(
                        $"All {poolSize} peers refused or timed out across {round} rounds. Last: {last?.Message}", last);
                }
                int backoffSeconds = Math.Min(30, 1 << Math.Min(round, 5));
                _log($"All {poolSize} peers refused — re-resolving DNS tree and backing off {backoffSeconds}s before round {round + 1}.");

                // Refresh both sources in parallel with the backoff window.
                // DNS gets the EF-curated list (cheap, scales to hundreds).
                // discv4 falls back to active FINDNODE on the seeds we know are
                // up — useful when DNS is blocked.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var dnsFound = await _dnsResolver.ResolveAsync(
                            EnrTreeResolver.MainnetEnrTree, TimeSpan.FromSeconds(5),
                            maxLeaves: 200, CancellationToken.None);
                        int dnsAdded = 0;
                        lock (_peers)
                        {
                            foreach (var enode in dnsFound)
                            {
                                if (_seenPeers.Add(enode)) { _peers.Add(enode); dnsAdded++; }
                            }
                        }
                        if (dnsAdded > 0) _log($"  DNS re-resolve added {dnsAdded} (pool {_peers.Count}).");
                    }
                    catch (Exception ex)
                    {
                        _log($"  DNS re-resolve failed: {ex.GetType().Name}: {ex.Message}");
                    }
                }, ct);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var found = await _discovery.DiscoverAsync(
                            _seedBootnodes, TimeSpan.FromSeconds(5), CancellationToken.None);
                        int added = 0;
                        lock (_peers)
                        {
                            foreach (var enode in found)
                            {
                                if (_seenPeers.Add(enode)) { _peers.Add(enode); added++; }
                            }
                        }
                        if (added > 0) _log($"  discv4 added {added} (pool {_peers.Count}).");
                    }
                    catch (Exception ex)
                    {
                        _log($"  discv4 round failed: {ex.GetType().Name}: {ex.Message}");
                    }
                }, ct);

                await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), ct);
            }
        }

        /// <summary>
        /// Public escape hatch for callers that detect higher-level peer
        /// misbehaviour (e.g. header chain break) after a successful response.
        /// The next call to <see cref="GetHeadersAsync"/> /
        /// <see cref="GetBodiesAsync"/> will dial a fresh peer.
        /// </summary>
        public Task DropCurrentPeerAsync() => DropCurrentAsync();

        /// <summary>
        /// Clear all per-run bans. Used after a successful auto-rewind has
        /// restored canonical state — peers previously banned for "chain-break
        /// vs our state" may now agree with us (we WERE wrong, the rewind
        /// corrected it). Without this, every successful state-restore locks
        /// us out of the very peers that flagged the divergence (Task #184).
        /// </summary>
        public int ClearAllBans()
        {
            var count = _bannedPeers.Count;
            _bannedPeers.Clear();
            if (count > 0) _log($"  cleared {count} per-run peer ban(s) after state restore");
            return count;
        }

        /// <summary>
        /// Drop the current peer AND ban its enode for the rest of this run
        /// (same mechanism as the Latest&lt;head <see cref="MainnetPeerSession.UselessPeerException"/>
        /// path). Use when a peer delivers a chain-break or causes a
        /// state-root mismatch — the peer is serving us a forked chain and
        /// will keep doing so if we let the rotator re-dial it. Without this,
        /// a single peer that consistently disagrees can keep us in a hot
        /// reconnect loop forever (Task #176). The ban is per-run; the peer
        /// cache survives so the next process invocation may re-try them
        /// if their chain has since caught up to canonical.
        /// </summary>
        public async Task BanAndDropCurrentPeerAsync(string reason)
        {
            var enode = _currentEnode;
            if (!string.IsNullOrEmpty(enode))
            {
                _bannedPeers.Add(enode);
                _peerCache?.RecordFailure(enode);
                _log($"  banning {_current?.PeerHost ?? "<unknown>"} for this run: {reason}");
            }
            await DropCurrentAsync();
        }

        private async Task DropCurrentAsync()
        {
            var peer = _current;
            _current = null;
            if (peer == null) return;
            try { await peer.DisposeAsync(); }
            catch { /* peer was already gone */ }
        }

        private static bool IsTransient(Exception ex)
            => ex is IOException
            || ex is SocketException
            || ex is TimeoutException
            || ex is TaskCanceledException
            || ex is OperationCanceledException
            || ex is InvalidOperationException; // "Peer did not send BlockHeaders within N attempts"

        public async ValueTask DisposeAsync()
        {
            await DropCurrentAsync();
            try { _discovery?.Dispose(); } catch { }
            try { _peerCache?.Save(); } catch { }
        }
    }
}
