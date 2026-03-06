using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.AppChain.Sync
{
    public class PeerManager : IPeerManager
    {
        private readonly ConcurrentDictionary<string, SyncPeer> _peers = new();
        private readonly PeerManagerConfig _config;
        private readonly Func<string, ISequencerRpcClient> _clientFactory;
        private CancellationTokenSource? _healthCheckCts;
        private Task? _healthCheckTask;

        public event EventHandler<PeerStatusChangedEventArgs>? PeerStatusChanged;

        public PeerManager(PeerManagerConfig? config = null, Func<string, ISequencerRpcClient>? clientFactory = null)
        {
            _config = config ?? PeerManagerConfig.Default;
            _clientFactory = clientFactory ?? (url => new HttpSequencerRpcClient(url));
        }

        public IReadOnlyList<SyncPeer> Peers => _peers.Values.ToList();

        public bool AddPeer(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            var normalizedUrl = NormalizeUrl(url);
            if (_peers.ContainsKey(normalizedUrl))
                return false;

            var peer = new SyncPeer
            {
                Url = normalizedUrl,
                Client = _clientFactory(normalizedUrl),
                IsHealthy = false,
                BlockNumber = -1,
                LastSeen = DateTime.MinValue,
                AddedAt = DateTime.UtcNow
            };

            if (_peers.TryAdd(normalizedUrl, peer))
            {
                _ = CheckPeerHealthAsync(peer, CancellationToken.None);
                return true;
            }
            return false;
        }

        public bool RemovePeer(string url)
        {
            var normalizedUrl = NormalizeUrl(url);
            return _peers.TryRemove(normalizedUrl, out _);
        }

        public SyncPeer? GetBestPeer()
        {
            return _peers.Values
                .Where(p => p.IsHealthy)
                .OrderByDescending(p => p.BlockNumber)
                .ThenBy(p => p.LastCheckDuration)
                .FirstOrDefault();
        }

        public SyncPeer? GetPeer(string url)
        {
            var normalizedUrl = NormalizeUrl(url);
            return _peers.TryGetValue(normalizedUrl, out var peer) ? peer : null;
        }

        public ISequencerRpcClient? GetBestClient()
        {
            return GetBestPeer()?.Client;
        }

        public async Task<ISequencerRpcClient?> GetHealthyClientAsync(CancellationToken cancellationToken = default)
        {
            var bestPeer = GetBestPeer();
            if (bestPeer != null)
                return bestPeer.Client;

            foreach (var peer in _peers.Values.OrderBy(p => p.FailureCount))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await CheckPeerHealthAsync(peer, cancellationToken);
                if (peer.IsHealthy)
                    return peer.Client;
            }

            return null;
        }

        public async Task StartHealthCheckAsync(CancellationToken cancellationToken = default)
        {
            if (_healthCheckTask != null)
                return;

            _healthCheckCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _healthCheckTask = RunHealthCheckLoopAsync(_healthCheckCts.Token);
        }

        public async Task StopHealthCheckAsync()
        {
            _healthCheckCts?.Cancel();
            if (_healthCheckTask != null)
            {
                try
                {
                    await _healthCheckTask;
                }
                catch (OperationCanceledException) { }
            }
            _healthCheckCts?.Dispose();
            _healthCheckCts = null;
            _healthCheckTask = null;
        }

        public async Task CheckAllPeersAsync(CancellationToken cancellationToken = default)
        {
            var tasks = _peers.Values.Select(p => CheckPeerHealthAsync(p, cancellationToken));
            await Task.WhenAll(tasks);
        }

        private async Task RunHealthCheckLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_config.HealthCheckIntervalMs, cancellationToken);
                    await CheckAllPeersAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    // Continue health checking even on errors
                }
            }
        }

        private async Task CheckPeerHealthAsync(SyncPeer peer, CancellationToken cancellationToken)
        {
            var wasHealthy = peer.IsHealthy;
            var startTime = DateTime.UtcNow;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_config.HealthCheckTimeoutMs);

                var blockNumber = await peer.Client.GetBlockNumberAsync(cts.Token);

                peer.BlockNumber = blockNumber;
                peer.IsHealthy = true;
                peer.LastSeen = DateTime.UtcNow;
                peer.LastCheckDuration = DateTime.UtcNow - startTime;
                peer.LastError = null;
                peer.FailureCount = 0;
            }
            catch (Exception ex)
            {
                peer.IsHealthy = false;
                peer.LastError = ex.Message;
                peer.FailureCount++;
                peer.LastCheckDuration = DateTime.UtcNow - startTime;
            }

            if (wasHealthy != peer.IsHealthy)
            {
                PeerStatusChanged?.Invoke(this, new PeerStatusChangedEventArgs
                {
                    Peer = peer,
                    PreviousHealthy = wasHealthy,
                    CurrentHealthy = peer.IsHealthy
                });
            }
        }

        private static string NormalizeUrl(string url)
        {
            url = url.Trim().TrimEnd('/');
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "http://" + url;
            }
            return url.ToLowerInvariant();
        }
    }

    public interface IPeerManager
    {
        IReadOnlyList<SyncPeer> Peers { get; }
        bool AddPeer(string url);
        bool RemovePeer(string url);
        SyncPeer? GetBestPeer();
        SyncPeer? GetPeer(string url);
        ISequencerRpcClient? GetBestClient();
        Task<ISequencerRpcClient?> GetHealthyClientAsync(CancellationToken cancellationToken = default);
        Task StartHealthCheckAsync(CancellationToken cancellationToken = default);
        Task StopHealthCheckAsync();
        Task CheckAllPeersAsync(CancellationToken cancellationToken = default);
        event EventHandler<PeerStatusChangedEventArgs>? PeerStatusChanged;
    }

    public class SyncPeer
    {
        public string Url { get; set; } = "";
        public ISequencerRpcClient Client { get; set; } = null!;
        public BigInteger BlockNumber { get; set; } = -1;
        public bool IsHealthy { get; set; }
        public DateTime LastSeen { get; set; }
        public DateTime AddedAt { get; set; }
        public TimeSpan LastCheckDuration { get; set; }
        public string? LastError { get; set; }
        public int FailureCount { get; set; }
    }

    public class PeerStatusChangedEventArgs : EventArgs
    {
        public SyncPeer Peer { get; set; } = null!;
        public bool PreviousHealthy { get; set; }
        public bool CurrentHealthy { get; set; }
    }

    public class PeerManagerConfig
    {
        public int HealthCheckIntervalMs { get; set; } = 10000;
        public int HealthCheckTimeoutMs { get; set; } = 5000;
        public int MaxFailuresBeforeRemoval { get; set; } = 10;

        public static PeerManagerConfig Default => new();
    }
}
