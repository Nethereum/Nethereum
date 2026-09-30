using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.CoreChain.Storage
{
    public sealed class FlatStateCache
    {
        public static readonly byte[] Tombstone = new byte[0];

        /// <summary>Cached "no account at this key" (deleted account) — distinct object identity; a real
        /// EIP-161-empty <see cref="Account"/> (nonce=0, balance=0, empty code/storage roots) is a DIFFERENT
        /// instance and must NOT be confused with this sentinel by callers that only compare by value.</summary>
        public static readonly Account AccountTombstone = new Account();

        private const byte AccountPrefix = 0x00;
        private const byte StoragePrefix = 0x01;
        private const byte CodePrefix = 0x02;

        private const int NodeOverhead = 64;
        private const int ByteArrayOverhead = 24;
        private const int AccountValueCost = 200;

        private readonly object _gate = new object();
        private readonly long _budgetBytes;
        private readonly Dictionary<byte[], LinkedListNode<Entry>> _map;
        private readonly LinkedList<Entry> _lru = new LinkedList<Entry>();
        private long _bytes;
        private long _hits, _misses;

        private readonly struct Entry
        {
            public readonly byte[] Key;
            public readonly object Value;
            public Entry(byte[] key, object value) { Key = key; Value = value; }
        }

        public FlatStateCache(long budgetBytes)
        {
            if (budgetBytes <= 0) throw new ArgumentOutOfRangeException(nameof(budgetBytes));
            _budgetBytes = budgetBytes;
            _map = new Dictionary<byte[], LinkedListNode<Entry>>(KeyComparer.Instance);
        }

        public long ApproxBytes { get { lock (_gate) return _bytes; } }
        public int Count { get { lock (_gate) return _map.Count; } }
        public long Hits { get { lock (_gate) return _hits; } }
        public long Misses { get { lock (_gate) return _misses; } }


        public static byte[] AccountKey(string address) => BuildKey(AccountPrefix, NormalizeAddress(address), null);

        public static byte[] StorageKey(string address, BigInteger slot)
            => BuildKey(StoragePrefix, NormalizeAddress(address), SlotBytes(slot));

        public static byte[] CodeKey(byte[] codeHash)
        {
            if (codeHash == null || codeHash.Length != 32)
                throw new ArgumentException("codeHash must be 32 bytes.", nameof(codeHash));
            var key = new byte[33];
            key[0] = CodePrefix;
            Buffer.BlockCopy(codeHash, 0, key, 1, 32);
            return key;
        }

        private static byte[] NormalizeAddress(string address)
        {
            var hex = AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant();
            return hex.HexToByteArray();
        }

        private static byte[] SlotBytes(BigInteger slot) => slot.ToBytesForRLPEncoding().PadBytes(32);

        private static byte[] BuildKey(byte prefix, byte[] addr20, byte[] slot32)
        {
            var key = new byte[1 + 20 + (slot32?.Length ?? 0)];
            key[0] = prefix;
            Buffer.BlockCopy(addr20, 0, key, 1, 20);
            if (slot32 != null) Buffer.BlockCopy(slot32, 0, key, 21, 32);
            return key;
        }


        public bool TryGetAccount(string address, out Account account)
        {
            if (TryGetRaw(AccountKey(address), out var raw))
            {
                var stored = (Account)raw;
                account = ReferenceEquals(stored, AccountTombstone) ? stored : stored.Clone();
                return true;
            }
            account = null;
            return false;
        }

        public void PutAccount(string address, Account account)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));
            Put(AccountKey(address), account);
        }

        public void PutAccountAbsent(string address) => Put(AccountKey(address), AccountTombstone);


        public bool TryGetStorage(string address, BigInteger slot, out byte[] value)
        {
            if (TryGetRaw(StorageKey(address, slot), out var raw))
            {
                var stored = (byte[])raw;
                value = ReferenceEquals(stored, Tombstone) ? stored : (byte[])stored.Clone();
                return true;
            }
            value = null;
            return false;
        }

        public void PutStorage(string address, BigInteger slot, byte[] value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            Put(StorageKey(address, slot), value);
        }

        public void PutStorageAbsent(string address, BigInteger slot) => Put(StorageKey(address, slot), Tombstone);


        public bool TryGetCode(byte[] codeHash, out byte[] code)
        {
            if (TryGetRaw(CodeKey(codeHash), out var raw))
            {
                code = (byte[])raw;
                return true;
            }
            code = null;
            return false;
        }

        public void PutCode(byte[] codeHash, byte[] code)
        {
            if (code == null) throw new ArgumentNullException(nameof(code));
            Put(CodeKey(codeHash), code);
        }


        public void RemoveOwnerStorage(string address)
        {
            var addr20 = NormalizeAddress(address);
            var prefix = new byte[21];
            prefix[0] = StoragePrefix;
            Buffer.BlockCopy(addr20, 0, prefix, 1, 20);
            lock (_gate)
            {
                List<byte[]> hits = null;
                foreach (var k in _map.Keys)
                    if (StartsWith(k, prefix)) (hits ??= new List<byte[]>()).Add(k);
                if (hits == null) return;
                foreach (var k in hits) RemoveLocked(k);
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _map.Clear();
                _lru.Clear();
                _bytes = 0;
            }
        }


        private bool TryGetRaw(byte[] key, out object value)
        {
            lock (_gate)
            {
                if (_map.TryGetValue(key, out var node))
                {
                    _lru.Remove(node);
                    _lru.AddFirst(node);
                    value = node.Value.Value;
                    _hits++;
                    return true;
                }
                _misses++;
            }
            value = null;
            return false;
        }

        private void Put(byte[] key, object value)
        {
            lock (_gate)
            {
                if (_map.TryGetValue(key, out var existing))
                {
                    _bytes += EntryCost(key, value) - EntryCost(existing.Value.Key, existing.Value.Value);
                    _lru.Remove(existing);
                    var replaced = new LinkedListNode<Entry>(new Entry(key, value));
                    _lru.AddFirst(replaced);
                    _map[key] = replaced;
                }
                else
                {
                    var node = new LinkedListNode<Entry>(new Entry(key, value));
                    _lru.AddFirst(node);
                    _map[key] = node;
                    _bytes += EntryCost(key, value);
                }
                EvictWhileOverBudget();
            }
        }

        private void RemoveLocked(byte[] key)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _bytes -= EntryCost(node.Value.Key, node.Value.Value);
                _lru.Remove(node);
                _map.Remove(key);
            }
        }

        private void EvictWhileOverBudget()
        {
            while (_bytes > _budgetBytes && _lru.Last != null)
            {
                var tail = _lru.Last;
                _bytes -= EntryCost(tail.Value.Key, tail.Value.Value);
                _lru.RemoveLast();
                _map.Remove(tail.Value.Key);
            }
        }

        private static long EntryCost(byte[] key, object value)
        {
            if (key[0] == AccountPrefix) return key.Length + AccountValueCost + NodeOverhead;
            var bytes = (byte[])value;
            return key.Length + bytes.Length + ByteArrayOverhead + NodeOverhead;
        }

        private static bool StartsWith(byte[] value, byte[] prefix)
        {
            if (value.Length < prefix.Length) return false;
            for (int i = 0; i < prefix.Length; i++)
                if (value[i] != prefix[i]) return false;
            return true;
        }

        private sealed class KeyComparer : IEqualityComparer<byte[]>
        {
            public static readonly KeyComparer Instance = new KeyComparer();

            public bool Equals(byte[] a, byte[] b)
            {
                if (ReferenceEquals(a, b)) return true;
                if (a == null || b == null || a.Length != b.Length) return false;
                for (int i = 0; i < a.Length; i++)
                    if (a[i] != b[i]) return false;
                return true;
            }

            public int GetHashCode(byte[] a)
            {
                if (a == null) return 0;
                unchecked
                {
                    int h = (int)2166136261;
                    for (int i = 0; i < a.Length; i++) h = (h ^ a[i]) * 16777619;
                    return h;
                }
            }
        }
    }
}
