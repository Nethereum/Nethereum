using System;
using System.Collections.Generic;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class PathHealNodeSink : IHealNodeSink
    {
        private static readonly IHashProvider Keccak = Sha3KeccackHashProvider.Instance;

        private readonly RocksDbPathTrieNodeStore _store;
        private readonly RocksDbManager _manager;

        private readonly Dictionary<string, (bool IsStorage, byte[] Key, byte[] Blob)> _buffer = new();

        public PathHealNodeSink(RocksDbPathTrieNodeStore store, RocksDbManager manager)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public bool HasNode(bool isStorage, byte[] accountHash, byte[] nibblePath, byte[] expectedHash)
            => Probe(isStorage, accountHash, nibblePath, expectedHash) == HealNodePresence.Match;

        public HealNodePresence Probe(bool isStorage, byte[] accountHash, byte[] nibblePath, byte[] expectedHash)
        {
            var blob = ReadCurrent(isStorage, accountHash, nibblePath);
            if (blob == null || blob.Length < 32) return HealNodePresence.Absent;
            return ByteUtil.AreEqual(Keccak.ComputeHash(blob), expectedHash)
                ? HealNodePresence.Match
                : HealNodePresence.Stale;
        }

        public void PutNode(bool isStorage, byte[] accountHash, byte[] nibblePath, byte[] expectedHash, byte[] blob)
        {
            if (blob == null || blob.Length < 32) return;
            var key = MakeKey(isStorage, accountHash, nibblePath);
            _buffer[BufferKey(isStorage, key)] = (isStorage, key, blob);
        }

        private readonly List<byte[]> _pendingWipes = new();
        private readonly HashSet<string> _pendingWipeOwners = new(StringComparer.Ordinal);

        public void WipeStorage(byte[] accountHash)
        {
            WipeStorage(accountHash, force: false);
        }

        public void ForceWipeStorage(byte[] accountHash)
        {
            WipeStorage(accountHash, force: true);
        }

        private void WipeStorage(byte[] accountHash, bool force)
        {
            if (accountHash == null || accountHash.Length == 0) return;
            var ownerHex = accountHash.ToHex();

            var prefix = "s:" + ownerHex;
            var stale = new List<string>();
            foreach (var k in _buffer.Keys)
                if (k.StartsWith(prefix, StringComparison.Ordinal)) stale.Add(k);
            foreach (var k in stale) _buffer.Remove(k);

            var occupied = force
                || stale.Count > 0
                || _store.TryGetRaw(accountHash, Array.Empty<byte>()) != null;
            if (!occupied) return;

            if (_pendingWipeOwners.Add(ownerHex)) _pendingWipes.Add(accountHash);
        }

        public bool HasRoot(byte[] root)
        {
            if (root == null || root.Length != 32) return false;
            var blob = ReadCurrent(isStorage: false, accountHash: null, nibblePath: Array.Empty<byte>());
            return blob != null && ByteUtil.AreEqual(Keccak.ComputeHash(blob), root);
        }

        private const int ReadBackSampleInterval = 1024;
        private long _writesSinceSample = -1;

        public void Flush()
        {
            if (_buffer.Count == 0 && _pendingWipes.Count == 0) return;
            var acctCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_ACCOUNT);
            var storCf = _manager.GetColumnFamily(RocksDbManager.CF_STATE_TRIE_STORAGE);
            (bool IsStorage, byte[] Key, byte[] Blob)? sample = null;
            using (var batch = _manager.CreateWriteBatch())
            {
                foreach (var owner in _pendingWipes)
                {
                    var upper = PrefixUpperBound(owner);
                    if (upper == null) { _store.DeleteRange(owner); continue; }
                    batch.DeleteRange(owner, (ulong)owner.Length, upper, (ulong)upper.Length, storCf);
                }
                foreach (var entry in _buffer.Values)
                {
                    batch.Put(entry.Key, entry.Blob, entry.IsStorage ? storCf : acctCf);
                    if (++_writesSinceSample % ReadBackSampleInterval == 0) sample = entry;
                }
                _manager.Write(batch);
            }
            _buffer.Clear();
            _pendingWipes.Clear();
            _pendingWipeOwners.Clear();
            _store.ClearCache();

            if (sample is { } s)
            {
                var readBack = _manager.Get(
                    s.IsStorage ? RocksDbManager.CF_STATE_TRIE_STORAGE : RocksDbManager.CF_STATE_TRIE_ACCOUNT,
                    s.Key);
                if (readBack == null || !ByteUtil.AreEqual(readBack, s.Blob))
                    throw new InvalidOperationException(
                        "Path-keyed heal read-back sample mismatch: a just-flushed node is absent or altered at its location — keying or column-family wiring is broken.");
            }
        }

        private byte[] ReadCurrent(bool isStorage, byte[] accountHash, byte[] nibblePath)
        {
            var key = MakeKey(isStorage, accountHash, nibblePath);
            if (_buffer.TryGetValue(BufferKey(isStorage, key), out var buffered))
                return buffered.Blob;
            if (isStorage && _pendingWipeOwners.Contains(accountHash.ToHex()))
                return null;
            return _store.TryGetRaw(isStorage ? accountHash : Array.Empty<byte>(), nibblePath);
        }

        private static byte[] PrefixUpperBound(byte[] prefix)
        {
            var upper = (byte[])prefix.Clone();
            for (int i = upper.Length - 1; i >= 0; i--)
            {
                if (upper[i] != 0xFF) { upper[i]++; return upper; }
                upper[i] = 0x00;
            }
            return null;
        }

        private static byte[] MakeKey(bool isStorage, byte[] accountHash, byte[] nibblePath)
            => isStorage
                ? RocksDbPathTrieNodeStore.StorageKey(accountHash, nibblePath)
                : nibblePath ?? Array.Empty<byte>();

        private static string BufferKey(bool isStorage, byte[] key)
            => (isStorage ? "s:" : "a:") + key.ToHex();
    }
}
