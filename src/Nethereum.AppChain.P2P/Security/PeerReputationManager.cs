using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Nethereum.AppChain.P2P.Security
{
    public class PeerReputationManager : IDisposable
    {
        private readonly ReputationConfig _config;
        private readonly ILogger<PeerReputationManager>? _logger;
        private readonly ConcurrentDictionary<string, PeerReputation> _reputations = new();
        private CancellationTokenSource? _cts;
        private Task? _decayTask;

        public event EventHandler<PeerBannedEventArgs>? PeerBanned;
        public event EventHandler<PeerUnbannedEventArgs>? PeerUnbanned;

        public PeerReputationManager(ReputationConfig? config = null, ILogger<PeerReputationManager>? logger = null)
        {
            _config = config ?? ReputationConfig.Default;
            _logger = logger;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _decayTask = RunDecayLoopAsync(_cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        public void RecordPositive(string peerId, ReputationEvent eventType)
        {
            var reputation = GetOrCreateReputation(peerId);
            var score = _config.PositiveScores.GetValueOrDefault(eventType, 1);

            reputation.AddScore(score);
            reputation.RecordEvent(eventType, score);

            _logger?.LogDebug("Positive event for peer {PeerId}: {Event} (+{Score})", peerId, eventType, score);
        }

        public void RecordNegative(string peerId, ReputationEvent eventType)
        {
            var reputation = GetOrCreateReputation(peerId);
            var penalty = _config.NegativeScores.GetValueOrDefault(eventType, -10);

            reputation.AddScore(penalty);
            reputation.RecordEvent(eventType, penalty);

            _logger?.LogDebug("Negative event for peer {PeerId}: {Event} ({Score})", peerId, eventType, penalty);

            if (reputation.Score < _config.BanThreshold && !reputation.IsBanned)
            {
                BanPeer(peerId, reputation);
            }
        }

        public int GetScore(string peerId)
        {
            return _reputations.TryGetValue(peerId, out var reputation) ? reputation.Score : _config.InitialScore;
        }

        public bool IsBanned(string peerId)
        {
            if (_reputations.TryGetValue(peerId, out var reputation))
            {
                if (reputation.IsBanned && DateTimeOffset.UtcNow >= reputation.BannedUntil)
                {
                    UnbanPeer(peerId, reputation);
                }
                return reputation.IsBanned;
            }
            return false;
        }

        public PeerReputation? GetReputation(string peerId)
        {
            return _reputations.TryGetValue(peerId, out var reputation) ? reputation : null;
        }

        public IEnumerable<string> GetBannedPeers()
        {
            return _reputations
                .Where(kv => kv.Value.IsBanned && DateTimeOffset.UtcNow < kv.Value.BannedUntil)
                .Select(kv => kv.Key);
        }

        public IEnumerable<(string PeerId, int Score)> GetTopPeers(int count)
        {
            return _reputations
                .Where(kv => !kv.Value.IsBanned)
                .OrderByDescending(kv => kv.Value.Score)
                .Take(count)
                .Select(kv => (kv.Key, kv.Value.Score));
        }

        public IEnumerable<(string PeerId, int Score)> GetBottomPeers(int count)
        {
            return _reputations
                .Where(kv => !kv.Value.IsBanned)
                .OrderBy(kv => kv.Value.Score)
                .Take(count)
                .Select(kv => (kv.Key, kv.Value.Score));
        }

        public void Reset(string peerId)
        {
            if (_reputations.TryRemove(peerId, out var reputation) && reputation.IsBanned)
            {
                PeerUnbanned?.Invoke(this, new PeerUnbannedEventArgs(peerId, "Manual reset"));
            }
        }

        private PeerReputation GetOrCreateReputation(string peerId)
        {
            return _reputations.GetOrAdd(peerId, _ => new PeerReputation(peerId, _config.InitialScore));
        }

        private void BanPeer(string peerId, PeerReputation reputation)
        {
            var banDuration = CalculateBanDuration(reputation);
            reputation.Ban(banDuration);

            _logger?.LogWarning("Peer {PeerId} banned for {Duration} due to low reputation ({Score})",
                peerId, banDuration, reputation.Score);

            PeerBanned?.Invoke(this, new PeerBannedEventArgs(peerId, reputation.Score, banDuration));
        }

        private void UnbanPeer(string peerId, PeerReputation reputation)
        {
            reputation.Unban();
            reputation.Score = Math.Max(reputation.Score, _config.InitialScore / 2);

            _logger?.LogInformation("Peer {PeerId} unbanned, score reset to {Score}", peerId, reputation.Score);

            PeerUnbanned?.Invoke(this, new PeerUnbannedEventArgs(peerId, "Ban expired"));
        }

        private TimeSpan CalculateBanDuration(PeerReputation reputation)
        {
            var baseDuration = _config.BaseBanDuration;
            var multiplier = Math.Pow(2, reputation.BanCount);
            var maxMultiplier = 16;

            return TimeSpan.FromTicks((long)(baseDuration.Ticks * Math.Min(multiplier, maxMultiplier)));
        }

        private async Task RunDecayLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_config.DecayIntervalMs, cancellationToken);

                    foreach (var reputation in _reputations.Values)
                    {
                        if (reputation.Score > _config.InitialScore)
                        {
                            reputation.Decay(_config.DecayRate);
                        }
                        else if (reputation.Score < _config.InitialScore)
                        {
                            reputation.Recover(_config.RecoveryRate);
                        }

                        if (reputation.IsBanned && DateTimeOffset.UtcNow >= reputation.BannedUntil)
                        {
                            UnbanPeer(reputation.PeerId, reputation);
                        }
                    }

                    var stale = _reputations
                        .Where(kv => !kv.Value.IsBanned &&
                                     kv.Value.Score == _config.InitialScore &&
                                     DateTimeOffset.UtcNow - kv.Value.LastActivity > TimeSpan.FromHours(24))
                        .Select(kv => kv.Key)
                        .ToList();

                    foreach (var peerId in stale)
                    {
                        _reputations.TryRemove(peerId, out _);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in reputation decay loop");
                }
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }

    public class PeerReputation
    {
        private readonly object _lock = new();
        private readonly List<ReputationHistoryEntry> _history = new();

        public string PeerId { get; }
        public int Score { get; set; }
        public bool IsBanned { get; private set; }
        public DateTimeOffset BannedUntil { get; private set; }
        public int BanCount { get; private set; }
        public DateTimeOffset LastActivity { get; private set; }
        public DateTimeOffset CreatedAt { get; }

        public IReadOnlyList<ReputationHistoryEntry> History => _history.TakeLast(100).ToList();

        public PeerReputation(string peerId, int initialScore)
        {
            PeerId = peerId;
            Score = initialScore;
            CreatedAt = DateTimeOffset.UtcNow;
            LastActivity = DateTimeOffset.UtcNow;
        }

        public void AddScore(int delta)
        {
            lock (_lock)
            {
                Score = Math.Max(-1000, Math.Min(1000, Score + delta));
                LastActivity = DateTimeOffset.UtcNow;
            }
        }

        public void RecordEvent(ReputationEvent eventType, int scoreDelta)
        {
            lock (_lock)
            {
                _history.Add(new ReputationHistoryEntry
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Event = eventType,
                    ScoreDelta = scoreDelta,
                    NewScore = Score
                });

                while (_history.Count > 100)
                {
                    _history.RemoveAt(0);
                }
            }
        }

        public void Ban(TimeSpan duration)
        {
            lock (_lock)
            {
                IsBanned = true;
                BannedUntil = DateTimeOffset.UtcNow.Add(duration);
                BanCount++;
            }
        }

        public void Unban()
        {
            lock (_lock)
            {
                IsBanned = false;
                BannedUntil = DateTimeOffset.MinValue;
            }
        }

        public void Decay(double rate)
        {
            lock (_lock)
            {
                var excess = Score - 100;
                Score = 100 + (int)(excess * (1 - rate));
            }
        }

        public void Recover(double rate)
        {
            lock (_lock)
            {
                var deficit = 100 - Score;
                Score = 100 - (int)(deficit * (1 - rate));
            }
        }
    }

    public class ReputationHistoryEntry
    {
        public DateTimeOffset Timestamp { get; set; }
        public ReputationEvent Event { get; set; }
        public int ScoreDelta { get; set; }
        public int NewScore { get; set; }
    }

    public enum ReputationEvent
    {
        ValidBlock,
        InvalidBlock,
        ValidTransaction,
        InvalidTransaction,
        UsefulData,
        UselessData,
        FastResponse,
        SlowResponse,
        NoResponse,
        ProtocolViolation,
        Spam,
        Disconnect,
        SuccessfulSync
    }

    public class ReputationConfig
    {
        public int InitialScore { get; set; } = 100;
        public int BanThreshold { get; set; } = -100;
        public TimeSpan BaseBanDuration { get; set; } = TimeSpan.FromHours(1);
        public int DecayIntervalMs { get; set; } = 60000;
        public double DecayRate { get; set; } = 0.01;
        public double RecoveryRate { get; set; } = 0.01;

        public Dictionary<ReputationEvent, int> PositiveScores { get; set; } = new();
        public Dictionary<ReputationEvent, int> NegativeScores { get; set; } = new();

        public static ReputationConfig Default
        {
            get
            {
                var config = new ReputationConfig
                {
                    InitialScore = 100,
                    BanThreshold = -100,
                    BaseBanDuration = TimeSpan.FromHours(1),
                    DecayIntervalMs = 60000,
                    DecayRate = 0.01,
                    RecoveryRate = 0.01
                };

                config.PositiveScores[ReputationEvent.ValidBlock] = 10;
                config.PositiveScores[ReputationEvent.ValidTransaction] = 1;
                config.PositiveScores[ReputationEvent.UsefulData] = 5;
                config.PositiveScores[ReputationEvent.FastResponse] = 2;
                config.PositiveScores[ReputationEvent.SuccessfulSync] = 20;

                config.NegativeScores[ReputationEvent.InvalidBlock] = -50;
                config.NegativeScores[ReputationEvent.InvalidTransaction] = -10;
                config.NegativeScores[ReputationEvent.UselessData] = -5;
                config.NegativeScores[ReputationEvent.SlowResponse] = -1;
                config.NegativeScores[ReputationEvent.NoResponse] = -5;
                config.NegativeScores[ReputationEvent.ProtocolViolation] = -30;
                config.NegativeScores[ReputationEvent.Spam] = -20;
                config.NegativeScores[ReputationEvent.Disconnect] = -2;

                return config;
            }
        }
    }

    public class PeerBannedEventArgs : EventArgs
    {
        public string PeerId { get; }
        public int Score { get; }
        public TimeSpan Duration { get; }

        public PeerBannedEventArgs(string peerId, int score, TimeSpan duration)
        {
            PeerId = peerId;
            Score = score;
            Duration = duration;
        }
    }

    public class PeerUnbannedEventArgs : EventArgs
    {
        public string PeerId { get; }
        public string Reason { get; }

        public PeerUnbannedEventArgs(string peerId, string reason)
        {
            PeerId = peerId;
            Reason = reason;
        }
    }
}
