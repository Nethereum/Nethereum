using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class SnapFlatSstSink : IBulkFlatStateSink
    {
        private readonly RocksDbManager _rocks;
        private readonly RocksDbStateStore _encoder;
        private readonly string _scratchDir;
        private readonly ILogger _logger;
        private readonly int _flushEntryThreshold;

        private readonly object _accountsLock = new object();
        private readonly object _storageLock = new object();
        private List<KeyValuePair<byte[], byte[]>> _accounts = new List<KeyValuePair<byte[], byte[]>>();
        private List<KeyValuePair<byte[], byte[]>> _storage = new List<KeyValuePair<byte[], byte[]>>();
        private int _sstCounter;
        private long _rowsIngested;

        private volatile bool _abandoned;

        private readonly object _ingestGate = new object();

        private static readonly byte[] Tombstone = null;

        private static readonly IngestExternalFileOptions MoveFilesIngest = new IngestExternalFileOptions().SetMoveFiles(true);

        public Action PreIngestHook { get; set; }

        public string ScratchDirectory => _scratchDir;

        public SnapFlatSstSink(
            RocksDbManager rocks, RocksDbStateStore encoder, string scratchDir,
            ILogger logger = null, int flushEntryThreshold = 1_500_000)
        {
            _rocks = rocks ?? throw new ArgumentNullException(nameof(rocks));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _scratchDir = scratchDir ?? throw new ArgumentNullException(nameof(scratchDir));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            _flushEntryThreshold = Math.Max(1, flushEntryThreshold);

            Directory.CreateDirectory(_scratchDir);
            foreach (var stale in Directory.GetFiles(_scratchDir, "*.sst"))
            {
                try { File.Delete(stale); } catch { }
            }
        }

        public long RowsIngested => Interlocked.Read(ref _rowsIngested);

        public bool HasBufferedRows
        {
            get
            {
                bool anyAccounts, anyStorage;
                lock (_accountsLock) anyAccounts = _accounts.Count > 0;
                lock (_storageLock) anyStorage = _storage.Count > 0;
                return anyAccounts || anyStorage;
            }
        }

        public Task SaveAccountByHashAsync(byte[] accountHash, Account account)
        {
            if (_abandoned) return Task.CompletedTask;
            var value = _encoder.EncodeFlatAccountValueByHash(account);
            List<KeyValuePair<byte[], byte[]>> toFlush = null;
            lock (_accountsLock)
            {
                _accounts.Add(new KeyValuePair<byte[], byte[]>(accountHash, value));
                if (_encoder.HasExternalCodeHashLayout && account.CodeHash != null)
                    _accounts.Add(new KeyValuePair<byte[], byte[]>(_encoder.GetCodeHashRowKey(accountHash), account.CodeHash));
                if (_accounts.Count >= _flushEntryThreshold)
                {
                    toFlush = _accounts;
                    _accounts = new List<KeyValuePair<byte[], byte[]>>(toFlush.Count);
                }
            }
            if (toFlush != null) IngestSortedLastWins(toFlush, RocksDbManager.CF_STATE_ACCOUNTS);
            return Task.CompletedTask;
        }

        public Task<Account> GetAccountByHashAsync(byte[] accountHash)
            => throw new NotSupportedException(
                "SnapFlatSstSink is a write-only bulk ingestion sink; buffered rows are not readable. Read flat accounts through the state store.");

        public Task DeleteAccountByHashAsync(byte[] accountHash)
        {
            if (_abandoned) return Task.CompletedTask;
            List<KeyValuePair<byte[], byte[]>> toFlush = null;
            lock (_accountsLock)
            {
                _accounts.Add(new KeyValuePair<byte[], byte[]>(accountHash, Tombstone));
                if (_encoder.HasExternalCodeHashLayout)
                    _accounts.Add(new KeyValuePair<byte[], byte[]>(_encoder.GetCodeHashRowKey(accountHash), Tombstone));
                if (_accounts.Count >= _flushEntryThreshold)
                {
                    toFlush = _accounts;
                    _accounts = new List<KeyValuePair<byte[], byte[]>>(toFlush.Count);
                }
            }
            if (toFlush != null) IngestSortedLastWins(toFlush, RocksDbManager.CF_STATE_ACCOUNTS);
            return Task.CompletedTask;
        }

        public Task SaveStorageByHashAsync(byte[] accountHash, byte[] slotKeccak, byte[] value)
        {
            if (_abandoned) return Task.CompletedTask;
            if (SnapFlatStorageValue.ClearsSlot(value)) value = Tombstone;
            var key = new byte[64];
            Buffer.BlockCopy(accountHash, 0, key, 0, 32);
            Buffer.BlockCopy(slotKeccak, 0, key, 32, 32);
            List<KeyValuePair<byte[], byte[]>> toFlush = null;
            lock (_storageLock)
            {
                _storage.Add(new KeyValuePair<byte[], byte[]>(key, value));
                if (_storage.Count >= _flushEntryThreshold)
                {
                    toFlush = _storage;
                    _storage = new List<KeyValuePair<byte[], byte[]>>(toFlush.Count);
                }
            }
            if (toFlush != null) IngestSortedLastWins(toFlush, RocksDbManager.CF_STATE_STORAGE);
            return Task.CompletedTask;
        }

        public void Flush()
        {
            if (_abandoned) return;
            List<KeyValuePair<byte[], byte[]>> accounts, storage;
            lock (_accountsLock) { accounts = _accounts; _accounts = new List<KeyValuePair<byte[], byte[]>>(); }
            lock (_storageLock) { storage = _storage; _storage = new List<KeyValuePair<byte[], byte[]>>(); }
            IngestSortedLastWins(accounts, RocksDbManager.CF_STATE_ACCOUNTS);
            IngestSortedLastWins(storage, RocksDbManager.CF_STATE_STORAGE);
        }

        private void IngestSortedLastWins(List<KeyValuePair<byte[], byte[]>> entries, string cfName)
        {
            if (_abandoned) return;
            if (entries == null || entries.Count == 0) return;

            var indexed = new (byte[] Key, byte[] Value, int Seq)[entries.Count];
            for (int i = 0; i < entries.Count; i++) indexed[i] = (entries[i].Key, entries[i].Value, i);
            Array.Sort(indexed, (a, b) =>
            {
                var c = CompareBytes(a.Key, b.Key);
                return c != 0 ? c : a.Seq.CompareTo(b.Seq);
            });

            var path = Path.Combine(_scratchDir, $"flat_{Interlocked.Increment(ref _sstCounter)}_{Guid.NewGuid():N}.sst");
            long written = 0;
            using (var writer = new SstFileWriter(new EnvOptions(), History.RocksProfiles.IndexSstOptions()))
            {
                writer.Open(path);
                for (int i = 0; i < indexed.Length; i++)
                {
                    if (i + 1 < indexed.Length && CompareBytes(indexed[i].Key, indexed[i + 1].Key) == 0) continue;
                    if (indexed[i].Value == null) writer.Delete(indexed[i].Key);
                    else writer.Put(indexed[i].Key, indexed[i].Value);
                    written++;
                }
                writer.Finish();
            }
            lock (_ingestGate)
            {
                if (_abandoned) { try { File.Delete(path); } catch { } return; }
                PreIngestHook?.Invoke();
                try
                {
                    using var lease = _rocks.Lease();
                    lease.Database.IngestExternalFiles(
                        new[] { path }, MoveFilesIngest, _rocks.GetColumnFamily(cfName));
                    Interlocked.Add(ref _rowsIngested, written);
                    _logger.LogInformation(
                        "snap.flat.sst ingested {Rows} rows into {Cf} ({Dupes} superseded in-buffer)",
                        written, cfName, entries.Count - written);
                }
                finally
                {
                    try { File.Delete(path); } catch { }
                }
            }
        }

        private static int CompareBytes(byte[] a, byte[] b)
        {
            int len = Math.Min(a.Length, b.Length);
            for (int i = 0; i < len; i++)
            {
                int d = a[i] - b[i];
                if (d != 0) return d;
            }
            return a.Length - b.Length;
        }

        public void Abandon()
        {
            lock (_ingestGate) { _abandoned = true; }
            lock (_accountsLock) { _accounts = new List<KeyValuePair<byte[], byte[]>>(); }
            lock (_storageLock) { _storage = new List<KeyValuePair<byte[], byte[]>>(); }
        }

        public void Dispose()
        {
            if (!_abandoned) Flush();
            try { if (Directory.Exists(_scratchDir)) Directory.Delete(_scratchDir, recursive: true); }
            catch { }
        }
    }
}
