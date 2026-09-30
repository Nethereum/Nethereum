namespace Nethereum.AccountAbstraction.Bundler.Reputation
{
    public interface IReputationService
    {
        Task<ReputationEntry?> GetAsync(string address);
        Task<ReputationEntry[]> GetAllAsync();
        Task UpdateAsync(ReputationEntry entry);
        Task RecordIncludedAsync(string address);
        Task RecordFailedAsync(string address);
        Task RecordDroppedAsync(string address);

        Task RecordSeenAsync(string address, int delta = 1);

        Task ApplyStakedAccountabilityPenaltyAsync(string address);

        Task<bool> IsThrottledAsync(string address);
        Task<bool> IsBannedAsync(string address);
        Task SetBannedAsync(string address, TimeSpan duration);
        Task SetThrottledAsync(string address, TimeSpan duration);
        Task ClearAsync(string address);
        Task ClearAllAsync();
        Task DecayAsync();
    }

    public class ReputationConfig
    {
        public TimeSpan DefaultThrottleDuration { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan DefaultBanDuration { get; set; } = TimeSpan.FromHours(24);

        public int DecayNumerator { get; set; } = 23;
        public int DecayDenominator { get; set; } = 24;

        public int MinInclusionDenominator { get; set; } = 10;
        public int ThrottlingSlack { get; set; } = 10;
        public int BanSlack { get; set; } = 50;

        public int StakedAccountabilityPenalty { get; set; } = 10000;
    }

    public class InMemoryReputationService : IReputationService
    {
        private readonly Dictionary<string, ReputationEntry> _entries = new();
        private readonly ReputationConfig _config;
        private readonly object _lock = new();

        public InMemoryReputationService(ReputationConfig? config = null)
        {
            _config = config ?? new ReputationConfig();
        }

        public Task<ReputationEntry?> GetAsync(string address)
        {
            lock (_lock)
            {
                var key = address?.ToLowerInvariant() ?? "";
                return Task.FromResult(_entries.TryGetValue(key, out var entry) ? Copy(entry) : null);
            }
        }

        public Task<ReputationEntry[]> GetAllAsync()
        {
            lock (_lock)
            {
                return Task.FromResult(_entries.Values.Select(Copy).ToArray());
            }
        }

        public Task UpdateAsync(ReputationEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            lock (_lock)
            {
                var key = entry.Address?.ToLowerInvariant() ?? "";
                entry.Address = key;
                entry.LastUpdated = DateTimeOffset.UtcNow;
                _entries[key] = entry;
            }
            return Task.CompletedTask;
        }

        public Task RecordIncludedAsync(string address)
        {
            MutateEntry(address, entry => entry.OpsIncluded++);
            return Task.CompletedTask;
        }

        public Task RecordFailedAsync(string address)
        {
            MutateEntry(address, entry => entry.OpsFailed++);
            return Task.CompletedTask;
        }

        public Task RecordDroppedAsync(string address)
        {
            MutateEntry(address, entry => entry.OpsDropped++);
            return Task.CompletedTask;
        }

        public Task RecordSeenAsync(string address, int delta = 1)
        {
            if (string.IsNullOrEmpty(address)) return Task.CompletedTask;

            MutateEntry(address, entry => entry.OpsSeen = Math.Max(0, entry.OpsSeen + delta));
            return Task.CompletedTask;
        }

        public Task ApplyStakedAccountabilityPenaltyAsync(string address)
        {
            if (string.IsNullOrEmpty(address)) return Task.CompletedTask;

            MutateEntry(address, entry =>
            {
                entry.OpsSeen += _config.StakedAccountabilityPenalty;
                entry.OpsIncluded = 0;
            });
            return Task.CompletedTask;
        }

        public Task<bool> IsThrottledAsync(string address)
        {
            lock (_lock)
            {
                var key = address?.ToLowerInvariant() ?? "";
                if (!_entries.TryGetValue(key, out var entry) || entry.Status != ReputationStatus.Throttled)
                {
                    return Task.FromResult(false);
                }

                if (entry.ThrottledUntil.HasValue && entry.ThrottledUntil.Value <= DateTimeOffset.UtcNow)
                {
                    entry.Status = ReputationStatus.Ok;
                    entry.ThrottledUntil = null;
                    entry.LastUpdated = DateTimeOffset.UtcNow;
                    return Task.FromResult(false);
                }

                return Task.FromResult(true);
            }
        }

        public Task<bool> IsBannedAsync(string address)
        {
            lock (_lock)
            {
                var key = address?.ToLowerInvariant() ?? "";
                if (!_entries.TryGetValue(key, out var entry) || entry.Status != ReputationStatus.Banned)
                {
                    return Task.FromResult(false);
                }

                if (entry.BannedUntil.HasValue && entry.BannedUntil.Value <= DateTimeOffset.UtcNow)
                {
                    entry.Status = ReputationStatus.Ok;
                    entry.BannedUntil = null;
                    entry.LastUpdated = DateTimeOffset.UtcNow;
                    return Task.FromResult(false);
                }

                return Task.FromResult(true);
            }
        }

        public Task SetBannedAsync(string address, TimeSpan duration)
        {
            lock (_lock)
            {
                var entry = GetOrCreateUnlocked(address);
                entry.Status = ReputationStatus.Banned;
                entry.BannedUntil = DateTimeOffset.UtcNow.Add(duration);
                PersistUnlocked(entry);
            }
            return Task.CompletedTask;
        }

        public Task SetThrottledAsync(string address, TimeSpan duration)
        {
            lock (_lock)
            {
                var entry = GetOrCreateUnlocked(address);
                entry.Status = ReputationStatus.Throttled;
                entry.ThrottledUntil = DateTimeOffset.UtcNow.Add(duration);
                PersistUnlocked(entry);
            }
            return Task.CompletedTask;
        }

        public Task ClearAsync(string address)
        {
            lock (_lock)
            {
                var key = address?.ToLowerInvariant() ?? "";
                _entries.Remove(key);
            }
            return Task.CompletedTask;
        }

        public Task ClearAllAsync()
        {
            lock (_lock)
            {
                _entries.Clear();
            }
            return Task.CompletedTask;
        }

        public Task DecayAsync()
        {
            lock (_lock)
            {
                foreach (var key in _entries.Keys.ToList())
                {
                    var entry = _entries[key];
                    if (ReputationDecayCalculator.Decay(entry, _config))
                    {
                        _entries.Remove(key);
                    }
                    else
                    {
                        ApplyReputationStatus(entry);
                        entry.LastUpdated = DateTimeOffset.UtcNow;
                    }
                }
            }
            return Task.CompletedTask;
        }

        private void MutateEntry(string address, Action<ReputationEntry> mutate)
        {
            lock (_lock)
            {
                var entry = GetOrCreateUnlocked(address);
                mutate(entry);
                ApplyReputationStatus(entry);
                PersistUnlocked(entry);
            }
        }

        private ReputationEntry GetOrCreateUnlocked(string address)
        {
            var key = address?.ToLowerInvariant() ?? "";
            if (_entries.TryGetValue(key, out var entry))
            {
                return entry;
            }

            return new ReputationEntry
            {
                Address = key,
                LastUpdated = DateTimeOffset.UtcNow
            };
        }

        private void PersistUnlocked(ReputationEntry entry)
        {
            var key = entry.Address?.ToLowerInvariant() ?? "";
            entry.Address = key;
            entry.LastUpdated = DateTimeOffset.UtcNow;
            _entries[key] = entry;
        }

        private void ApplyReputationStatus(ReputationEntry entry)
        {
            entry.Status = ReputationStatusCalculator.Compute(entry.OpsSeen, entry.OpsIncluded, _config);

            switch (entry.Status)
            {
                case ReputationStatus.Banned:
                    entry.BannedUntil = DateTimeOffset.UtcNow.Add(_config.DefaultBanDuration);
                    break;
                case ReputationStatus.Throttled:
                    entry.ThrottledUntil = DateTimeOffset.UtcNow.Add(_config.DefaultThrottleDuration);
                    break;
                default:
                    entry.BannedUntil = null;
                    entry.ThrottledUntil = null;
                    break;
            }
        }

        private static ReputationEntry Copy(ReputationEntry entry)
        {
            return new ReputationEntry
            {
                Address = entry.Address,
                OpsSeen = entry.OpsSeen,
                OpsIncluded = entry.OpsIncluded,
                OpsFailed = entry.OpsFailed,
                OpsDropped = entry.OpsDropped,
                Status = entry.Status,
                LastUpdated = entry.LastUpdated,
                BannedUntil = entry.BannedUntil,
                ThrottledUntil = entry.ThrottledUntil
            };
        }
    }
}
