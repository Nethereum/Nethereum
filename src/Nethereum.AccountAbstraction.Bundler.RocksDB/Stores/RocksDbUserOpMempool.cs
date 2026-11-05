using System.Numerics;
using System.Text;
using Microsoft.Extensions.Logging;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.RocksDB.Serialization;
using Nethereum.AccountAbstraction.Bundler.Validation;
using Nethereum.AccountAbstraction.Structs;
using RocksDbSharp;

namespace Nethereum.AccountAbstraction.Bundler.RocksDB.Stores
{
    public class RocksDbUserOpMempool : IUserOpMempool, IMempoolEvictionLog, IMempoolInFlight, IMempoolPendingPool
    {
        public const int SenderIndexSchemaVersion = 2;
        public const string SenderIndexSchemaVersionMetadataKey = "sender_index_schema_version";
        private static readonly byte[] SenderIndexSchemaVersionKeyBytes =
            Encoding.UTF8.GetBytes(SenderIndexSchemaVersionMetadataKey);

        private readonly BundlerRocksDbManager _manager;
        private readonly BundlerRocksDbOptions _options;
        private readonly object _lock = new();
        private readonly List<MempoolEntry> _evicted = new();
        private readonly ILogger? _logger;
        private int _pendingCount;

        public RocksDbUserOpMempool(BundlerRocksDbManager manager, BundlerRocksDbOptions options)
            : this(manager, options, null)
        {
        }

        public RocksDbUserOpMempool(BundlerRocksDbManager manager, BundlerRocksDbOptions options, ILogger? logger)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _options = options ?? new BundlerRocksDbOptions();
            _logger = logger;

            EnsureSenderIndexSchema();
            ReleaseOrphanedInFlight();
            Interlocked.Exchange(ref _pendingCount, CountInColumnFamily(BundlerRocksDbManager.CF_USEROP_PENDING));
        }

        private void AdjustPending(int delta) => Interlocked.Add(ref _pendingCount, delta);

        private void ReleaseOrphanedInFlight()
        {
            lock (_lock)
            {
                var submitted = ReadAll(BundlerRocksDbManager.CF_USEROP_SUBMITTED)
                    .Select(e => e.UserOpHash)
                    .ToArray();
                MoveEntries(
                    submitted,
                    BundlerRocksDbManager.CF_USEROP_SUBMITTED,
                    BundlerRocksDbManager.CF_USEROP_PENDING,
                    entry => { MempoolStateTransitions.Requeue(entry); return true; });
                DeleteAllInColumnFamily(BundlerRocksDbManager.CF_TX_MAPPING);
            }
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
                    foreach (var (entryKey, entry) in ReadAllWithKeys(cf))
                    {
                        var sender = entry.UserOperation.Sender?.ToLowerInvariant() ?? "";
                        var senderKey = MempoolEntrySerializer.CreateSenderKey(
                            sender, entry.EntryPoint, entry.UserOperation.Nonce);
                        batch.Put(senderKey, entryKey, senderIndexCf);
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

                var sender = entry.UserOperation.Sender?.ToLowerInvariant() ?? "";
                var senderKey = MempoolEntrySerializer.CreateSenderKey(
                    sender, entry.EntryPoint, entry.UserOperation.Nonce);

                var occupyingHashKey = _manager.Get(BundlerRocksDbManager.CF_SENDER_INDEX, senderKey);
                var decision = MempoolAdmission.Decide(
                    ExistsInAnyState(key),
                    occupyingHashKey != null ? GetEntryByKey(occupyingHashKey) : null,
                    CountPendingInternal(),
                    _options.MaxMempoolSize,
                    () => ReadAll(BundlerRocksDbManager.CF_USEROP_PENDING),
                    entry);

                if (!decision.Accepted) return Task.FromResult(decision.Outcome);

                if (decision.Evicted != null)
                {
                    RemoveInternal(decision.Evicted.UserOpHash);
                    _evicted.Add(decision.Evicted);
                    if (_evicted.Count > _options.MaxMempoolSize) _evicted.RemoveAt(0);
                }

                InsertInternal(entry, key, senderKey, replacedHashKey: decision.Replaced != null ? occupyingHashKey : null);
                return Task.FromResult(decision.Outcome);
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
                else
                {
                    AdjustPending(1);
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

            return Task.FromResult(GetEntryByKey(key));
        }

        public Task<MempoolEntry[]> GetPendingAsync(int maxCount, BigInteger? maxGas = null)
        {
            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var eligible = ReadAll(BundlerRocksDbManager.CF_USEROP_PENDING)
                .Where(entry => ValidationValidityWindow.IsValidAtTime(entry.ValidAfter, entry.ValidUntil, now))
                .ToList();

            return Task.FromResult(MempoolPendingChains.SelectBundleCandidates(eligible, maxCount, maxGas));
        }

        public Task<MempoolEntry[]> GetAllPendingAsync()
        {
            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var ordered = ReadAll(BundlerRocksDbManager.CF_USEROP_PENDING)
                .Where(entry => ValidationValidityWindow.IsValidAtTime(entry.ValidAfter, entry.ValidUntil, now))
                .OrderBy(e => e.SubmittedAt)
                .ToArray();
            return Task.FromResult(ordered);
        }

        public Task<MempoolEntry[]> GetPendingPoolAsync()
        {
            var pool = ReadAll(BundlerRocksDbManager.CF_USEROP_PENDING)
                .OrderBy(e => e.SubmittedAt)
                .ToArray();
            return Task.FromResult(pool);
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
                    var entry = TryDecode(data, out _);
                    var batch = _manager.CreateWriteBatch();
                    try
                    {
                        batch.Delete(key, _manager.GetColumnFamily(cf));
                        if (cf == BundlerRocksDbManager.CF_USEROP_PENDING)
                        {
                            AdjustPending(-1);
                        }

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
                        var pendingData = _manager.Get(BundlerRocksDbManager.CF_USEROP_PENDING, key);
                        var data = pendingData ?? _manager.Get(BundlerRocksDbManager.CF_USEROP_SUBMITTED, key);
                        if (data != null)
                        {
                            var entry = MempoolEntrySerializer.Deserialize(data);
                            if (entry != null)
                            {
                                MempoolStateTransitions.TrySubmit(entry, transactionHash);

                                var newData = MempoolEntrySerializer.Serialize(entry);
                                if (pendingData != null)
                                {
                                    batch.Delete(key, _manager.GetColumnFamily(BundlerRocksDbManager.CF_USEROP_PENDING));
                                    AdjustPending(-1);
                                }
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

        public Task<string[]> MarkInFlightAsync(string[] userOpHashes)
        {
            lock (_lock)
            {
                return Task.FromResult(MoveEntries(
                    userOpHashes,
                    BundlerRocksDbManager.CF_USEROP_PENDING,
                    BundlerRocksDbManager.CF_USEROP_SUBMITTED,
                    MempoolStateTransitions.TryMarkInFlight));
            }
        }

        public Task ReleaseInFlightAsync(string[] userOpHashes)
        {
            lock (_lock)
            {
                ReleaseInFlight(userOpHashes);
            }
            return Task.CompletedTask;
        }

        private void ReleaseInFlight(string[] userOpHashes)
        {
            MoveEntries(
                userOpHashes,
                BundlerRocksDbManager.CF_USEROP_SUBMITTED,
                BundlerRocksDbManager.CF_USEROP_PENDING,
                MempoolStateTransitions.TryRelease);
        }

        private string[] MoveEntries(
            string[] userOpHashes, string fromCf, string toCf, Func<MempoolEntry, bool> transition)
        {
            var moved = new List<string>();
            var batch = _manager.CreateWriteBatch();
            try
            {
                foreach (var hash in userOpHashes)
                {
                    var key = MempoolEntrySerializer.StringToKey(hash);
                    var data = _manager.Get(fromCf, key);
                    if (data == null) continue;

                    var entry = MempoolEntrySerializer.Deserialize(data);
                    if (entry == null || !transition(entry)) continue;

                    batch.Delete(key, _manager.GetColumnFamily(fromCf));
                    batch.Put(key, MempoolEntrySerializer.Serialize(entry), _manager.GetColumnFamily(toCf));
                    moved.Add(hash);
                }

                if (fromCf == BundlerRocksDbManager.CF_USEROP_PENDING)
                {
                    AdjustPending(-moved.Count);
                }

                if (toCf == BundlerRocksDbManager.CF_USEROP_PENDING)
                {
                    AdjustPending(moved.Count);
                }

                _manager.Write(batch);
                return moved.ToArray();
            }
            finally
            {
                batch.Dispose();
            }
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
                            BundlerRocksDbManager.CF_USEROP_FAILED,
                            BundlerRocksDbManager.CF_USEROP_INCLUDED })
                        {
                            var data = _manager.Get(sourceCf, key);
                            if (data == null) continue;

                            var entry = MempoolEntrySerializer.Deserialize(data);
                            if (entry != null)
                            {
                                if (!MempoolStateTransitions.TryInclude(entry, transactionHash, blockNumber, blockHash)) break;

                                var newData = MempoolEntrySerializer.Serialize(entry);
                                if (sourceCf != BundlerRocksDbManager.CF_USEROP_INCLUDED)
                                {
                                    batch.Delete(key, _manager.GetColumnFamily(sourceCf));
                                }
                                if (sourceCf == BundlerRocksDbManager.CF_USEROP_PENDING)
                                {
                                    AdjustPending(-1);
                                }
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

                                    MempoolStateTransitions.TryFail(entry, error);

                                    var newData = MempoolEntrySerializer.Serialize(entry);
                                    batch.Delete(key, _manager.GetColumnFamily(sourceCf));
                                    if (sourceCf == BundlerRocksDbManager.CF_USEROP_PENDING)
                                    {
                                        AdjustPending(-1);
                                    }
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
                                if (!MempoolStateTransitions.TryRevert(entry)) continue;

                                var newData = MempoolEntrySerializer.Serialize(entry);
                                batch.Delete(key, _manager.GetColumnFamily(BundlerRocksDbManager.CF_USEROP_SUBMITTED));
                                batch.Put(key, newData, _manager.GetColumnFamily(BundlerRocksDbManager.CF_USEROP_PENDING));
                                AdjustPending(1);
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
                _evicted.Clear();
                Interlocked.Exchange(ref _pendingCount, 0);
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
                foreach (var entry in ReadAll(cf))
                {
                    if (!string.IsNullOrEmpty(entry.UserOperation.Sender))
                        uniqueSenders.Add(entry.UserOperation.Sender.ToLowerInvariant());
                    if (!string.IsNullOrEmpty(entry.Paymaster))
                        uniquePaymasters.Add(entry.Paymaster.ToLowerInvariant());
                    totalPrefund += entry.Prefund;
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
            var expired = new[] {
                    BundlerRocksDbManager.CF_USEROP_PENDING,
                    BundlerRocksDbManager.CF_USEROP_SUBMITTED,
                    BundlerRocksDbManager.CF_USEROP_INCLUDED,
                    BundlerRocksDbManager.CF_USEROP_FAILED }
                .SelectMany(cf => ReadAll(cf))
                .Where(entry => MempoolPruneRule.IsExpired(entry, now, _options.EntryTtl, _options.IncludedEntryRetention))
                .Select(entry => entry.UserOpHash)
                .ToList();

            var removedCount = 0;
            lock (_lock)
            {
                foreach (var hash in expired)
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
                if (data != null) return TryRead(cf, key, data);
            }
            return null;
        }

        private List<MempoolEntry> ReadAll(string cf)
        {
            return ReadAllWithKeys(cf).Select(pair => pair.Entry).ToList();
        }

        private List<(byte[] Key, MempoolEntry Entry)> ReadAllWithKeys(string cf)
        {
            var result = new List<(byte[] Key, MempoolEntry Entry)>();
            var corrupt = new List<(byte[] Key, string Reason)>();

            using (var iterator = _manager.CreateIterator(cf))
            {
                iterator.SeekToFirst();
                while (iterator.Valid())
                {
                    var key = iterator.Key();
                    var entry = TryDecode(iterator.Value(), out var reason);
                    if (entry != null)
                    {
                        result.Add((key, entry));
                    }
                    else
                    {
                        corrupt.Add((key, reason));
                    }
                    iterator.Next();
                }
            }

            foreach (var (key, reason) in corrupt)
            {
                Quarantine(cf, key, reason);
            }

            return result;
        }

        private MempoolEntry? TryRead(string cf, byte[] key, byte[] data)
        {
            var entry = TryDecode(data, out var reason);
            if (entry == null)
            {
                Quarantine(cf, key, reason);
            }
            return entry;
        }

        private MempoolEntry? TryDecode(byte[] data, out string reason)
        {
            reason = string.Empty;

            MempoolEntry? entry;
            try
            {
                entry = MempoolEntrySerializer.Deserialize(data);
            }
            catch (Exception ex)
            {
                reason = ex.Message;
                return null;
            }

            if (entry == null || entry.UserOperation == null || string.IsNullOrEmpty(entry.UserOpHash))
            {
                reason = "row holds no user operation";
                return null;
            }

            if (!IsStructurallySound(entry.UserOperation))
            {
                reason = "row holds a user operation with inconsistent field sizes";
                return null;
            }

            return entry;
        }

        private static bool IsStructurallySound(PackedUserOperation userOp)
        {
            return IsEmptyOrLength(userOp.AccountGasLimits, 32) &&
                   IsEmptyOrLength(userOp.GasFees, 32) &&
                   (string.IsNullOrEmpty(userOp.Sender) || userOp.Sender.Length == 42);
        }

        private static bool IsEmptyOrLength(byte[]? data, int length) =>
            data == null || data.Length == 0 || data.Length == length;

        private void Quarantine(string cf, byte[] key, string reason)
        {
            _logger?.LogWarning(
                "Dropping undecodable bundler mempool row {Key} from {ColumnFamily}: {Reason}",
                Encoding.UTF8.GetString(key), cf, reason);
            _manager.Delete(cf, key);
            if (cf == BundlerRocksDbManager.CF_USEROP_PENDING)
            {
                AdjustPending(-1);
            }
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
            return Volatile.Read(ref _pendingCount);
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
