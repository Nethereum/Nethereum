using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Nethereum.AppChain.P2P.Security
{
    internal static class ConcurrentDictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dict, TKey key, TValue defaultValue)
            where TKey : notnull
        {
            return dict.TryGetValue(key, out var value) ? value : defaultValue;
        }
    }

    public class PeerRateLimiter : IDisposable
    {
        private readonly RateLimitConfig _config;
        private readonly ILogger<PeerRateLimiter>? _logger;
        private readonly ConcurrentDictionary<string, PeerRateLimitState> _peerStates = new();
        private readonly ConcurrentDictionary<string, SlidingWindow> _globalWindows = new();
        private CancellationTokenSource? _cts;
        private Task? _cleanupTask;

        public PeerRateLimiter(RateLimitConfig? config = null, ILogger<PeerRateLimiter>? logger = null)
        {
            _config = config ?? RateLimitConfig.Default;
            _logger = logger;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _cleanupTask = RunCleanupLoopAsync(_cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        public RateLimitResult CheckLimit(string peerId, RateLimitCategory category, int cost = 1)
        {
            var state = _peerStates.GetOrAdd(peerId, _ => new PeerRateLimitState(peerId, _config));

            if (!state.TryConsume(category, cost))
            {
                _logger?.LogWarning("Rate limit exceeded for peer {PeerId} in category {Category}",
                    peerId, category);
                return RateLimitResult.Exceeded(category, state.GetRetryAfter(category));
            }

            var globalKey = category.ToString();
            var globalWindow = _globalWindows.GetOrAdd(globalKey,
                _ => new SlidingWindow(_config.GlobalLimits.GetValueOrDefault(category, 10000), 60));

            if (!globalWindow.TryConsume(cost))
            {
                _logger?.LogWarning("Global rate limit exceeded for category {Category}", category);
                return RateLimitResult.GlobalExceeded(category, globalWindow.GetRetryAfter());
            }

            return RateLimitResult.Allowed(state.GetRemaining(category));
        }

        public void ReportViolation(string peerId, ViolationType type)
        {
            if (_peerStates.TryGetValue(peerId, out var state))
            {
                state.RecordViolation(type);
                _logger?.LogWarning("Violation recorded for peer {PeerId}: {Type}", peerId, type);
            }
        }

        public bool IsBanned(string peerId)
        {
            if (_peerStates.TryGetValue(peerId, out var state))
            {
                return state.IsBanned;
            }
            return false;
        }

        public void Reset(string peerId)
        {
            _peerStates.TryRemove(peerId, out _);
        }

        private async Task RunCleanupLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);

                    var expiredPeers = _peerStates
                        .Where(kv => kv.Value.IsExpired)
                        .Select(kv => kv.Key)
                        .ToList();

                    foreach (var peerId in expiredPeers)
                    {
                        _peerStates.TryRemove(peerId, out _);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }

    public class PeerRateLimitState
    {
        private readonly string _peerId;
        private readonly RateLimitConfig _config;
        private readonly ConcurrentDictionary<RateLimitCategory, SlidingWindow> _windows = new();
        private int _violationCount;
        private DateTimeOffset _bannedUntil;
        private DateTimeOffset _lastActivity;

        public bool IsBanned => DateTimeOffset.UtcNow < _bannedUntil;
        public bool IsExpired => DateTimeOffset.UtcNow - _lastActivity > TimeSpan.FromHours(1);

        public PeerRateLimitState(string peerId, RateLimitConfig config)
        {
            _peerId = peerId;
            _config = config;
            _lastActivity = DateTimeOffset.UtcNow;
        }

        public bool TryConsume(RateLimitCategory category, int cost)
        {
            if (IsBanned)
                return false;

            _lastActivity = DateTimeOffset.UtcNow;

            var limit = _config.PerPeerLimits.GetValueOrDefault(category, 100);
            var windowSeconds = _config.WindowSeconds.GetValueOrDefault(category, 60);

            var window = _windows.GetOrAdd(category, _ => new SlidingWindow(limit, windowSeconds));
            return window.TryConsume(cost);
        }

        public int GetRemaining(RateLimitCategory category)
        {
            if (_windows.TryGetValue(category, out var window))
            {
                return window.Remaining;
            }
            return _config.PerPeerLimits.GetValueOrDefault(category, 100);
        }

        public TimeSpan GetRetryAfter(RateLimitCategory category)
        {
            if (_windows.TryGetValue(category, out var window))
            {
                return window.GetRetryAfter();
            }
            return TimeSpan.Zero;
        }

        public void RecordViolation(ViolationType type)
        {
            var severity = type switch
            {
                ViolationType.InvalidMessage => 1,
                ViolationType.InvalidSignature => 2,
                ViolationType.InvalidBlock => 3,
                ViolationType.Spam => 2,
                ViolationType.ProtocolViolation => 3,
                _ => 1
            };

            var newCount = Interlocked.Add(ref _violationCount, severity);

            if (newCount >= _config.MaxViolationsBeforeBan)
            {
                _bannedUntil = DateTimeOffset.UtcNow.Add(_config.BanDuration);
            }
        }
    }

    public class SlidingWindow
    {
        private readonly int _limit;
        private readonly int _windowSeconds;
        private readonly ConcurrentQueue<(DateTimeOffset Timestamp, int Cost)> _requests = new();
        private int _currentCount;
        private readonly object _lock = new();

        public int Remaining => Math.Max(0, _limit - _currentCount);

        public SlidingWindow(int limit, int windowSeconds)
        {
            _limit = limit;
            _windowSeconds = windowSeconds;
        }

        public bool TryConsume(int cost)
        {
            lock (_lock)
            {
                CleanupOldRequests();

                if (_currentCount + cost > _limit)
                    return false;

                _requests.Enqueue((DateTimeOffset.UtcNow, cost));
                _currentCount += cost;
                return true;
            }
        }

        public TimeSpan GetRetryAfter()
        {
            lock (_lock)
            {
                if (_requests.TryPeek(out var oldest))
                {
                    var windowEnd = oldest.Timestamp.AddSeconds(_windowSeconds);
                    var retryAfter = windowEnd - DateTimeOffset.UtcNow;
                    return retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.Zero;
                }
                return TimeSpan.Zero;
            }
        }

        private void CleanupOldRequests()
        {
            var cutoff = DateTimeOffset.UtcNow.AddSeconds(-_windowSeconds);
            while (_requests.TryPeek(out var oldest) && oldest.Timestamp < cutoff)
            {
                if (_requests.TryDequeue(out var removed))
                {
                    _currentCount -= removed.Cost;
                }
            }
        }
    }

    public class RateLimitConfig
    {
        public ConcurrentDictionary<RateLimitCategory, int> PerPeerLimits { get; set; } = new();
        public ConcurrentDictionary<RateLimitCategory, int> GlobalLimits { get; set; } = new();
        public ConcurrentDictionary<RateLimitCategory, int> WindowSeconds { get; set; } = new();
        public int MaxViolationsBeforeBan { get; set; } = 10;
        public TimeSpan BanDuration { get; set; } = TimeSpan.FromHours(1);

        public static RateLimitConfig Default
        {
            get
            {
                var config = new RateLimitConfig
                {
                    MaxViolationsBeforeBan = 10,
                    BanDuration = TimeSpan.FromHours(1)
                };

                config.PerPeerLimits[RateLimitCategory.Messages] = 1000;
                config.PerPeerLimits[RateLimitCategory.Blocks] = 100;
                config.PerPeerLimits[RateLimitCategory.Transactions] = 500;
                config.PerPeerLimits[RateLimitCategory.Requests] = 200;

                config.GlobalLimits[RateLimitCategory.Messages] = 50000;
                config.GlobalLimits[RateLimitCategory.Blocks] = 5000;
                config.GlobalLimits[RateLimitCategory.Transactions] = 25000;
                config.GlobalLimits[RateLimitCategory.Requests] = 10000;

                config.WindowSeconds[RateLimitCategory.Messages] = 60;
                config.WindowSeconds[RateLimitCategory.Blocks] = 60;
                config.WindowSeconds[RateLimitCategory.Transactions] = 60;
                config.WindowSeconds[RateLimitCategory.Requests] = 60;

                return config;
            }
        }
    }

    public enum RateLimitCategory
    {
        Messages,
        Blocks,
        Transactions,
        Requests
    }

    public enum ViolationType
    {
        InvalidMessage,
        InvalidSignature,
        InvalidBlock,
        Spam,
        ProtocolViolation
    }

    public class RateLimitResult
    {
        public bool IsAllowed { get; }
        public int Remaining { get; }
        public RateLimitCategory? ExceededCategory { get; }
        public TimeSpan RetryAfter { get; }
        public bool IsGlobalLimit { get; }

        private RateLimitResult(bool allowed, int remaining, RateLimitCategory? category, TimeSpan retryAfter, bool isGlobal)
        {
            IsAllowed = allowed;
            Remaining = remaining;
            ExceededCategory = category;
            RetryAfter = retryAfter;
            IsGlobalLimit = isGlobal;
        }

        public static RateLimitResult Allowed(int remaining) => new(true, remaining, null, TimeSpan.Zero, false);
        public static RateLimitResult Exceeded(RateLimitCategory category, TimeSpan retryAfter) =>
            new(false, 0, category, retryAfter, false);
        public static RateLimitResult GlobalExceeded(RateLimitCategory category, TimeSpan retryAfter) =>
            new(false, 0, category, retryAfter, true);
    }
}
