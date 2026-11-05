using System.Collections.Concurrent;
using System.Numerics;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Bundler.Mempool
{
    public class InMemoryUserOpMempool : IUserOpMempool, IMempoolEvictionLog, IMempoolInFlight, IMempoolPendingPool
    {
        private readonly ConcurrentDictionary<string, MempoolEntry> _entries = new();
        private readonly ConcurrentDictionary<string, HashSet<string>> _bySender = new();
        private readonly ConcurrentDictionary<string, HashSet<string>> _byTransaction = new();
        private static readonly TimeSpan FinishedEntryRetention = TimeSpan.FromMinutes(5);

        private readonly int _maxSize;
        private readonly TimeSpan _entryTtl;
        private readonly object _lock = new();
        private readonly List<MempoolEntry> _evicted = new();

        public InMemoryUserOpMempool(int maxSize = 1000, TimeSpan? entryTtl = null)
        {
            _maxSize = maxSize;
            _entryTtl = entryTtl ?? TimeSpan.FromMinutes(30);
        }

        public Task<MempoolAddOutcome> AddAsync(MempoolEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (string.IsNullOrEmpty(entry.UserOpHash)) throw new ArgumentException("UserOpHash required");

            var key = MempoolHashKey.Normalize(entry.UserOpHash);

            lock (_lock)
            {
                var sender = entry.UserOperation.Sender?.ToLowerInvariant() ?? "";
                var decision = MempoolAdmission.Decide(
                    _entries.ContainsKey(key),
                    FindBySenderAndNonce(sender, entry.UserOperation.Nonce, entry.EntryPoint),
                    _entries.Values.Count(e => e.State == MempoolEntryState.Pending),
                    _maxSize,
                    () => _entries.Values,
                    entry);

                if (!decision.Accepted) return Task.FromResult(decision.Outcome);

                if (decision.Replaced != null) RemoveInternal(decision.Replaced.UserOpHash);
                if (decision.Evicted != null)
                {
                    RemoveInternal(decision.Evicted.UserOpHash);
                    _evicted.Add(decision.Evicted);
                    if (_evicted.Count > _maxSize) _evicted.RemoveAt(0);
                }

                InsertInternal(entry, key, sender);
                return Task.FromResult(decision.Outcome);
            }
        }

        private void InsertInternal(MempoolEntry entry, string key, string sender)
        {
            entry.SubmittedAt = DateTimeOffset.UtcNow;
            entry.State = MempoolEntryState.Pending;
            _entries[key] = entry;

            _bySender.AddOrUpdate(
                sender,
                _ => new HashSet<string> { key },
                (_, set) => { set.Add(key); return set; });
        }

        private MempoolEntry? FindBySenderAndNonce(string sender, BigInteger nonce, string entryPoint)
        {
            if (!_bySender.TryGetValue(sender, out var hashes)) return null;

            foreach (var hash in hashes)
            {
                if (_entries.TryGetValue(hash, out var existing) &&
                    existing.UserOperation.Nonce == nonce &&
                    existing.EntryPoint.Equals(entryPoint, StringComparison.OrdinalIgnoreCase) &&
                    MempoolStateTransitions.IsOccupying(existing))
                {
                    return existing;
                }
            }

            return null;
        }

        public Task<MempoolEntry?> GetAsync(string userOpHash)
        {
            _entries.TryGetValue(MempoolHashKey.Normalize(userOpHash), out var entry);
            return Task.FromResult(entry);
        }

        public Task<MempoolEntry[]> GetPendingAsync(int maxCount, BigInteger? maxGas = null)
        {
            var currentTimestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var eligible = _entries.Values
                .Where(e => e.State == MempoolEntryState.Pending)
                .Where(e => Validation.ValidationValidityWindow.IsValidAtTime(e.ValidAfter, e.ValidUntil, currentTimestamp))
                .ToArray();

            return Task.FromResult(MempoolPendingChains.SelectBundleCandidates(eligible, maxCount, maxGas));
        }

        public Task<MempoolEntry[]> GetAllPendingAsync()
        {
            var currentTimestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var pending = _entries.Values
                .Where(e => e.State == MempoolEntryState.Pending)
                .Where(e => Validation.ValidationValidityWindow.IsValidAtTime(e.ValidAfter, e.ValidUntil, currentTimestamp))
                .OrderBy(e => e.SubmittedAt)
                .ToArray();

            return Task.FromResult(pending);
        }

        public Task<MempoolEntry[]> GetPendingPoolAsync()
        {
            var pool = _entries.Values
                .Where(e => e.State == MempoolEntryState.Pending)
                .OrderBy(e => e.SubmittedAt)
                .ToArray();

            return Task.FromResult(pool);
        }

        public Task<MempoolEntry[]> GetBySenderAsync(string sender)
        {
            var normalizedSender = sender?.ToLowerInvariant() ?? "";

            lock (_lock)
            {
                if (!_bySender.TryGetValue(normalizedSender, out var hashes))
                {
                    return Task.FromResult(Array.Empty<MempoolEntry>());
                }

                var entries = hashes
                    .Select(h => _entries.TryGetValue(MempoolHashKey.Normalize(h), out var e) ? e : null)
                    .Where(e => e != null)
                    .Cast<MempoolEntry>()
                    .ToArray();

                return Task.FromResult(entries);
            }
        }

        public Task<bool> RemoveAsync(string userOpHash)
        {
            lock (_lock)
            {
                return Task.FromResult(RemoveInternal(userOpHash));
            }
        }

        private bool RemoveInternal(string userOpHash)
        {
            userOpHash = MempoolHashKey.Normalize(userOpHash);
            if (!_entries.TryRemove(userOpHash, out var entry))
            {
                return false;
            }

            var sender = entry.UserOperation.Sender?.ToLowerInvariant() ?? "";
            if (_bySender.TryGetValue(sender, out var senderHashes))
            {
                senderHashes.Remove(userOpHash);
                if (senderHashes.Count == 0)
                {
                    _bySender.TryRemove(sender, out _);
                }
            }

            return true;
        }

        public Task MarkSubmittedAsync(string[] userOpHashes, string transactionHash)
        {
            foreach (var hash in userOpHashes)
            {
                if (_entries.TryGetValue(MempoolHashKey.Normalize(hash), out var entry))
                {
                    MempoolStateTransitions.TrySubmit(entry, transactionHash);
                }
            }

            _byTransaction.AddOrUpdate(
                transactionHash,
                _ => new HashSet<string>(userOpHashes),
                (_, set) => { foreach (var h in userOpHashes) set.Add(h); return set; });

            return Task.CompletedTask;
        }

        public Task<string[]> MarkInFlightAsync(string[] userOpHashes)
        {
            var marked = new List<string>();
            lock (_lock)
            {
                foreach (var hash in userOpHashes)
                {
                    if (_entries.TryGetValue(MempoolHashKey.Normalize(hash), out var entry) &&
                        MempoolStateTransitions.TryMarkInFlight(entry))
                    {
                        marked.Add(hash);
                    }
                }
            }

            return Task.FromResult(marked.ToArray());
        }

        public Task ReleaseInFlightAsync(string[] userOpHashes)
        {
            lock (_lock)
            {
                foreach (var hash in userOpHashes)
                {
                    if (_entries.TryGetValue(MempoolHashKey.Normalize(hash), out var entry))
                    {
                        MempoolStateTransitions.TryRelease(entry);
                    }
                }
            }

            return Task.CompletedTask;
        }

        public Task MarkIncludedAsync(string[] userOpHashes, string transactionHash, BigInteger blockNumber, string? blockHash = null)
        {
            foreach (var hash in userOpHashes)
            {
                if (_entries.TryGetValue(MempoolHashKey.Normalize(hash), out var entry))
                {
                    MempoolStateTransitions.TryInclude(entry, transactionHash, blockNumber, blockHash);
                }
            }

            _byTransaction.TryRemove(transactionHash, out _);

            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(string[] userOpHashes, string error)
        {
            foreach (var hash in userOpHashes)
            {
                if (_entries.TryGetValue(MempoolHashKey.Normalize(hash), out var entry))
                {
                    var transactionHash = entry.TransactionHash;
                    if (MempoolStateTransitions.TryFail(entry, error) && !string.IsNullOrEmpty(transactionHash))
                    {
                        _byTransaction.TryRemove(transactionHash, out _);
                    }
                }
            }

            return Task.CompletedTask;
        }

        public Task RevertSubmittedAsync(string transactionHash)
        {
            if (_byTransaction.TryRemove(transactionHash, out var hashes))
            {
                foreach (var hash in hashes)
                {
                    if (_entries.TryGetValue(MempoolHashKey.Normalize(hash), out var entry))
                    {
                        MempoolStateTransitions.TryRevert(entry);
                    }
                }
            }

            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            lock (_lock)
            {
                _entries.Clear();
                _bySender.Clear();
                _byTransaction.Clear();
                _evicted.Clear();
            }
            return Task.CompletedTask;
        }

        public MempoolEntry[] DrainEvicted()
        {
            lock (_lock)
            {
                var drained = _evicted.ToArray();
                _evicted.Clear();
                return drained;
            }
        }

        public Task<int> CountAsync()
        {
            return Task.FromResult(_entries.Count);
        }

        public Task<MempoolStats> GetStatsAsync()
        {
            var entries = _entries.Values.ToList();
            var stats = new MempoolStats
            {
                TotalCount = entries.Count,
                PendingCount = entries.Count(e => e.State == MempoolEntryState.Pending),
                SubmittedCount = entries.Count(e => e.State == MempoolEntryState.Submitted),
                IncludedCount = entries.Count(e => e.State == MempoolEntryState.Included),
                FailedCount = entries.Count(e => e.State == MempoolEntryState.Failed),
                UniqueSenders = entries.Select(e => e.UserOperation.Sender?.ToLowerInvariant()).Distinct().Count(),
                UniquePaymasters = entries.Where(e => !string.IsNullOrEmpty(e.Paymaster)).Select(e => e.Paymaster?.ToLowerInvariant()).Distinct().Count(),
                TotalPrefund = entries.Aggregate(BigInteger.Zero, (sum, e) => sum + e.Prefund)
            };

            return Task.FromResult(stats);
        }

        public async Task<int> PruneAsync()
        {
            var now = DateTimeOffset.UtcNow;
            var toRemove = _entries
                .Where(kvp => MempoolPruneRule.IsExpired(kvp.Value, now, _entryTtl, FinishedEntryRetention))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var hash in toRemove)
            {
                await RemoveAsync(hash);
            }

            return toRemove.Count;
        }

    }
}
