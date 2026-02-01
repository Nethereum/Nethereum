using System.Collections.Concurrent;
using System.Numerics;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Bundler.Mempool
{
    /// <summary>
    /// In-memory implementation of the UserOperation mempool.
    /// Suitable for development, testing, and single-instance bundlers.
    /// </summary>
    public class InMemoryUserOpMempool : IUserOpMempool
    {
        private readonly ConcurrentDictionary<string, MempoolEntry> _entries = new();
        private readonly ConcurrentDictionary<string, HashSet<string>> _bySender = new();
        private readonly ConcurrentDictionary<string, HashSet<string>> _byTransaction = new();
        private readonly int _maxSize;
        private readonly TimeSpan _entryTtl;
        private readonly object _lock = new();

        public InMemoryUserOpMempool(int maxSize = 1000, TimeSpan? entryTtl = null)
        {
            _maxSize = maxSize;
            _entryTtl = entryTtl ?? TimeSpan.FromMinutes(30);
        }

        public Task<bool> AddAsync(MempoolEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (string.IsNullOrEmpty(entry.UserOpHash)) throw new ArgumentException("UserOpHash required");

            lock (_lock)
            {
                if (_entries.Count >= _maxSize)
                {
                    return Task.FromResult(false);
                }

                if (_entries.ContainsKey(entry.UserOpHash))
                {
                    return Task.FromResult(false);
                }

                entry.SubmittedAt = DateTimeOffset.UtcNow;
                entry.State = MempoolEntryState.Pending;

                if (!_entries.TryAdd(entry.UserOpHash, entry))
                {
                    return Task.FromResult(false);
                }

                var sender = entry.UserOperation.Sender?.ToLowerInvariant() ?? "";
                _bySender.AddOrUpdate(
                    sender,
                    _ => new HashSet<string> { entry.UserOpHash },
                    (_, set) => { set.Add(entry.UserOpHash); return set; });

                return Task.FromResult(true);
            }
        }

        public Task<MempoolEntry?> GetAsync(string userOpHash)
        {
            _entries.TryGetValue(userOpHash, out var entry);
            return Task.FromResult(entry);
        }

        public Task<MempoolEntry[]> GetPendingAsync(int maxCount, BigInteger? maxGas = null)
        {
            var pending = _entries.Values
                .Where(e => e.State == MempoolEntryState.Pending)
                .Where(e => !e.ValidAfter.HasValue || e.ValidAfter.Value <= (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                .Where(e => !e.ValidUntil.HasValue || e.ValidUntil.Value > (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                .OrderByDescending(e => e.Priority)
                .ThenBy(e => e.SubmittedAt);

            var result = new List<MempoolEntry>();
            BigInteger totalGas = 0;

            foreach (var entry in pending)
            {
                if (result.Count >= maxCount) break;

                if (maxGas.HasValue)
                {
                    var opGas = GetOperationGas(entry);
                    if (totalGas + opGas > maxGas.Value) continue;
                    totalGas += opGas;
                }

                result.Add(entry);
            }

            return Task.FromResult(result.ToArray());
        }

        public Task<MempoolEntry[]> GetBySenderAsync(string sender)
        {
            var normalizedSender = sender?.ToLowerInvariant() ?? "";
            if (!_bySender.TryGetValue(normalizedSender, out var hashes))
            {
                return Task.FromResult(Array.Empty<MempoolEntry>());
            }

            var entries = hashes
                .Select(h => _entries.TryGetValue(h, out var e) ? e : null)
                .Where(e => e != null)
                .Cast<MempoolEntry>()
                .ToArray();

            return Task.FromResult(entries);
        }

        public Task<bool> RemoveAsync(string userOpHash)
        {
            lock (_lock)
            {
                if (!_entries.TryRemove(userOpHash, out var entry))
                {
                    return Task.FromResult(false);
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

                return Task.FromResult(true);
            }
        }

        public Task MarkSubmittedAsync(string[] userOpHashes, string transactionHash)
        {
            foreach (var hash in userOpHashes)
            {
                if (_entries.TryGetValue(hash, out var entry))
                {
                    entry.State = MempoolEntryState.Submitted;
                    entry.TransactionHash = transactionHash;
                }
            }

            _byTransaction.AddOrUpdate(
                transactionHash,
                _ => new HashSet<string>(userOpHashes),
                (_, set) => { foreach (var h in userOpHashes) set.Add(h); return set; });

            return Task.CompletedTask;
        }

        public Task MarkIncludedAsync(string[] userOpHashes, string transactionHash, BigInteger blockNumber)
        {
            foreach (var hash in userOpHashes)
            {
                if (_entries.TryGetValue(hash, out var entry))
                {
                    entry.State = MempoolEntryState.Included;
                    entry.TransactionHash = transactionHash;
                    entry.BlockNumber = blockNumber;
                }
            }

            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(string[] userOpHashes, string error)
        {
            foreach (var hash in userOpHashes)
            {
                if (_entries.TryGetValue(hash, out var entry))
                {
                    entry.State = MempoolEntryState.Failed;
                    entry.Error = error;
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
                    if (_entries.TryGetValue(hash, out var entry) && entry.State == MempoolEntryState.Submitted)
                    {
                        entry.State = MempoolEntryState.Pending;
                        entry.TransactionHash = null;
                        entry.RetryCount++;
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
            }
            return Task.CompletedTask;
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
            var toRemove = new List<string>();

            foreach (var kvp in _entries)
            {
                var entry = kvp.Value;

                if (entry.State == MempoolEntryState.Included || entry.State == MempoolEntryState.Failed)
                {
                    if (now - entry.SubmittedAt > TimeSpan.FromMinutes(5))
                    {
                        toRemove.Add(kvp.Key);
                    }
                }
                else if (now - entry.SubmittedAt > _entryTtl)
                {
                    toRemove.Add(kvp.Key);
                }
                else if (entry.ValidUntil.HasValue && entry.ValidUntil.Value < (ulong)now.ToUnixTimeSeconds())
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var hash in toRemove)
            {
                await RemoveAsync(hash);
            }

            return toRemove.Count;
        }

        private static BigInteger GetOperationGas(MempoolEntry entry)
        {
            var userOp = entry.UserOperation;
            var accountGasLimits = userOp.AccountGasLimits ?? Array.Empty<byte>();

            BigInteger verificationGas = 0;
            BigInteger callGas = 0;

            if (accountGasLimits.Length >= 32)
            {
                verificationGas = new BigInteger(accountGasLimits.Take(16).Reverse().ToArray(), isUnsigned: true);
                callGas = new BigInteger(accountGasLimits.Skip(16).Take(16).Reverse().ToArray(), isUnsigned: true);
            }

            return verificationGas + callGas + userOp.PreVerificationGas;
        }
    }
}
