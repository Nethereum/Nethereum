using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using RocksDbSharp;

namespace Nethereum.CheckpointReplay
{
    /// <summary>
    /// READ-ONLY view over the certified checkpoint RocksDB. Opens the DB via
    /// <see cref="RocksDb.OpenReadOnly"/> (no WAL replay, no compaction, no write) and exposes the flat
    /// account/storage/code CFs plus the path-keyed trie-node CFs using the exact key/value encodings of
    /// <c>RocksDbStateStore</c> and <c>RocksDbPathTrieNodeStore</c>. The checkpoint is NEVER modified.
    /// </summary>
    public sealed class CheckpointReader : IDisposable
    {
        private readonly RocksDb _db;
        private readonly ColumnFamilyHandle _accounts;
        private readonly ColumnFamilyHandle _storage;
        private readonly ColumnFamilyHandle _code;
        private readonly ColumnFamilyHandle _trieAccount;
        private readonly ColumnFamilyHandle _trieStorage;

        public CheckpointReader(string path)
        {
            var names = new List<string> { "default" };
            names.AddRange(RocksDbManager.ColumnFamilyNames);
            names.AddRange(HistoryColumnFamilies.Catalogue.Select(c => c.Name));

            var cfs = new ColumnFamilies();
            foreach (var n in names.Distinct())
                cfs.Add(n, new ColumnFamilyOptions());

            var options = new DbOptions();
            // Lazy, low-footprint open: cap the table cache so the read-only open does NOT preload every SST
            // handle across a multi-TB LIVE store (default max_open_files=-1 preloads all and hangs/thrashes).
            // SSTs are then opened on demand for the handful of nodes a single-block descent actually reads.
            options.SetMaxOpenFiles(256);
            // errIfLogFileExists = false: the checkpoint was flushed at creation, WAL is consistent.
            _db = RocksDb.OpenReadOnly(options, path, cfs, false);

            _accounts = _db.GetColumnFamily(RocksDbManager.CF_STATE_ACCOUNTS);
            _storage = _db.GetColumnFamily(RocksDbManager.CF_STATE_STORAGE);
            _code = _db.GetColumnFamily(RocksDbManager.CF_STATE_CODE);
            _trieAccount = _db.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_ACCOUNT);
            _trieStorage = _db.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_STORAGE);
        }

        // Scan CF_NODE_HISTORY_INDEX for a storage owner: prints every (path, block) at which a node under this
        // owner was written. Answers "when was (owner, path) last written" — root path is empty. Index key layout:
        // ['O'][owner][pathLen(1)][path][block(8 BE)].
        public void DumpHistory(byte[] owner)
        {
            var cf = _db.GetColumnFamily(RocksDbManager.CF_NODE_HISTORY_INDEX);
            var prefix = new byte[1 + owner.Length];
            prefix[0] = (byte)'O';
            System.Buffer.BlockCopy(owner, 0, prefix, 1, owner.Length);
            using var it = _db.NewIterator(cf: cf);
            it.Seek(prefix);
            int n = 0; ulong rootLast = 0; int rootCount = 0;
            while (it.Valid())
            {
                var key = it.Key();
                if (key.Length < prefix.Length) break;
                bool match = true;
                for (int i = 0; i < prefix.Length; i++) if (key[i] != prefix[i]) { match = false; break; }
                if (!match) break;
                int off = prefix.Length;
                int pathLen = key[off]; off += 1;
                var path = new byte[pathLen];
                System.Buffer.BlockCopy(key, off, path, 0, pathLen); off += pathLen;
                ulong block = 0; for (int i = 0; i < 8; i++) block = (block << 8) | key[off + i];
                bool isRoot = pathLen == 0;
                if (isRoot) { rootLast = block; rootCount++; }
                if (n < 200 || isRoot)
                    Console.WriteLine($"HIST path={(pathLen == 0 ? "(root)" : BitConverter.ToString(path).Replace("-", "").ToLower())} block={block}{(isRoot ? "  <== ROOT" : "")}");
                it.Next(); n++;
                if (n > 20000) { Console.WriteLine("... truncated"); break; }
            }
            Console.WriteLine($"HIST-SUMMARY owner=0x{BitConverter.ToString(owner).Replace("-", "").ToLower()} total_entries={n} root_writes={rootCount} root_last_block={rootLast}");
        }

        // ---- Flat state (RocksDbStateStore layout) ----

        // Account value layout: address[20] || AccountEncoder RLP (unversioned; RlpAccountLayout default).
        public Account GetAccount(string address)
        {
            var key = StateKeys.AccountKey(address);
            var data = _db.Get(key, _accounts);
            if (data == null || data.Length < 20) return null;
            var encoded = new byte[data.Length - 20];
            Buffer.BlockCopy(data, 20, encoded, 0, encoded.Length);
            return AccountEncoder.Current.Decode(encoded);
        }

        // Storage value: raw (trimmed) slot bytes keyed by keccak(addr)||keccak(slot).
        public byte[] GetStorage(string address, BigInteger slot)
        {
            var acct = StateKeys.AccountKey(address);
            var slotKey = StateKeys.StorageSlotKey(slot);
            var key = new byte[64];
            Buffer.BlockCopy(acct, 0, key, 0, 32);
            Buffer.BlockCopy(slotKey, 0, key, 32, 32);
            return _db.Get(key, _storage);
        }

        // Flat account keyed directly by accountHash = keccak(address) (the same key a storage owner uses).
        public Account GetAccountByHash(byte[] accountHash)
        {
            var data = _db.Get(accountHash, _accounts);
            if (data == null || data.Length < 20) return null;
            var encoded = new byte[data.Length - 20];
            Buffer.BlockCopy(data, 20, encoded, 0, encoded.Length);
            return AccountEncoder.Current.Decode(encoded);
        }

        public byte[] GetCode(byte[] codeHash)
        {
            if (codeHash == null) return null;
            return _db.Get(codeHash, _code);
        }

        // ---- Path-keyed trie nodes (RocksDbPathTrieNodeStore layout) ----
        // Account trie: CF_STATE_TRIE_ACCOUNT, key = path.  Storage trie: CF_STATE_TRIE_STORAGE, key = owner||path.

        public byte[] GetTrieNode(byte[] owner, byte[] path)
        {
            if (owner == null || owner.Length == 0)
                return _db.Get(path ?? Array.Empty<byte>(), _trieAccount);
            return _db.Get(StorageKey(owner, path), _trieStorage);
        }

        public bool TrieNodeExists(byte[] owner, byte[] path)
        {
            var v = GetTrieNode(owner, path);
            return v != null;
        }

        internal static byte[] StorageKey(byte[] owner, byte[] path)
        {
            var pathLen = path?.Length ?? 0;
            var key = new byte[owner.Length + pathLen];
            Buffer.BlockCopy(owner, 0, key, 0, owner.Length);
            if (pathLen > 0) Buffer.BlockCopy(path, 0, key, owner.Length, pathLen);
            return key;
        }

        public void Dispose() => _db?.Dispose();
    }
}
