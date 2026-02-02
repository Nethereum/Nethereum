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
        public int ThrottleThreshold { get; set; } = 10;
        public int BanThreshold { get; set; } = 50;
        public double ThrottleFailRate { get; set; } = 0.3;
        public TimeSpan DefaultThrottleDuration { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan DefaultBanDuration { get; set; } = TimeSpan.FromHours(24);
        public double DecayFactor { get; set; } = 0.9;
        public TimeSpan DecayInterval { get; set; } = TimeSpan.FromHours(1);
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
                return Task.FromResult(_entries.TryGetValue(key, out var entry) ? entry : null);
            }
        }

        public Task<ReputationEntry[]> GetAllAsync()
        {
            lock (_lock)
            {
                return Task.FromResult(_entries.Values.ToArray());
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

        public async Task RecordIncludedAsync(string address)
        {
            var entry = await GetOrCreateAsync(address);
            entry.OpsIncluded++;
            await UpdateStatusAsync(entry);
            await UpdateAsync(entry);
        }

        public async Task RecordFailedAsync(string address)
        {
            var entry = await GetOrCreateAsync(address);
            entry.OpsFailed++;
            await UpdateStatusAsync(entry);
            await UpdateAsync(entry);
        }

        public async Task RecordDroppedAsync(string address)
        {
            var entry = await GetOrCreateAsync(address);
            entry.OpsDropped++;
            await UpdateStatusAsync(entry);
            await UpdateAsync(entry);
        }

        public async Task<bool> IsThrottledAsync(string address)
        {
            var entry = await GetAsync(address);
            if (entry == null) return false;

            if (entry.Status == ReputationStatus.Throttled)
            {
                if (entry.ThrottledUntil.HasValue && entry.ThrottledUntil.Value <= DateTimeOffset.UtcNow)
                {
                    entry.Status = ReputationStatus.Ok;
                    entry.ThrottledUntil = null;
                    await UpdateAsync(entry);
                    return false;
                }
                return true;
            }

            return false;
        }

        public async Task<bool> IsBannedAsync(string address)
        {
            var entry = await GetAsync(address);
            if (entry == null) return false;

            if (entry.Status == ReputationStatus.Banned)
            {
                if (entry.BannedUntil.HasValue && entry.BannedUntil.Value <= DateTimeOffset.UtcNow)
                {
                    entry.Status = ReputationStatus.Ok;
                    entry.BannedUntil = null;
                    await UpdateAsync(entry);
                    return false;
                }
                return true;
            }

            return false;
        }

        public async Task SetBannedAsync(string address, TimeSpan duration)
        {
            var entry = await GetOrCreateAsync(address);
            entry.Status = ReputationStatus.Banned;
            entry.BannedUntil = DateTimeOffset.UtcNow.Add(duration);
            await UpdateAsync(entry);
        }

        public async Task SetThrottledAsync(string address, TimeSpan duration)
        {
            var entry = await GetOrCreateAsync(address);
            entry.Status = ReputationStatus.Throttled;
            entry.ThrottledUntil = DateTimeOffset.UtcNow.Add(duration);
            await UpdateAsync(entry);
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

        public async Task DecayAsync()
        {
            var entries = await GetAllAsync();
            foreach (var entry in entries)
            {
                entry.OpsIncluded = (int)(entry.OpsIncluded * _config.DecayFactor);
                entry.OpsFailed = (int)(entry.OpsFailed * _config.DecayFactor);
                entry.OpsDropped = (int)(entry.OpsDropped * _config.DecayFactor);
                await UpdateAsync(entry);
            }
        }

        private async Task<ReputationEntry> GetOrCreateAsync(string address)
        {
            var entry = await GetAsync(address);
            if (entry == null)
            {
                entry = new ReputationEntry
                {
                    Address = address?.ToLowerInvariant() ?? "",
                    LastUpdated = DateTimeOffset.UtcNow
                };
            }
            return entry;
        }

        private Task UpdateStatusAsync(ReputationEntry entry)
        {
            if (entry.Status == ReputationStatus.Banned) return Task.CompletedTask;

            var totalOps = entry.OpsIncluded + entry.OpsFailed;
            double failRate = totalOps > 0 ? (double)entry.OpsFailed / totalOps : 0;

            if (entry.OpsFailed >= _config.BanThreshold)
            {
                entry.Status = ReputationStatus.Banned;
                entry.BannedUntil = DateTimeOffset.UtcNow.Add(_config.DefaultBanDuration);
            }
            else if (entry.OpsFailed >= _config.ThrottleThreshold || failRate >= _config.ThrottleFailRate)
            {
                if (entry.Status != ReputationStatus.Throttled)
                {
                    entry.Status = ReputationStatus.Throttled;
                    entry.ThrottledUntil = DateTimeOffset.UtcNow.Add(_config.DefaultThrottleDuration);
                }
            }

            return Task.CompletedTask;
        }
    }
}
