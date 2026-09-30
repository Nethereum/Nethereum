using System.Numerics;
using System.Text;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Serialization;
using RocksDbSharp;

namespace Nethereum.AccountAbstraction.Bundler.RocksDB.Stores
{
    public class RocksDbUserOpMempool : IUserOpMempool
    {
        public const int SenderIndexSchemaVersion = 2;
        public const string SenderIndexSchemaVersionMetadataKey = "sender_index_schema_version";
        private static readonly byte[] SenderIndexSchemaVersionKeyBytes =
            Encoding.UTF8.GetBytes(SenderIndexSchemaVersionMetadataKey);

        private readonly BundlerRocksDbManager _manager;
        private readonly BundlerRocksDbOptions _options;
        private readonly object _lock = new();

        public RocksDbUserOpMempool(BundlerRocksDbManager manager, BundlerRocksDbOptions options)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _options = options ?? new BundlerRocksDbOptions();

            EnsureSenderIndexSchema();
        }

        private void EnsureSenderIndexSchema()
        {
            lock (_lock)
            {
                var stored = _manager.Get(BundlerRocksDbManager.CF_METADATA, SenderIndexSchemaVersionKeyBytes);
                if (stored != null && stored.Length == sizeof(int) &&
                    BitConverter.ToInt32(stored, 0) == SenderIndexSchemaVersion)
                {
                    return;
                }

                RebuildSenderIndex();
            }
        }

        private void RebuildSenderIndex()
        {
            var senderIndexCf = _manager.GetColumnFamily(BundlerRocksDbManager.CF_SENDER_INDEX);
            var batch = _manager.CreateWriteBatch();
            try
            {
                using (var iterator = _manager.CreateIterator(BundlerRocksDbManager.CF_SENDER_INDEX))
                {
                    iterator.SeekToFirst();
                    while (iterator.Valid())
                    {
                        batch.Delete(iterator.Key(), senderIndexCf);
                        iterator.Next();
                    }
                }

                foreach (var cf in new[] {
                    BundlerRocksDbManager.CF_USEROP_INCLUDED,
                    BundlerRocksDbManager.CF_USEROP_FAILED,
                    BundlerRocksDbManager.CF_USEROP_SUBMITTED,
                    BundlerRocksDbManager.CF_USEROP_PENDING })
                {
                    using var iterator = _manager.CreateIterator(cf);
                    iterator.SeekToFirst();
                    while (iterator.Valid())
                    {
                        var entry = MempoolEntrySerializer.Deserialize(iterator.Value());
                        if (entry?.UserOperation != null)
                        {
                            var sender = entry.UserOperation.Sender?.ToLowerInvariant() ?? "";
                            var senderKey = MempoolEntrySerializer.CreateSenderKey(
                                sender, entry.EntryPoint, entry.UserOperation.Nonce);
                            batch.Put(senderKey, iterator.Key(), senderIndexCf);
                        }
                        iterator.Next();
                    }
                }

                batch.Put(
                    SenderIndexSchemaVersionKeyBytes,
                    BitConverter.GetBytes(SenderIndexSchemaVersion),
                    _manager.GetColumnFamily(BundlerRocksDbManager.CF_METADATA));

                _manager.Write(batch);
            }
            finally
            {
                batch.Dispose();
            }
        }

        public Task<MempoolAddOutcome> AddAsync(MempoolEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (string.IsNullOrEmpty(entry.UserOpHash)) throw new ArgumentException("UserOpHash required");

            lock (_lock)
            {
                var key = MempoolEntrySerializer.StringToKey(entry.UserOpHash);

                if (ExistsInAnyState(key))
                {
                    return Task.FromResult(MempoolAddOutcome.RejectedDuplicate);
                }

                var sender = entry.UserOperation.Sender?.ToLowerInvariant() ?? "";
                var senderKey = MempoolEntrySerializer.CreateSenderKey(
                    sender, entry.EntryPoint, entry.UserOperation.Nonce);

                var occupyingHashKey = _manager.Get(BundlerRocksDbManager.CF_SENDER_INDEX, senderKey);
                var occupyingEntry = occupyingHashKey != null ? GetEntryByKey(occupyingHashKey) : null;

                if (occupyingEntry != null &&
                    (occupyingEntry.State == MempoolEntryState.Pending ||
                     occupyingEntry.State == MempoolEntryState.Submitted))
                {
                    if (occupyingEntry.State == MempoolEntryState.Submitted)
                    {
                        return Task.FromResult(MempoolAddOutcome.RejectedDuplicate);
                    }

                    if (!MempoolReplacementRules.IsValidFeeBump(occupyingEntry.UserOperation, entry.UserOperation))
                    {
                        return Task.FromResult(MempoolAddOutcome.RejectedUnderpriced);
                    }

                    InsertInternal(entry, key, senderKey, replacedHashKey: occupyingHashKey);
                    return Task.FromResult(MempoolAddOutcome.Replaced);
                }

                if (CountPendingInternal() >= _options.MaxMempoolSize)
                {
                    return Task.FromResult(MempoolAddOutcome.RejectedFull);
                }

                InsertInternal(entry, key, senderKey, replacedHashKey: null);
                return Task.FromResult(MempoolAddOutcome.Added);
            }
        }

        private void InsertInternal(MempoolEntry entry, byte[] key, byte[] senderKey, byte[]? replacedHashKey)
        {
            entry.SubmittedAt = DateTimeOffset.UtcNow;
            entry.State = MempoolEntryState.Pending;

            var batch = _manager.CreateWriteBatch();
            try
            {
                if (replacedHashKey != null)
                {
                    batch.Delete(replacedHashKey, _manager.GetColumnFamily(BundlerRocksDbManager.CF_USEROP_PENDING));
                }

                var data = MempoolEntrySerializer.Serialize(entry);
                batch.Put(key, data, _manager.GetColumnFamily(BundlerRocksDbManager.CF_USEROP_PENDING));
                batch.Put(senderKey, key, _manager.GetColumnFamily(BundlerRocksDbManager.CF_SENDER_INDEX));

                _manager.Write(batch);
            }
            finally
            {
                batch.Dispose();
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
            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            using var iterator = _manager.CreateIterator(BundlerRocksDbManager.CF_USEROP_PENDING);
            iterator.SeekToFirst();

            var eligible = new List<MempoolEntry>();
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
                    eligible.Add(entry);
                }
                iterator.Next();
            }

            return Task.FromResult(MempoolPendingChains.SelectBundleCandidates(eligible, maxCount, maxGas));
        }

        public Task<MempoolEntry[]> GetAllPendingAsync()
        {
            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var pendingEntries = new List<MempoolEntry>();

            using var iterator = _manager.CreateIterator(BundlerRocksDbManager.CF_USEROP_PENDING);
            iterator.SeekToFirst();

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

            var ordered = pendingEntries.OrderBy(e => e.SubmittedAt).ToArray();
            return Task.FromResult(ordered);
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
                return Task.FromResult(RemoveInternal(userOpHash));
            }
        }

        private bool RemoveInternal(string userOpHash)
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
                            var senderKey = MempoolEntrySerializer.CreateSenderKey(
                                sender, entry.EntryPoint, entry.UserOperation.Nonce);

                            var indexValue = _manager.Get(BundlerRocksDbManager.CF_SENDER_INDEX, senderKey);
                            if (indexValue != null && indexValue.AsSpan().SequenceEqual(key))
                            {
                                batch.Delete(senderKey, _manager.GetColumnFamily(BundlerRocksDbManager.CF_SENDER_INDEX));
                            }
                        }

                        _manager.Write(batch);
                        return true;
                    }
                    finally
                    {
                        batch.Dispose();
                    }
                }
            }

            return false;
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

        public Task MarkIncludedAsync(string[] userOpHashes, string transactionHash, BigInteger blockNumber, string? blockHash = null)
        {
            lock (_lock)
            {
                var batch = _manager.CreateWriteBatch();
                try
                {
                    batch.Delete(
                        MempoolEntrySerializer.StringToKey(transactionHash),
                        _manager.GetColumnFamily(BundlerRocksDbManager.CF_TX_MAPPING));

                    foreach (var hash in userOpHashes)
                    {
                        var key = MempoolEntrySerializer.StringToKey(hash);

                        foreach (var sourceCf in new[] {
                            BundlerRocksDbManager.CF_USEROP_SUBMITTED,
                            BundlerRocksDbManager.CF_USEROP_PENDING,
                            BundlerRocksDbManager.CF_USEROP_FAILED })
                        {
                            var data = _manager.Get(sourceCf, key);
                            if (data == null) continue;

                            var entry = MempoolEntrySerializer.Deserialize(data);
                            if (entry != null)
                            {
                                entry.State = MempoolEntryState.Included;
                                entry.TransactionHash = transactionHash;
                                entry.BlockNumber = blockNumber;
                                entry.BlockHash = blockHash;
                                entry.Error = null;

                                var newData = MempoolEntrySerializer.Serialize(entry);
                                batch.Delete(key, _manager.GetColumnFamily(sourceCf));
                                batch.Put(key, newData, _manager.GetColumnFamily(BundlerRocksDbManager.CF_USEROP_INCLUDED));
                            }
                            break;
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
                                    if (!string.IsNullOrEmpty(entry.TransactionHash))
                                    {
                                        batch.Delete(
                                            MempoolEntrySerializer.StringToKey(entry.TransactionHash),
                                            _manager.GetColumnFamily(BundlerRocksDbManager.CF_TX_MAPPING));
                                    }

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

            var removedCount = 0;
            lock (_lock)
            {
                foreach (var (_, hash) in toRemove)
                {
                    if (RemoveInternal(hash)) removedCount++;
                }
            }

            return Task.FromResult(removedCount);
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

    }
}
