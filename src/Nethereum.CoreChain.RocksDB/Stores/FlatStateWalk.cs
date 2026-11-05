using System;
using System.Threading;
using Nethereum.Model;
using Nethereum.Util;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal static class FlatStateWalk
    {
        internal const int ShardCount = 16;
        internal const int MaxMissingPathsPerStore = 1_024;
        internal static readonly byte[] ZeroSlot = new byte[32];

        internal static (byte[] Start, byte[] End) ShardBounds(int shard) => NibbleBounds(new[] { (byte)shard });

        internal static (byte[] Start, byte[] End) NibbleBounds(byte[] nibbles)
        {
            var start = new byte[32];
            for (int i = 0; i < nibbles.Length; i++)
                start[i / 2] |= (byte)(i % 2 == 0 ? nibbles[i] << 4 : nibbles[i]);
            var next = (byte[])nibbles.Clone();
            var length = next.Length;
            while (length > 0 && next[length - 1] == 0x0F) length--;
            if (length == 0) return (start, null);
            next[length - 1]++;
            var end = new byte[32];
            for (int i = 0; i < length; i++)
                end[i / 2] |= (byte)(i % 2 == 0 ? next[i] << 4 : next[i]);
            return (start, end);
        }

        internal static long DeleteStorageBelow(
            RocksDbManager rocks, Iterator storage, byte[] bound64, CancellationToken ct, bool countOnly = false)
        {
            long deleted = 0;
            for (; storage.Valid() && (bound64 == null || ByteArrayComparer.Current.Compare(storage.Key(), bound64) < 0); storage.Next())
            {
                if (!countOnly) rocks.Delete(RocksDbManager.CF_STATE_STORAGE, storage.Key());
                if ((++deleted & 0xFFFF) == 0) ct.ThrowIfCancellationRequested();
            }
            return deleted;
        }

        internal static byte[] OwnerSlotKey(byte[] owner32, byte[] slot32)
        {
            var key = new byte[64];
            Buffer.BlockCopy(owner32, 0, key, 0, 32);
            Buffer.BlockCopy(slot32, 0, key, 32, 32);
            return key;
        }

        internal static bool IsStorageOf(Iterator storage, byte[] owner32)
            => storage.Valid() && ByteUtil.StartsWith(storage.Key(), owner32);

        internal static byte[] NextAccountKey(Iterator accounts, byte[] shardEnd)
        {
            for (; accounts.Valid(); accounts.Next())
            {
                var key = accounts.Key();
                if (key.Length != 32) continue;
                return shardEnd != null && ByteArrayComparer.Current.Compare(key, shardEnd) >= 0 ? null : key;
            }
            return null;
        }

        internal static Account DecodeFlatAccount(RocksDbStateStore flat, byte[] accountHash, byte[] flatValue)
        {
            Account account;
            try
            {
                account = flat.DecodeFlatAccountValue(flatValue);
            }
            catch (Exception)
            {
                return null;
            }
            if (account == null) return null;
            if (account.CodeHash == null && flat.HasExternalCodeHashLayout)
                account.CodeHash = flat.GetFlatCodeHash(accountHash);
            account.StateRoot ??= DefaultValues.EMPTY_TRIE_HASH;
            account.CodeHash ??= DefaultValues.EMPTY_DATA_HASH;
            return account;
        }

        internal static bool HasStorage(Account account) => HasStorageRoot(account?.StateRoot);

        internal static bool HasStorageRoot(byte[] storageRoot)
            => storageRoot != null
               && storageRoot.Length == 32
               && !ByteUtil.AreEqual(storageRoot, DefaultValues.EMPTY_TRIE_HASH);
    }
}
