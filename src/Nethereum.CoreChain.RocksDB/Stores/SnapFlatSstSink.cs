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

        private readonly SstRowBuffer _accountRows;
        private readonly SstRowBuffer _storageRows;
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
            _accountRows = new SstRowBuffer(_flushEntryThreshold);
            _storageRows = new SstRowBuffer(_flushEntryThreshold);

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
                var anyAccounts = _accountRows.HasRows;
                var anyStorage = _storageRows.HasRows;
                return anyAccounts || anyStorage;
            }
        }

        public Task SaveAccountByHashAsync(byte[] accountHash, Account account)
        {
            if (_abandoned) return Task.CompletedTask;
            var value = _encoder.EncodeFlatAccountValueByHash(account);
            KeyValuePair<byte[], byte[]>? codeHashRow = null;
            if (_encoder.HasExternalCodeHashLayout && account.CodeHash != null)
                codeHashRow = new KeyValuePair<byte[], byte[]>(_encoder.GetCodeHashRowKey(accountHash), account.CodeHash);
            var toFlush = _accountRows.Add(new KeyValuePair<byte[], byte[]>(accountHash, value), codeHashRow);
            if (toFlush != null) IngestSortedLastWins(toFlush, RocksDbManager.CF_STATE_ACCOUNTS);
            return Task.CompletedTask;
        }

        public Task<Account> GetAccountByHashAsync(byte[] accountHash)
            => throw new NotSupportedException(
                "SnapFlatSstSink is a write-only bulk ingestion sink; buffered rows are not readable. Read flat accounts through the state store.");

        public Task DeleteAccountByHashAsync(byte[] accountHash)
        {
            if (_abandoned) return Task.CompletedTask;
            KeyValuePair<byte[], byte[]>? codeHashRow = null;
            if (_encoder.HasExternalCodeHashLayout)
                codeHashRow = new KeyValuePair<byte[], byte[]>(_encoder.GetCodeHashRowKey(accountHash), Tombstone);
            var toFlush = _accountRows.Add(new KeyValuePair<byte[], byte[]>(accountHash, Tombstone), codeHashRow);
            if (toFlush != null) IngestSortedLastWins(toFlush, RocksDbManager.CF_STATE_ACCOUNTS);
            return Task.CompletedTask;
        }

        public Task SaveStorageByHashAsync(byte[] accountHash, byte[] slotKeccak, byte[] value)
        {
            if (_abandoned) return Task.CompletedTask;
            if (SnapFlatStorageValue.ClearsSlot(value)) value = Tombstone;
            var key = FlatStateWalk.OwnerSlotKey(accountHash, slotKeccak);
            var toFlush = _storageRows.Add(new KeyValuePair<byte[], byte[]>(key, value));
            if (toFlush != null) IngestSortedLastWins(toFlush, RocksDbManager.CF_STATE_STORAGE);
            return Task.CompletedTask;
        }

        public void Flush()
        {
            if (_abandoned) return;
            var accounts = _accountRows.Drain();
            var storage = _storageRows.Drain();
            IngestSortedLastWins(accounts, RocksDbManager.CF_STATE_ACCOUNTS);
            IngestSortedLastWins(storage, RocksDbManager.CF_STATE_STORAGE);
        }

        private void IngestSortedLastWins(List<KeyValuePair<byte[], byte[]>> entries, string cfName)
        {
            if (_abandoned) return;
            if (entries == null || entries.Count == 0) return;

            var sorted = SstIngest.SortLastWins(entries);

            var path = Path.Combine(_scratchDir, $"flat_{Interlocked.Increment(ref _sstCounter)}_{Guid.NewGuid():N}.sst");
            long written = SstIngest.Write(path, sorted);
            IngestFile(path, cfName, written, entries.Count);
        }

        private void IngestFile(string path, string cfName, long written, int entryCount)
        {
            lock (_ingestGate)
            {
                if (_abandoned) { try { File.Delete(path); } catch { } return; }
                PreIngestHook?.Invoke();
                using var lease = _rocks.Lease();
                SstIngest.IngestAndDelete(lease.Database, _rocks.GetColumnFamily(cfName), path, MoveFilesIngest);
                Interlocked.Add(ref _rowsIngested, written);
                _logger.LogInformation(
                    "snap.flat.sst ingested {Rows} rows into {Cf} ({Dupes} superseded in-buffer)",
                    written, cfName, entryCount - written);
            }
        }

        public void Abandon()
        {
            lock (_ingestGate) { _abandoned = true; }
            _accountRows.Clear();
            _storageRows.Clear();
        }

        public void Dispose()
        {
            if (!_abandoned) Flush();
            try { if (Directory.Exists(_scratchDir)) Directory.Delete(_scratchDir, recursive: true); }
            catch { }
        }
    }
}
