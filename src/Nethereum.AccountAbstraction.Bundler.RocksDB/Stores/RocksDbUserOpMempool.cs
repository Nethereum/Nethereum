using System.Numerics;
using System.Text;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Serialization;
using RocksDbSharp;

namespace Nethereum.AccountAbstraction.Bundler.RocksDB.Stores
{
    public class RocksDbUserOpMempool : IUserOpMempool
    {
        private readonly BundlerRocksDbManager _manager;
        private readonly BundlerRocksDbOptions _options;
        private readonly object _lock = new();

        public RocksDbUserOpMempool(BundlerRocksDbManager manager, BundlerRocksDbOptions options)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _options = options ?? new BundlerRocksDbOptions();
        }

        public Task<bool> AddAsync(MempoolEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (string.IsNullOrEmpty(entry.UserOpHash)) throw new ArgumentException("UserOpHash required");

            lock (_lock)
            {
                var key = MempoolEntrySerializer.StringToKey(entry.UserOpHash);

                if (ExistsInAnyState(key))
                {
                    return Task.FromResult(false);
                }

                var countResult = CountPendingInternal();
                if (countResult >= _options.MaxMempoolSize)
                {
                    return Task.FromResult(false);
                }

                entry.SubmittedAt = DateTimeOffset.UtcNow;
                entry.State = MempoolEntryState.Pending;

                var batch = _manager.CreateWriteBatch();
                try
                {
                    var data = MempoolEntrySerializer.Serialize(entry);
                    batch.Put(key, data, _manager.GetColumnFamily(BundlerRocksDbManager.CF_USEROP_PENDING));

                    var sender = entry.UserOperation.Sender?.ToLowerInvariant() ?? "";
                    var senderKey = MempoolEntrySerializer.CreateSenderKey(sender, entry.UserOperation.Nonce);
                    batch.Put(senderKey, key, _manager.GetColumnFamily(BundlerRocksDbManager.CF_SENDER_INDEX));

                    _manager.Write(batch);
                    return Task.FromResult(true);
                }
                finally
                {
                    batch.Dispose();
                }
            }
        }

        public Task<MempoolEntry?> GetAsync(string userOpHash)
        {
            var key = MempoolEntrySerializer.StringToKey(userOpHash);

            var data = _manager.Get(BundlerRocksDbManager.CF_USEROP_PENDING, key);
            if (data != null) return Task.FromResult(MempoolEntrySerializer.Deserialize(data));

            data = _manager.Get(BundlerRocksDbManager.CF_USEROP_SUBMITTED, key);
            if (data != null) return Task.FromResult(MempoolEntrySerializer.Deserialize(data));

            data = _manager.Get(BundlerRocksDbManager.CF_USEROP_INCLUDED, key);
            if (data != null) return Task.FromResult(MempoolEntrySerializer.Deserialize(data));

            data = _manager.Get(BundlerRocksDbManager.CF_USEROP_FAILED, key);
            if (data != null) return Task.FromResult(MempoolEntrySerializer.Deserialize(data));

            return Task.FromResult<MempoolEntry?>(null);
        }

        public Task<MempoolEntry[]> GetPendingAsync(int maxCount, BigInteger? maxGas = null)
        {
            var result = new List<MempoolEntry>();
            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            BigInteger totalGas = 0;

            using var iterator = _manager.CreateIterator(BundlerRocksDbManager.CF_USEROP_PENDING);
            iterator.SeekToFirst();

            var pendingEntries = new List<MempoolEntry>();
            while (iterator.Valid())
            {
                var entry = MempoolEntrySerializer.Deserialize(iterator.Value());
                if (entry != null)
                {
                    if (entry.ValidAfter.HasValue && entry.ValidAfter.Value > now)
                    {
                        iterator.Next();
                        continue;
                    }
                    if (entry.ValidUntil.HasValue && entry.ValidUntil.Value <= now)
                    {
                        iterator.Next();
                        continue;
                    }
                    pendingEntries.Add(entry);
                }
                iterator.Next();
            }

            var orderedEntries = pendingEntries
                .OrderByDescending(e => e.Priority)
                .ThenBy(e => e.SubmittedAt);

            foreach (var entry in orderedEntries)
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
            var result = new List<MempoolEntry>();
            var senderPrefix = MempoolEntrySerializer.CreateSenderPrefixKey(sender);

            using var iterator = _manager.CreateIterator(BundlerRocksDbManager.CF_SENDER_INDEX);
            iterator.Seek(senderPrefix);

            while (iterator.Valid())
            {
                var currentKey = iterator.Key();
                if (!currentKey.AsSpan().StartsWith(senderPrefix))
                    break;

                var userOpHashKey = iterator.Value();
                var entry = GetEntryByKey(userOpHashKey);
                if (entry != null)
                {
                    result.Add(entry);
                }

                iterator.Next();
            }

            return Task.FromResult(result.ToArray());
        }

        public Task<bool> RemoveAsync(string userOpHash)
        {
            lock (_lock)
            {
                var key = MempoolEntrySerializer.StringToKey(userOpHash);

                foreach (var cf in new[] {
                    BundlerRocksDbManager.CF_USEROP_PENDING,
                    BundlerRocksDbManager.CF_USEROP_SUBMITTED,
                    BundlerRocksDbManager.CF_USEROP_INCLUDED,
                    BundlerRocksDbManager.CF_USEROP_FAILED })
                {
                    var data = _manager.Get(cf, key);
                    if (data != null)
                    {
                        var entry = MempoolEntrySerializer.Deserialize(data);
                        var batch = _manager.CreateWriteBatch();
                        try
                        {
                            batch.Delete(key, _manager.GetColumnFamily(cf));

                            if (entry != null)
                            {
                                var sender = entry.UserOperation.Sender?.ToLowerInvariant() ?? "";
                                var senderKey = MempoolEntrySerializer.CreateSenderKey(sender, entry.UserOperation.Nonce);
                                batch.Delete(senderKey, _manager.GetColumnFamily(BundlerRocksDbManager.CF_SENDER_INDEX));
                            }

                            _manager.Write(batch);
                            return Task.FromResult(true);
                        }
                        finally
                        {
                            batch.Dispose();
                        }
                    }
                }

                return Task.FromResult(false);
            }
        }

        public Task MarkSubmittedAsync(string[] userOpHashes, string transactionHash)
        {
            lock (_lock)
            {
                var batch = _manager.CreateWriteBatch();
                try
                {
                    var txMappingValue = string.Join(",", userOpHashes);
                    var txKey = MempoolEntrySerializer.StringToKey(transactionHash);
                    batch.Put(txKey, Encoding.UTF8.GetBytes(txMappingValue),
                        _manager.GetColumnFamily(BundlerRocksDbManager.CF_TX_MAPPING));

                    foreach (var hash in userOpHashes)
                    {
                        var key = MempoolEntrySerializer.StringToKey(hash);
                        var data = _manager.Get(BundlerRocksDbManager.CF_USEROP_PENDING, key);
                        if (data != null)
                        {
                            var entry = MempoolEntrySerializer.Deserialize(data);
                            if (entry != null)
                            {
                                entry.State = MempoolEntryState.Submitted;
                                entry.TransactionHash = transactionHash;

                                var newData = MempoolEntrySerializer.Serialize(entry);
                                batch.Delete(key, _manager.GetColumnFamily(BundlerRocksDbManager.CF_USEROP_PENDING));
                                batch.Put(key, newData, _manager.GetColumnFamily(BundlerRocksDbManager.CF_USEROP_SUBMITTED));
                            }
                        }
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

        public Task MarkIncludedAsync(string[] userOpHashes, string transactionHash, BigInteger blockNumber)
        {
            lock (_lock)
            {
                var batch = _manager.CreateWriteBatch();
                try
                {
                    foreach (var hash in userOpHashes)
                    {
                        var key = MempoolEntrySerializer.StringToKey(hash);
                        var data = _manager.Get(BundlerRocksDbManager.CF_USEROP_SUBMITTED, key);
                        if (data != null)
                        {
                            var entry = MempoolEntrySerializer.Deserialize(data);
                            if (entry != null)
                            {
                                entry.State = MempoolEntryState.Included;
                                entry.TransactionHash = transactionHash;
                                entry.BlockNumber = blockNumber;

                                var newData = MempoolEntrySerializer.Serialize(entry);
                                batch.Delete(key, _manager.GetColumnFamily(BundlerRocksDbManager.CF_USEROP_SUBMITTED));
                                batch.Put(key, newData, _manager.GetColumnFamily(BundlerRocksDbManager.CF_USEROP_INCLUDED));
                            }
                        }
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

        public Task MarkFailedAsync(string[] userOpHashes, string error)
        {
            lock (_lock)
            {
                var batch = _manager.CreateWriteBatch();
                try
                {
                    foreach (var hash in userOpHashes)
                    {
                        var key = MempoolEntrySerializer.StringToKey(hash);

                        foreach (var sourceCf in new[] {
                            BundlerRocksDbManager.CF_USEROP_PENDING,
                            BundlerRocksDbManager.CF_USEROP_SUBMITTED })
                        {
                            var data = _manager.Get(sourceCf, key);
                            if (data != null)
                            {
                                var entry = MempoolEntrySerializer.Deserialize(data);
                                if (entry != null)
                                {
                                    entry.State = MempoolEntryState.Failed;
                                    entry.Error = error;

                                    var newData = MempoolEntrySerializer.Serialize(entry);
                                    batch.Delete(key, _manager.GetColumnFamily(sourceCf));
                                    batch.Put(key, newData, _manager.GetColumnFamily(BundlerRocksDbManager.CF_USEROP_FAILED));
                                }
                                break;
                            }
                        }
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

        public Task RevertSubmittedAsync(string transactionHash)
        {
            lock (_lock)
            {
                var txKey = MempoolEntrySerializer.StringToKey(transactionHash);
                var txData = _manager.Get(BundlerRocksDbManager.CF_TX_MAPPING, txKey);
                if (txData == null) return Task.CompletedTask;

                var userOpHashes = Encoding.UTF8.GetString(txData).Split(',');

                var batch = _manager.CreateWriteBatch();
                try
                {
                    batch.Delete(txKey, _manager.GetColumnFamily(BundlerRocksDbManager.CF_TX_MAPPING));

                    foreach (var hash in userOpHashes)
                    {
                        var key = MempoolEntrySerializer.StringToKey(hash);
                        var data = _manager.Get(BundlerRocksDbManager.CF_USEROP_SUBMITTED, key);
                        if (data != null)
                        {
                            var entry = MempoolEntrySerializer.Deserialize(data);
                            if (entry != null)
                            {
                                entry.State = MempoolEntryState.Pending;
                                entry.TransactionHash = null;
                                entry.RetryCount++;

                                var newData = MempoolEntrySerializer.Serialize(entry);
                                batch.Delete(key, _manager.GetColumnFamily(BundlerRocksDbManager.CF_USEROP_SUBMITTED));
                                batch.Put(key, newData, _manager.GetColumnFamily(BundlerRocksDbManager.CF_USEROP_PENDING));
                            }
                        }
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

        public Task ClearAsync()
        {
            lock (_lock)
            {
                foreach (var cf in new[] {
                    BundlerRocksDbManager.CF_USEROP_PENDING,
                    BundlerRocksDbManager.CF_USEROP_SUBMITTED,
                    BundlerRocksDbManager.CF_USEROP_INCLUDED,
                    BundlerRocksDbManager.CF_USEROP_FAILED,
                    BundlerRocksDbManager.CF_SENDER_INDEX,
                    BundlerRocksDbManager.CF_TX_MAPPING })
                {
                    DeleteAllInColumnFamily(cf);
                }
            }
            return Task.CompletedTask;
        }

        public Task<int> CountAsync()
        {
            int count = 0;
            foreach (var cf in new[] {
                BundlerRocksDbManager.CF_USEROP_PENDING,
                BundlerRocksDbManager.CF_USEROP_SUBMITTED,
                BundlerRocksDbManager.CF_USEROP_INCLUDED,
                BundlerRocksDbManager.CF_USEROP_FAILED })
            {
                count += CountInColumnFamily(cf);
            }
            return Task.FromResult(count);
        }

        public Task<MempoolStats> GetStatsAsync()
        {
            var pendingCount = CountInColumnFamily(BundlerRocksDbManager.CF_USEROP_PENDING);
            var submittedCount = CountInColumnFamily(BundlerRocksDbManager.CF_USEROP_SUBMITTED);
            var includedCount = CountInColumnFamily(BundlerRocksDbManager.CF_USEROP_INCLUDED);
            var failedCount = CountInColumnFamily(BundlerRocksDbManager.CF_USEROP_FAILED);

            var uniqueSenders = new HashSet<string>();
            var uniquePaymasters = new HashSet<string>();
            BigInteger totalPrefund = 0;

            void ProcessColumnFamily(string cf)
            {
                using var iterator = _manager.CreateIterator(cf);
                iterator.SeekToFirst();
                while (iterator.Valid())
                {
                    var entry = MempoolEntrySerializer.Deserialize(iterator.Value());
                    if (entry != null)
                    {
                        if (!string.IsNullOrEmpty(entry.UserOperation.Sender))
                            uniqueSenders.Add(entry.UserOperation.Sender.ToLowerInvariant());
                        if (!string.IsNullOrEmpty(entry.Paymaster))
                            uniquePaymasters.Add(entry.Paymaster.ToLowerInvariant());
                        totalPrefund += entry.Prefund;
                    }
                    iterator.Next();
                }
            }

            ProcessColumnFamily(BundlerRocksDbManager.CF_USEROP_PENDING);
            ProcessColumnFamily(BundlerRocksDbManager.CF_USEROP_SUBMITTED);

            return Task.FromResult(new MempoolStats
            {
                TotalCount = pendingCount + submittedCount + includedCount + failedCount,
                PendingCount = pendingCount,
                SubmittedCount = submittedCount,
                IncludedCount = includedCount,
                FailedCount = failedCount,
                UniqueSenders = uniqueSenders.Count,
                UniquePaymasters = uniquePaymasters.Count,
                TotalPrefund = totalPrefund
            });
        }

        public Task<int> PruneAsync()
        {
            var now = DateTimeOffset.UtcNow;
            var toRemove = new List<(string cf, string hash)>();

            void CheckColumnFamily(string cf, TimeSpan retention, bool checkTtl = false, bool checkValidUntil = false)
            {
                using var iterator = _manager.CreateIterator(cf);
                iterator.SeekToFirst();
                while (iterator.Valid())
                {
                    var entry = MempoolEntrySerializer.Deserialize(iterator.Value());
                    if (entry != null)
                    {
                        bool shouldRemove = false;

                        if (now - entry.SubmittedAt > retention)
                            shouldRemove = true;

                        if (checkValidUntil && entry.ValidUntil.HasValue &&
                            entry.ValidUntil.Value < (ulong)now.ToUnixTimeSeconds())
                            shouldRemove = true;

                        if (shouldRemove)
                            toRemove.Add((cf, entry.UserOpHash));
                    }
                    iterator.Next();
                }
            }

            CheckColumnFamily(BundlerRocksDbManager.CF_USEROP_PENDING, _options.EntryTtl, checkValidUntil: true);
            CheckColumnFamily(BundlerRocksDbManager.CF_USEROP_INCLUDED, _options.IncludedEntryRetention);
            CheckColumnFamily(BundlerRocksDbManager.CF_USEROP_FAILED, _options.IncludedEntryRetention);

            lock (_lock)
            {
                foreach (var (cf, hash) in toRemove)
                {
                    var key = MempoolEntrySerializer.StringToKey(hash);
                    _manager.Delete(cf, key);
                }
            }

            return Task.FromResult(toRemove.Count);
        }

        private bool ExistsInAnyState(byte[] key)
        {
            return _manager.KeyExists(BundlerRocksDbManager.CF_USEROP_PENDING, key) ||
                   _manager.KeyExists(BundlerRocksDbManager.CF_USEROP_SUBMITTED, key) ||
                   _manager.KeyExists(BundlerRocksDbManager.CF_USEROP_INCLUDED, key) ||
                   _manager.KeyExists(BundlerRocksDbManager.CF_USEROP_FAILED, key);
        }

        private MempoolEntry? GetEntryByKey(byte[] key)
        {
            foreach (var cf in new[] {
                BundlerRocksDbManager.CF_USEROP_PENDING,
                BundlerRocksDbManager.CF_USEROP_SUBMITTED,
                BundlerRocksDbManager.CF_USEROP_INCLUDED,
                BundlerRocksDbManager.CF_USEROP_FAILED })
            {
                var data = _manager.Get(cf, key);
                if (data != null) return MempoolEntrySerializer.Deserialize(data);
            }
            return null;
        }

        private int CountInColumnFamily(string cf)
        {
            int count = 0;
            using var iterator = _manager.CreateIterator(cf);
            iterator.SeekToFirst();
            while (iterator.Valid())
            {
                count++;
                iterator.Next();
            }
            return count;
        }

        private int CountPendingInternal()
        {
            return CountInColumnFamily(BundlerRocksDbManager.CF_USEROP_PENDING);
        }

        private void DeleteAllInColumnFamily(string cf)
        {
            var batch = _manager.CreateWriteBatch();
            try
            {
                using var iterator = _manager.CreateIterator(cf);
                iterator.SeekToFirst();
                while (iterator.Valid())
                {
                    batch.Delete(iterator.Key(), _manager.GetColumnFamily(cf));
                    iterator.Next();
                }
                _manager.Write(batch);
            }
            finally
            {
                batch.Dispose();
            }
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
