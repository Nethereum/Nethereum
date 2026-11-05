using System;
using System.Collections.Generic;
using System.Threading;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal enum StoredNode { Equal, Missing, Differs }

    internal sealed class RangeHash
    {
        private readonly StackTrie _trie;
        private readonly RocksDbSharp.Iterator _trieRows;
        private readonly byte[] _owner;
        private readonly CancellationToken _ct;
        private long _inserted;

        public RangeHash(byte[] nibbles, RocksDbSharp.Iterator trieRows, byte[] owner, CancellationToken ct)
        {
            _trieRows = trieRows;
            _owner = owner;
            _ct = ct;
            _trie = new StackTrie(nibbles, CompareEmitted);
        }

        public byte[] Reference { get; private set; }
        public byte[] RootHash { get; private set; } = DefaultValues.EMPTY_TRIE_HASH;
        public long Count { get; private set; }
        public bool Malformed { get; private set; }
        public long HoleCount { get; private set; }
        public List<(byte[] Path, StoredNode Stored, byte[] Expected)> Holes { get; } = new();

        public void Add(byte[] key, byte[] value)
        {
            if ((++Count & 0xFFF) == 0) _ct.ThrowIfCancellationRequested();
            if (value == null)
            {
                Malformed = true;
                return;
            }
            _trie.Update(key, value);
            _inserted++;
        }

        public void AddFlatSlot(ReadOnlySpan<byte> key, byte[] value, byte[] scratch)
        {
            if (key.Length != 64)
            {
                Add(null, null);
                return;
            }
            key.Slice(32).CopyTo(scratch);
            Add(scratch, Nethereum.RLP.RLP.EncodeElement(value));
        }

        public RangeHash Seal()
        {
            if (_inserted == 0) return this;
            Reference = _trie.Reference();
            RootHash = _trie.Hash();
            return this;
        }

        private void CompareEmitted(byte[] path, byte[] blob)
        {
            var key = _owner.Length == 0 ? path : ByteUtil.Merge(_owner, path);
            var stored = CompareStoredNode(_trieRows, key, key.Length, blob);
            if (stored == StoredNode.Equal) return;
            HoleCount++;
            if (Holes.Count < FlatStateWalk.MaxMissingPathsPerStore) Holes.Add((path, stored, Sha3Keccack.Current.CalculateHash(blob)));
        }

        private static StoredNode CompareStoredNode(RocksDbSharp.Iterator trieColumn, byte[] key, int keyLength, byte[] blob)
        {
            trieColumn.Seek(key, (ulong)keyLength);
            if (!trieColumn.Valid() || !trieColumn.GetKeySpan().SequenceEqual(key.AsSpan(0, keyLength)))
                return StoredNode.Missing;
            return trieColumn.GetValueSpan().SequenceEqual(blob) ? StoredNode.Equal : StoredNode.Differs;
        }

        internal static RangeHash OfFlatSlots(RocksDbManager rocks, byte[] owner, byte[] nibbles, CancellationToken ct)
        {
            var (start, end) = FlatStateWalk.NibbleBounds(nibbles);
            var bound = end == null ? null : ByteUtil.Merge(owner, end);
            using var slots = rocks.CreateIterator(RocksDbManager.CF_STATE_STORAGE);
            using var trieRows = rocks.CreateIterator(RocksDbManager.CF_STATE_TRIE_STORAGE);
            var hashed = new RangeHash(nibbles, trieRows, owner, ct);
            var slot = new byte[32];
            for (slots.Seek(nibbles.Length == 0 ? owner : ByteUtil.Merge(owner, start)); slots.Valid(); slots.Next())
            {
                var key = slots.Key();
                if (!ByteUtil.StartsWith(key, owner)) break;
                if (bound != null && ByteArrayComparer.Current.Compare(key, bound) >= 0) break;
                hashed.AddFlatSlot(key, slots.Value(), slot);
            }
            return hashed.Seal();
        }
    }
}
