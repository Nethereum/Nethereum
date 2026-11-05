using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using Nethereum.AccountAbstraction.Bundler.Reputation;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Serialization;

namespace Nethereum.AccountAbstraction.Bundler.RocksDB.Stores
{
    public interface IReputationStore : IReputationService
    {
    }

    public class RocksDbReputationStore : IReputationStore
    {
        private readonly BundlerRocksDbManager _manager;
        private readonly ReputationConfig _config;
        private readonly ConcurrentDictionary<string, ReputationEntry> _cache = new();
        private readonly object _lock = new();
        private readonly ILogger? _logger;

        public RocksDbReputationStore(BundlerRocksDbManager manager, ReputationConfig? config = null)
            : this(manager, config, null)
        {
        }

        public RocksDbReputationStore(BundlerRocksDbManager manager, ReputationConfig? config, ILogger? logger)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _config = config ?? new ReputationConfig();
            _logger = logger;
            LoadCacheFromDb();
        }

        public Task<ReputationEntry?> GetAsync(string address)
        {
            lock (_lock)
            {
                var entry = GetExistingUnlocked(address);
                return Task.FromResult(entry != null ? Copy(entry) : null);
            }
        }

        public Task<ReputationEntry[]> GetAllAsync()
        {
            return Task.FromResult(ReadAllFromDb().ToArray());
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
                var entry = GetExistingUnlocked(address);
                if (entry == null || entry.Status != ReputationStatus.Throttled)
                {
                    return Task.FromResult(false);
                }

                if (entry.ThrottledUntil.HasValue && entry.ThrottledUntil.Value <= DateTimeOffset.UtcNow)
                {
                    entry.Status = ReputationStatus.Ok;
                    entry.ThrottledUntil = null;
                    PersistUnlocked(entry);
                    return Task.FromResult(false);
                }

                return Task.FromResult(true);
            }
        }

        public Task<bool> IsBannedAsync(string address)
        {
            lock (_lock)
            {
                var entry = GetExistingUnlocked(address);
                if (entry == null || entry.Status != ReputationStatus.Banned)
                {
                    return Task.FromResult(false);
                }

                if (entry.BannedUntil.HasValue && entry.BannedUntil.Value <= DateTimeOffset.UtcNow)
                {
                    entry.Status = ReputationStatus.Ok;
                    entry.BannedUntil = null;
                    PersistUnlocked(entry);
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

        /// <summary>
        /// ERC-4337 hourly reputation decay (the reference bundler's
        /// ReputationManager.hourlyCron), identical to InMemoryReputationService.DecayAsync:
        /// every entity's opsSeen/opsIncluded are decayed by 23/24 via the shared
        /// ReputationDecayCalculator, its status is recomputed (so a ban/throttle whose
        /// counters have fallen back below threshold clears), and an entity that has
        /// decayed to all-zero is dropped from both the cache and RocksDB. The whole sweep
        /// runs under _lock as one atomic snapshot - the same discipline as MutateEntry -
        /// so a concurrent update cannot interleave into a half-decayed entry. All work is
        /// synchronous (in-process RocksDB Get/Put/Delete); no await is taken while locked.
        /// </summary>
        public Task DecayAsync()
        {
            lock (_lock)
            {
                foreach (var entry in ReadAllFromDb())
                {
                    if (ReputationDecayCalculator.Decay(entry, _config))
                    {
                        RemoveUnlocked(entry.Address);
                    }
                    else
                    {
                        ApplyStatus(entry);
                        PersistUnlocked(entry);
                    }
                }
            }
            return Task.CompletedTask;
        }

        private List<ReputationEntry> ReadAllFromDb()
        {
            var result = new List<ReputationEntry>();
            var corrupt = new List<(byte[] Key, string Reason)>();

            using (var iterator = _manager.CreateIterator(BundlerRocksDbManager.CF_REPUTATION))
            {
                iterator.SeekToFirst();

                while (iterator.Valid())
                {
                    var entry = TryDecode(iterator.Value(), out var reason);
                    if (entry != null)
                    {
                        result.Add(entry);
                    }
                    else
                    {
                        corrupt.Add((iterator.Key(), reason));
                    }
                    iterator.Next();
                }
            }

            foreach (var (key, reason) in corrupt)
            {
                Quarantine(key, reason);
            }

            return result;
        }

        private ReputationEntry? TryDecode(byte[] data, out string reason)
        {
            reason = string.Empty;

            ReputationEntry? entry;
            try
            {
                entry = ReputationSerializer.Deserialize(data);
            }
            catch (Exception ex)
            {
                reason = ex.Message;
                return null;
            }

            if (entry == null || string.IsNullOrEmpty(entry.Address))
            {
                reason = "row holds no reputation entry";
                return null;
            }

            if (entry.OpsSeen < 0 || entry.OpsIncluded < 0 || entry.OpsFailed < 0 || entry.OpsDropped < 0 ||
                !Enum.IsDefined(typeof(ReputationStatus), entry.Status))
            {
                reason = "row holds out-of-range counters or status";
                return null;
            }

            return entry;
        }

        private void Quarantine(byte[] key, string reason)
        {
            _logger?.LogWarning(
                "Dropping undecodable bundler reputation row {Key}: {Reason}",
                Encoding.UTF8.GetString(key), reason);
            _manager.Delete(BundlerRocksDbManager.CF_REPUTATION, key);
        }

        private void RemoveUnlocked(string? address)
        {
            var key = address?.ToLowerInvariant() ?? "";
            _cache.TryRemove(key, out _);
            _manager.Delete(BundlerRocksDbManager.CF_REPUTATION, ReputationSerializer.AddressToKey(key));
        }

        private void LoadCacheFromDb()
        {
            foreach (var entry in ReadAllFromDb())
            {
                _cache[entry.Address!.ToLowerInvariant()] = entry;
            }
        }

        private void MutateEntry(string address, Action<ReputationEntry> mutate)
        {
            lock (_lock)
            {
                var entry = GetOrCreateUnlocked(address);
                mutate(entry);
                ApplyStatus(entry);
                PersistUnlocked(entry);
            }
        }

        private ReputationEntry? GetExistingUnlocked(string address)
        {
            var key = address?.ToLowerInvariant() ?? "";
            if (_cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var rowKey = ReputationSerializer.AddressToKey(key);
            var data = _manager.Get(BundlerRocksDbManager.CF_REPUTATION, rowKey);
            if (data != null)
            {
                var entry = TryDecode(data, out var reason);
                if (entry != null)
                {
                    _cache[key] = entry;
                    return entry;
                }

                Quarantine(rowKey, reason);
            }

            return null;
        }

        private ReputationEntry GetOrCreateUnlocked(string address)
        {
            return GetExistingUnlocked(address) ?? new ReputationEntry
            {
                Address = address?.ToLowerInvariant() ?? "",
                LastUpdated = DateTimeOffset.UtcNow
            };
        }

        private void PersistUnlocked(ReputationEntry entry)
        {
            var key = entry.Address?.ToLowerInvariant() ?? "";
            entry.Address = key;
            entry.LastUpdated = DateTimeOffset.UtcNow;

            _cache[key] = entry;
            _manager.Put(
                BundlerRocksDbManager.CF_REPUTATION,
                ReputationSerializer.AddressToKey(key),
                ReputationSerializer.Serialize(entry));
        }

        private void ApplyStatus(ReputationEntry entry)
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
