using System;
using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal sealed class FlatInventory
    {
        private static readonly object MissingCodeGate = new object();

        private readonly RocksDbManager _rocks;

        public FlatInventory(RocksDbManager rocks)
        {
            _rocks = rocks;
        }

        private static byte[] DamageInventoryKey => System.Text.Encoding.ASCII.GetBytes("flatrepair:inventory");

        public IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> GetDamage()
        {
            var list = new List<(byte[], byte[])>();
            foreach (var row in ReadFixedWidth(DamageInventoryKey, 64))
            {
                var acct = new byte[32];
                var root = new byte[32];
                Buffer.BlockCopy(row, 0, acct, 0, 32);
                Buffer.BlockCopy(row, 32, root, 0, 32);
                list.Add((acct, root));
            }
            return list;
        }

        public void ClearDamage()
            => _rocks.Delete(RocksDbManager.CF_METADATA, DamageInventoryKey);

        private static byte[] MissingCodeKey => System.Text.Encoding.ASCII.GetBytes("flatrepair:missingcode");

        public void MergeMissingCode(HashSet<byte[]> hashes)
        {
            if (hashes == null || hashes.Count == 0) return;
            lock (MissingCodeGate)
            {
                var merged = new HashSet<byte[]>(GetMissingCode(), ByteArrayComparer.Current);
                var persisted = merged.Count;
                merged.UnionWith(hashes);
                if (merged.Count == persisted) return;
                var blob = new byte[merged.Count * 32];
                int i = 0;
                foreach (var h in merged) { h.CopyTo(blob, i * 32); i++; }
                _rocks.Put(RocksDbManager.CF_METADATA, MissingCodeKey, blob);
            }
        }

        public IReadOnlyList<byte[]> GetMissingCode()
        {
            return ReadFixedWidth(MissingCodeKey, 32);
        }

        private List<byte[]> ReadFixedWidth(byte[] key, int width)
        {
            var blob = _rocks.Get(RocksDbManager.CF_METADATA, key);
            var list = new List<byte[]>();
            if (blob == null || blob.Length % width != 0) return list;
            for (int i = 0; i + width <= blob.Length; i += width)
            {
                var row = new byte[width];
                Buffer.BlockCopy(blob, i, row, 0, width);
                list.Add(row);
            }
            return list;
        }

        public void ClearMissingCode()
            => _rocks.Delete(RocksDbManager.CF_METADATA, MissingCodeKey);
    }
}
