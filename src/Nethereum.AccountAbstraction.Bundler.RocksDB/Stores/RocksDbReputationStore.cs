using System.Collections.Concurrent;
using Nethereum.AccountAbstraction.Bundler.Reputation;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Serialization;

namespace Nethereum.AccountAbstraction.Bundler.RocksDB.Stores
{
    public interface IReputationStore : IReputationService
    {
        Task DecayAsync(double factor);
    }

    public class RocksDbReputationStore : IReputationStore
    {
        private readonly BundlerRocksDbManager _manager;
        private readonly ReputationConfig _config;
        private readonly ConcurrentDictionary<string, ReputationEntry> _cache = new();
        private readonly object _lock = new();

        public RocksDbReputationStore(BundlerRocksDbManager manager, ReputationConfig? config = null)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _config = config ?? new ReputationConfig();
            LoadCacheFromDb();
        }

        public Task<ReputationEntry?> GetAsync(string address)
        {
            var normalizedAddress = address?.ToLowerInvariant() ?? "";
            if (_cache.TryGetValue(normalizedAddress, out var entry))
            {
                return Task.FromResult<ReputationEntry?>(entry);
            }

            var key = ReputationSerializer.AddressToKey(normalizedAddress);
            var data = _manager.Get(BundlerRocksDbManager.CF_REPUTATION, key);
            if (data != null)
            {
                entry = ReputationSerializer.Deserialize(data);
                if (entry != null)
                {
                    _cache[normalizedAddress] = entry;
                }
                return Task.FromResult(entry);
            }

            return Task.FromResult<ReputationEntry?>(null);
        }

        public Task<ReputationEntry[]> GetAllAsync()
        {
            var result = new List<ReputationEntry>();
            using var iterator = _manager.CreateIterator(BundlerRocksDbManager.CF_REPUTATION);
            iterator.SeekToFirst();

            while (iterator.Valid())
            {
                var entry = ReputationSerializer.Deserialize(iterator.Value());
                if (entry != null)
                {
                    result.Add(entry);
                }
                iterator.Next();
            }

            return Task.FromResult(result.ToArray());
        }

        public Task UpdateAsync(ReputationEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            var normalizedAddress = entry.Address?.ToLowerInvariant() ?? "";
            entry.Address = normalizedAddress;
            entry.LastUpdated = DateTimeOffset.UtcNow;

            lock (_lock)
            {
                _cache[normalizedAddress] = entry;
                var key = ReputationSerializer.AddressToKey(normalizedAddress);
                var data = ReputationSerializer.Serialize(entry);
                _manager.Put(BundlerRocksDbManager.CF_REPUTATION, key, data);
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
            var normalizedAddress = address?.ToLowerInvariant() ?? "";
            lock (_lock)
            {
                _cache.TryRemove(normalizedAddress, out _);
                var key = ReputationSerializer.AddressToKey(normalizedAddress);
                _manager.Delete(BundlerRocksDbManager.CF_REPUTATION, key);
            }
            return Task.CompletedTask;
        }

        public Task ClearAllAsync()
        {
            lock (_lock)
            {
                _cache.Clear();

                var batch = _manager.CreateWriteBatch();
                try
                {
                    using var iterator = _manager.CreateIterator(BundlerRocksDbManager.CF_REPUTATION);
                    iterator.SeekToFirst();
                    while (iterator.Valid())
                    {
                        batch.Delete(iterator.Key(), _manager.GetColumnFamily(BundlerRocksDbManager.CF_REPUTATION));
                        iterator.Next();
                    }
                    _manager.Write(batch);
                }
                finally
                {
                    batch.Dispose();
                }
            }
            return Task.CompletedTask;
        }

        public async Task DecayAsync(double factor)
        {
            var entries = await GetAllAsync();
            foreach (var entry in entries)
            {
                entry.OpsIncluded = (int)(entry.OpsIncluded * factor);
                entry.OpsFailed = (int)(entry.OpsFailed * factor);
                entry.OpsDropped = (int)(entry.OpsDropped * factor);
                await UpdateAsync(entry);
            }
        }

        public Task DecayAsync()
        {
            return DecayAsync(_config.DecayFactor);
        }

        private void LoadCacheFromDb()
        {
            using var iterator = _manager.CreateIterator(BundlerRocksDbManager.CF_REPUTATION);
            iterator.SeekToFirst();

            while (iterator.Valid())
            {
                var entry = ReputationSerializer.Deserialize(iterator.Value());
                if (entry != null && !string.IsNullOrEmpty(entry.Address))
                {
                    _cache[entry.Address.ToLowerInvariant()] = entry;
                }
                iterator.Next();
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
