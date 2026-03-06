using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Serialization;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public class RocksDbStateDiffStore : IStateDiffStore
    {
        private const byte TYPE_ACCOUNT = 0x01;
        private const byte TYPE_STORAGE = 0x02;
        private static readonly byte[] SENTINEL_NOT_EXIST = new byte[] { 0x00 };
        private static readonly byte[] EMPTY_VALUE = Array.Empty<byte>();
        private static readonly byte[] META_KEY_OLDEST = System.Text.Encoding.UTF8.GetBytes("oldest_block");
        private static readonly byte[] META_KEY_NEWEST = System.Text.Encoding.UTF8.GetBytes("newest_block");

        private readonly RocksDbManager _manager;

        public RocksDbStateDiffStore(RocksDbManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public Task SaveBlockDiffAsync(BlockStateDiff diff)
        {
            var blockBytes = BlockNumberToBytes(diff.BlockNumber);
            using var batch = _manager.CreateWriteBatch();
            var cfAccounts = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_ACCOUNTS);
            var cfStorage = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_STORAGE);
            var cfIndex = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX);
            var cfMeta = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_META);

            foreach (var entry in diff.AccountDiffs)
            {
                var addressBytes = NormalizeAddressToBytes(entry.Address);
                var dataKey = BuildAccountDataKey(addressBytes, blockBytes);
                var value = entry.PreValue != null ? RocksDbSerializer.SerializeAccount(entry.PreValue) : SENTINEL_NOT_EXIST;
                batch.Put(dataKey, value, cfAccounts);

                var indexKey = BuildAccountIndexKey(blockBytes, addressBytes);
                batch.Put(indexKey, EMPTY_VALUE, cfIndex);
            }

            foreach (var entry in diff.StorageDiffs)
            {
                var addressBytes = NormalizeAddressToBytes(entry.Address);
                var slotBytes = SlotToBytes(entry.Slot);
                var dataKey = BuildStorageDataKey(addressBytes, slotBytes, blockBytes);
                var value = entry.PreValue ?? EMPTY_VALUE;
                batch.Put(dataKey, value, cfStorage);

                var indexKey = BuildStorageIndexKey(blockBytes, addressBytes, slotBytes);
                batch.Put(indexKey, EMPTY_VALUE, cfIndex);
            }

            UpdateMetaBounds(batch, cfMeta, diff.BlockNumber);
            _manager.Write(batch);
            return Task.CompletedTask;
        }

        public Task<(bool Found, Account PreValue)> GetFirstAccountPreValueAfterBlockAsync(string address, BigInteger blockNumber)
        {
            var addressBytes = NormalizeAddressToBytes(address);
            var searchFromBlock = blockNumber + 1;
            var seekKey = BuildAccountDataKey(addressBytes, BlockNumberToBytes(searchFromBlock));

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_HISTORY_ACCOUNTS);
            iterator.Seek(seekKey);

            if (iterator.Valid())
            {
                var key = iterator.Key();
                if (key.Length == 28 && KeyStartsWithAddress(key, addressBytes))
                {
                    var value = iterator.Value();
                    if (value.Length == 1 && value[0] == 0x00)
                        return Task.FromResult((true, (Account)null));

                    var account = RocksDbSerializer.DeserializeAccount(value);
                    return Task.FromResult((true, account));
                }
            }

            return Task.FromResult((false, (Account)null));
        }

        public Task<(bool Found, byte[] PreValue)> GetFirstStoragePreValueAfterBlockAsync(string address, BigInteger slot, BigInteger blockNumber)
        {
            var addressBytes = NormalizeAddressToBytes(address);
            var slotBytes = SlotToBytes(slot);
            var searchFromBlock = blockNumber + 1;
            var seekKey = BuildStorageDataKey(addressBytes, slotBytes, BlockNumberToBytes(searchFromBlock));

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_HISTORY_STORAGE);
            iterator.Seek(seekKey);

            if (iterator.Valid())
            {
                var key = iterator.Key();
                if (key.Length == 60 && KeyStartsWithAddressAndSlot(key, addressBytes, slotBytes))
                {
                    var value = iterator.Value();
                    if (value.Length == 0)
                        return Task.FromResult((true, (byte[])null));

                    return Task.FromResult((true, value));
                }
            }

            return Task.FromResult((false, (byte[])null));
        }

        public Task DeleteDiffsAboveBlockAsync(BigInteger blockNumber)
        {
            var startBlockBytes = BlockNumberToBytes(blockNumber + 1);
            var startKey = new byte[startBlockBytes.Length + 1];
            Buffer.BlockCopy(startBlockBytes, 0, startKey, 0, startBlockBytes.Length);
            startKey[startBlockBytes.Length] = 0x00;

            DeleteDiffsViaIndex(startKey, forward: true);
            UpdateMetaAfterDelete();
            return Task.CompletedTask;
        }

        public Task DeleteDiffsBelowBlockAsync(BigInteger blockNumber)
        {
            var endBlockBytes = BlockNumberToBytes(blockNumber);
            var endKey = new byte[endBlockBytes.Length + 1];
            Buffer.BlockCopy(endBlockBytes, 0, endKey, 0, endBlockBytes.Length);
            endKey[endBlockBytes.Length] = 0x00;

            DeleteDiffsViaIndex(null, forward: true, endKey: endKey);
            UpdateMetaAfterDelete();
            return Task.CompletedTask;
        }

        public Task<BigInteger?> GetOldestDiffBlockAsync()
        {
            var value = _manager.Get(RocksDbManager.CF_STATE_HISTORY_META, META_KEY_OLDEST);
            if (value == null) return Task.FromResult<BigInteger?>(null);
            return Task.FromResult<BigInteger?>(BytesToBlockNumber(value));
        }

        public Task<BigInteger?> GetNewestDiffBlockAsync()
        {
            var value = _manager.Get(RocksDbManager.CF_STATE_HISTORY_META, META_KEY_NEWEST);
            if (value == null) return Task.FromResult<BigInteger?>(null);
            return Task.FromResult<BigInteger?>(BytesToBlockNumber(value));
        }

        #region Key Building

        private static byte[] BuildAccountDataKey(byte[] address20, byte[] blockNum8)
        {
            var key = new byte[28];
            Buffer.BlockCopy(address20, 0, key, 0, 20);
            Buffer.BlockCopy(blockNum8, 0, key, 20, 8);
            return key;
        }

        private static byte[] BuildStorageDataKey(byte[] address20, byte[] slot32, byte[] blockNum8)
        {
            var key = new byte[60];
            Buffer.BlockCopy(address20, 0, key, 0, 20);
            Buffer.BlockCopy(slot32, 0, key, 20, 32);
            Buffer.BlockCopy(blockNum8, 0, key, 52, 8);
            return key;
        }

        private static byte[] BuildAccountIndexKey(byte[] blockNum8, byte[] address20)
        {
            var key = new byte[29];
            Buffer.BlockCopy(blockNum8, 0, key, 0, 8);
            key[8] = TYPE_ACCOUNT;
            Buffer.BlockCopy(address20, 0, key, 9, 20);
            return key;
        }

        private static byte[] BuildStorageIndexKey(byte[] blockNum8, byte[] address20, byte[] slot32)
        {
            var key = new byte[61];
            Buffer.BlockCopy(blockNum8, 0, key, 0, 8);
            key[8] = TYPE_STORAGE;
            Buffer.BlockCopy(address20, 0, key, 9, 20);
            Buffer.BlockCopy(slot32, 0, key, 29, 32);
            return key;
        }

        private static bool KeyStartsWithAddress(byte[] key, byte[] address20)
        {
            if (key.Length < 20) return false;
            for (int i = 0; i < 20; i++)
            {
                if (key[i] != address20[i]) return false;
            }
            return true;
        }

        private static bool KeyStartsWithAddressAndSlot(byte[] key, byte[] address20, byte[] slot32)
        {
            if (key.Length < 52) return false;
            for (int i = 0; i < 20; i++)
            {
                if (key[i] != address20[i]) return false;
            }
            for (int i = 0; i < 32; i++)
            {
                if (key[20 + i] != slot32[i]) return false;
            }
            return true;
        }

        #endregion

        #region Serialization Helpers

        private static byte[] BlockNumberToBytes(BigInteger blockNumber)
        {
            var bytes = blockNumber.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (bytes.Length >= 8) return bytes;
            var padded = new byte[8];
            Buffer.BlockCopy(bytes, 0, padded, 8 - bytes.Length, bytes.Length);
            return padded;
        }

        private static BigInteger BytesToBlockNumber(byte[] bytes)
        {
            return new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        }

        private static byte[] SlotToBytes(BigInteger slot)
        {
            var bytes = slot.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (bytes.Length >= 32) return bytes;
            var padded = new byte[32];
            Buffer.BlockCopy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
            return padded;
        }

        private static byte[] NormalizeAddressToBytes(string address)
        {
            var normalized = AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant();
            if (normalized.StartsWith("0x"))
                normalized = normalized.Substring(2);
            var bytes = new byte[20];
            for (int i = 0; i < 20; i++)
            {
                bytes[i] = Convert.ToByte(normalized.Substring(i * 2, 2), 16);
            }
            return bytes;
        }

        #endregion

        #region Index-Based Cleanup

        private void DeleteDiffsViaIndex(byte[] seekKey, bool forward, byte[] endKey = null)
        {
            var cfAccounts = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_ACCOUNTS);
            var cfStorage = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_STORAGE);
            var cfIndex = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX);

            var keysToDelete = new List<(byte[] Key, ColumnFamilyHandle Cf)>();
            var indexKeysToDelete = new List<byte[]>();

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX);

            if (seekKey != null)
                iterator.Seek(seekKey);
            else
                iterator.SeekToFirst();

            while (iterator.Valid())
            {
                var indexKey = iterator.Key();

                if (endKey != null && CompareBytes(indexKey, endKey) >= 0)
                    break;

                if (indexKey.Length >= 9)
                {
                    var blockBytes = new byte[8];
                    Buffer.BlockCopy(indexKey, 0, blockBytes, 0, 8);
                    var type = indexKey[8];

                    if (type == TYPE_ACCOUNT && indexKey.Length == 29)
                    {
                        var address = new byte[20];
                        Buffer.BlockCopy(indexKey, 9, address, 0, 20);
                        var dataKey = BuildAccountDataKey(address, blockBytes);
                        keysToDelete.Add((dataKey, cfAccounts));
                    }
                    else if (type == TYPE_STORAGE && indexKey.Length == 61)
                    {
                        var address = new byte[20];
                        Buffer.BlockCopy(indexKey, 9, address, 0, 20);
                        var slot = new byte[32];
                        Buffer.BlockCopy(indexKey, 29, slot, 0, 32);
                        var dataKey = BuildStorageDataKey(address, slot, blockBytes);
                        keysToDelete.Add((dataKey, cfStorage));
                    }

                    indexKeysToDelete.Add((byte[])indexKey.Clone());
                }

                iterator.Next();
            }

            if (keysToDelete.Count == 0 && indexKeysToDelete.Count == 0)
                return;

            using var batch = _manager.CreateWriteBatch();
            foreach (var (key, cf) in keysToDelete)
            {
                batch.Delete(key, cf);
            }
            foreach (var key in indexKeysToDelete)
            {
                batch.Delete(key, cfIndex);
            }
            _manager.Write(batch);
        }

        private void UpdateMetaBounds(WriteBatch batch, ColumnFamilyHandle cfMeta, BigInteger blockNumber)
        {
            var blockBytes = BlockNumberToBytes(blockNumber);
            var currentOldest = _manager.Get(RocksDbManager.CF_STATE_HISTORY_META, META_KEY_OLDEST);
            var currentNewest = _manager.Get(RocksDbManager.CF_STATE_HISTORY_META, META_KEY_NEWEST);

            if (currentOldest == null || BytesToBlockNumber(currentOldest) > blockNumber)
                batch.Put(META_KEY_OLDEST, blockBytes, cfMeta);

            if (currentNewest == null || BytesToBlockNumber(currentNewest) < blockNumber)
                batch.Put(META_KEY_NEWEST, blockBytes, cfMeta);
        }

        private void UpdateMetaAfterDelete()
        {
            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX);
            iterator.SeekToFirst();

            BigInteger? oldest = null;
            BigInteger? newest = null;

            if (iterator.Valid())
            {
                var key = iterator.Key();
                if (key.Length >= 8)
                {
                    var blockBytes = new byte[8];
                    Buffer.BlockCopy(key, 0, blockBytes, 0, 8);
                    oldest = BytesToBlockNumber(blockBytes);
                }
            }

            iterator.SeekToLast();
            if (iterator.Valid())
            {
                var key = iterator.Key();
                if (key.Length >= 8)
                {
                    var blockBytes = new byte[8];
                    Buffer.BlockCopy(key, 0, blockBytes, 0, 8);
                    newest = BytesToBlockNumber(blockBytes);
                }
            }

            using var batch = _manager.CreateWriteBatch();
            var cfMeta = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_META);

            if (oldest.HasValue)
                batch.Put(META_KEY_OLDEST, BlockNumberToBytes(oldest.Value), cfMeta);
            else
                batch.Delete(META_KEY_OLDEST, cfMeta);

            if (newest.HasValue)
                batch.Put(META_KEY_NEWEST, BlockNumberToBytes(newest.Value), cfMeta);
            else
                batch.Delete(META_KEY_NEWEST, cfMeta);

            _manager.Write(batch);
        }

        private static int CompareBytes(byte[] a, byte[] b)
        {
            var minLen = Math.Min(a.Length, b.Length);
            for (int i = 0; i < minLen; i++)
            {
                if (a[i] < b[i]) return -1;
                if (a[i] > b[i]) return 1;
            }
            return a.Length.CompareTo(b.Length);
        }

        #endregion
    }
}
