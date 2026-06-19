using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Serialization;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public class RocksDbStateDiffStore : IStateDiffStore
    {
        private const byte TYPE_ACCOUNT = 0x01;
        private const byte TYPE_STORAGE = 0x02;
        // Yellow Paper §4.1: address keys are keccak256(address) = 32 bytes.
        private const int AddressKeyLength = 32;
        private const int InlineAddressLength = 20;
        private const int AccountDataKeyLength = AddressKeyLength + 8;
        private const int AccountIndexKeyLength = 8 + 1 + AddressKeyLength;
        private const int StorageDataKeyLength = AddressKeyLength + 32 + 8;
        private const int StorageIndexKeyLength = 8 + 1 + AddressKeyLength + 32;
        private static readonly byte[] SENTINEL_NOT_EXIST = new byte[] { 0x00 };
        private static readonly byte[] EMPTY_VALUE = Array.Empty<byte>();
        private static readonly byte[] META_KEY_OLDEST = System.Text.Encoding.UTF8.GetBytes("oldest_block");
        private static readonly byte[] META_KEY_NEWEST = System.Text.Encoding.UTF8.GetBytes("newest_block");

        private readonly RocksDbManager _manager;
        private readonly RocksDbSerializer _serializer;
        private readonly IAccountLayoutStrategy _accountLayout;

        public RocksDbStateDiffStore(
            RocksDbManager manager,
            RocksDbSerializer serializer = null,
            IAccountLayoutStrategy accountLayout = null)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _serializer = serializer ?? RocksDbSerializer.Default;
            _accountLayout = accountLayout ?? RlpAccountLayout.Instance;
        }

        public Task SaveBlockDiffAsync(BlockStateDiff diff)
        {
            var blockBytes = BlockNumberToBytes(diff.BlockNumber);
            using var batch = _manager.CreateWriteBatch();
            var cfAccounts = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_ACCOUNTS);
            var cfStorage = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_STORAGE);
            var cfIndex = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX);
            var cfMeta = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_META);

            // Delete any prior entries for this block before writing the new
            // ones. Re-execution of the same block (after a kill-mid-block or
            // an auto-rewind cycle) can produce a DIFFERENT touched-set; any
            // surviving entries from the prior partial run would silently
            // corrupt future rewinds. The delete + write happen in the same
            // WriteBatch so the swap is atomic.
            var seekKey = new byte[9];
            Buffer.BlockCopy(blockBytes, 0, seekKey, 0, 8);
            seekKey[8] = 0x00;
            using (var it = _manager.CreateIterator(RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX))
            {
                it.Seek(seekKey);
                while (it.Valid())
                {
                    var indexKey = it.Key();
                    if (indexKey.Length < 9) { it.Next(); continue; }
                    bool samePrefix = true;
                    for (int i = 0; i < 8; i++)
                    {
                        if (indexKey[i] != blockBytes[i]) { samePrefix = false; break; }
                    }
                    if (!samePrefix) break;

                    var type = indexKey[8];
                    if (type == TYPE_ACCOUNT && indexKey.Length == AccountIndexKeyLength)
                    {
                        var addr = new byte[AddressKeyLength];
                        Buffer.BlockCopy(indexKey, 9, addr, 0, AddressKeyLength);
                        batch.Delete(BuildAccountDataKey(addr, blockBytes), cfAccounts);
                    }
                    else if (type == TYPE_STORAGE && indexKey.Length == StorageIndexKeyLength)
                    {
                        var addr = new byte[AddressKeyLength];
                        Buffer.BlockCopy(indexKey, 9, addr, 0, AddressKeyLength);
                        var slot = new byte[32];
                        Buffer.BlockCopy(indexKey, 9 + AddressKeyLength, slot, 0, 32);
                        batch.Delete(BuildStorageDataKey(addr, slot, blockBytes), cfStorage);
                    }
                    batch.Delete((byte[])indexKey.Clone(), cfIndex);
                    it.Next();
                }
            }

            foreach (var entry in diff.AccountDiffs)
            {
                var keccakKey = StateKeys.AccountKey(entry.Address);
                var inlineAddress = OriginalAddressBytes(entry.Address);
                var dataKey = BuildAccountDataKey(keccakKey, blockBytes);
                byte[] body = entry.PreValue != null ? _accountLayout.EncodeAccount(entry.PreValue) : SENTINEL_NOT_EXIST;
                var value = ConcatInline(inlineAddress, body);
                batch.Put(dataKey, value, cfAccounts);

                var indexKey = BuildAccountIndexKey(blockBytes, keccakKey);
                batch.Put(indexKey, EMPTY_VALUE, cfIndex);
            }

            foreach (var entry in diff.StorageDiffs)
            {
                var keccakKey = StateKeys.AccountKey(entry.Address);
                var inlineAddress = OriginalAddressBytes(entry.Address);
                // SlotKey is already keccak(slot) — Yellow Paper §4.1 storage path.
                var slotBytes = entry.SlotKey ?? throw new System.ArgumentException("StorageDiffEntry.SlotKey is required (keccak(slot), 32 bytes)");
                if (slotBytes.Length != 32)
                    throw new System.ArgumentException($"StorageDiffEntry.SlotKey must be 32 bytes (keccak(slot)), got {slotBytes.Length}");
                var dataKey = BuildStorageDataKey(keccakKey, slotBytes, blockBytes);
                byte[] body = entry.PreValue ?? EMPTY_VALUE;
                var value = ConcatInline(inlineAddress, body);
                batch.Put(dataKey, value, cfStorage);

                var indexKey = BuildStorageIndexKey(blockBytes, keccakKey, slotBytes);
                batch.Put(indexKey, EMPTY_VALUE, cfIndex);
            }

            UpdateMetaBounds(batch, cfMeta, diff.BlockNumber);
            _manager.Write(batch);
            return Task.CompletedTask;
        }

        public Task<(bool Found, Account PreValue)> GetFirstAccountPreValueAfterBlockAsync(string address, BigInteger blockNumber)
        {
            var addressBytes = StateKeys.AccountKey(address);
            var searchFromBlock = blockNumber + 1;
            var seekKey = BuildAccountDataKey(addressBytes, BlockNumberToBytes(searchFromBlock));

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_HISTORY_ACCOUNTS);
            iterator.Seek(seekKey);

            if (iterator.Valid())
            {
                var key = iterator.Key();
                if (key.Length == AccountDataKeyLength && KeyStartsWithAddress(key, addressBytes))
                {
                    var value = iterator.Value();
                    var body = StripInlineAddress(value);
                    if (body.Length == 1 && body[0] == 0x00)
                        return Task.FromResult((true, (Account)null));

                    var account = _accountLayout.DecodeAccount(body);
                    return Task.FromResult((true, account));
                }
            }

            return Task.FromResult((false, (Account)null));
        }

        public Task<(bool Found, byte[] PreValue)> GetFirstStoragePreValueAfterBlockAsync(string address, BigInteger slot, BigInteger blockNumber)
        {
            var addressBytes = StateKeys.AccountKey(address);
            var slotBytes = SlotToBytes(slot);
            var searchFromBlock = blockNumber + 1;
            var seekKey = BuildStorageDataKey(addressBytes, slotBytes, BlockNumberToBytes(searchFromBlock));

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_HISTORY_STORAGE);
            iterator.Seek(seekKey);

            if (iterator.Valid())
            {
                var key = iterator.Key();
                if (key.Length == StorageDataKeyLength && KeyStartsWithAddressAndSlot(key, addressBytes, slotBytes))
                {
                    var value = iterator.Value();
                    var body = StripInlineAddress(value);
                    if (body.Length == 0)
                        return Task.FromResult((true, (byte[])null));

                    return Task.FromResult((true, body));
                }
            }

            return Task.FromResult((false, (byte[])null));
        }

        public Task<BlockStateDiff> GetBlockDiffAsync(BigInteger blockNumber)
        {
            var blockBytes = BlockNumberToBytes(blockNumber);
            // Index keys are (blockNum8 || type || address [|| slot]). Seek to
            // blockNum8 || 0x00 and walk forward while the prefix matches —
            // 0x00 is below TYPE_ACCOUNT (0x01) and TYPE_STORAGE (0x02) so the
            // seek lands at the first entry for this block (or the next block
            // if none exist).
            var seekKey = new byte[9];
            Buffer.BlockCopy(blockBytes, 0, seekKey, 0, 8);
            seekKey[8] = 0x00;

            var diff = new BlockStateDiff { BlockNumber = blockNumber };
            bool found = false;

            using var it = _manager.CreateIterator(RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX);
            it.Seek(seekKey);
            while (it.Valid())
            {
                var indexKey = it.Key();
                if (indexKey.Length < 9) { it.Next(); continue; }
                // Block-number prefix exhausted — stop before bleeding into
                // the next block's index entries.
                bool samePrefix = true;
                for (int i = 0; i < 8; i++)
                {
                    if (indexKey[i] != blockBytes[i]) { samePrefix = false; break; }
                }
                if (!samePrefix) break;

                found = true;
                var type = indexKey[8];
                if (type == TYPE_ACCOUNT && indexKey.Length == AccountIndexKeyLength)
                {
                    var addr = new byte[AddressKeyLength];
                    Buffer.BlockCopy(indexKey, 9, addr, 0, AddressKeyLength);
                    var dataKey = BuildAccountDataKey(addr, blockBytes);
                    var raw = _manager.Get(RocksDbManager.CF_STATE_HISTORY_ACCOUNTS, dataKey);
                    Account pre;
                    byte[] inlineAddress;
                    if (raw == null)
                    {
                        pre = null;
                        inlineAddress = null;
                    }
                    else
                    {
                        inlineAddress = ReadInlineAddress(raw);
                        var body = StripInlineAddress(raw);
                        if (body.Length == 1 && body[0] == SENTINEL_NOT_EXIST[0])
                            pre = null;
                        else
                            pre = _accountLayout.DecodeAccount(body);
                    }
                    diff.AccountDiffs.Add(new AccountDiffEntry
                    {
                        Address = inlineAddress != null ? "0x" + inlineAddress.ToHex() : null,
                        PreValue = pre
                    });
                }
                else if (type == TYPE_STORAGE && indexKey.Length == StorageIndexKeyLength)
                {
                    var addr = new byte[AddressKeyLength];
                    Buffer.BlockCopy(indexKey, 9, addr, 0, AddressKeyLength);
                    var slot = new byte[32];
                    Buffer.BlockCopy(indexKey, 9 + AddressKeyLength, slot, 0, 32);
                    var dataKey = BuildStorageDataKey(addr, slot, blockBytes);
                    var raw = _manager.Get(RocksDbManager.CF_STATE_HISTORY_STORAGE, dataKey);
                    byte[] inlineAddress;
                    byte[] body;
                    if (raw == null)
                    {
                        inlineAddress = null;
                        body = EMPTY_VALUE;
                    }
                    else
                    {
                        inlineAddress = ReadInlineAddress(raw);
                        body = StripInlineAddress(raw);
                    }
                    diff.StorageDiffs.Add(new StorageDiffEntry
                    {
                        Address = inlineAddress != null ? "0x" + inlineAddress.ToHex() : null,
                        // Slot bytes are keccak(slot) — Yellow Paper §4.1 storage path.
                        SlotKey = slot,
                        PreValue = body
                    });
                }
                it.Next();
            }

            return Task.FromResult(found ? diff : null);
        }

        public Task DeleteBlockDiffAsync(BigInteger blockNumber)
        {
            var blockBytes = BlockNumberToBytes(blockNumber);
            using var batch = _manager.CreateWriteBatch();
            var cfAccounts = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_ACCOUNTS);
            var cfStorage = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_STORAGE);
            var cfIndex = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX);

            var seekKey = new byte[9];
            Buffer.BlockCopy(blockBytes, 0, seekKey, 0, 8);
            seekKey[8] = 0x00;

            using var it = _manager.CreateIterator(RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX);
            it.Seek(seekKey);
            while (it.Valid())
            {
                var indexKey = it.Key();
                if (indexKey.Length < 9) { it.Next(); continue; }
                bool samePrefix = true;
                for (int i = 0; i < 8; i++)
                {
                    if (indexKey[i] != blockBytes[i]) { samePrefix = false; break; }
                }
                if (!samePrefix) break;

                var type = indexKey[8];
                if (type == TYPE_ACCOUNT && indexKey.Length == AccountIndexKeyLength)
                {
                    var addr = new byte[AddressKeyLength];
                    Buffer.BlockCopy(indexKey, 9, addr, 0, AddressKeyLength);
                    batch.Delete(BuildAccountDataKey(addr, blockBytes), cfAccounts);
                }
                else if (type == TYPE_STORAGE && indexKey.Length == StorageIndexKeyLength)
                {
                    var addr = new byte[AddressKeyLength];
                    Buffer.BlockCopy(indexKey, 9, addr, 0, AddressKeyLength);
                    var slot = new byte[32];
                    Buffer.BlockCopy(indexKey, 9 + AddressKeyLength, slot, 0, 32);
                    batch.Delete(BuildStorageDataKey(addr, slot, blockBytes), cfStorage);
                }
                batch.Delete((byte[])indexKey.Clone(), cfIndex);
                it.Next();
            }

            _manager.Write(batch);
            return Task.CompletedTask;
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

        private static byte[] BuildAccountDataKey(byte[] addressKey, byte[] blockNum8)
        {
            var key = new byte[AccountDataKeyLength];
            Buffer.BlockCopy(addressKey, 0, key, 0, AddressKeyLength);
            Buffer.BlockCopy(blockNum8, 0, key, AddressKeyLength, 8);
            return key;
        }

        private static byte[] BuildStorageDataKey(byte[] addressKey, byte[] slot32, byte[] blockNum8)
        {
            var key = new byte[StorageDataKeyLength];
            Buffer.BlockCopy(addressKey, 0, key, 0, AddressKeyLength);
            Buffer.BlockCopy(slot32, 0, key, AddressKeyLength, 32);
            Buffer.BlockCopy(blockNum8, 0, key, AddressKeyLength + 32, 8);
            return key;
        }

        private static byte[] BuildAccountIndexKey(byte[] blockNum8, byte[] addressKey)
        {
            var key = new byte[AccountIndexKeyLength];
            Buffer.BlockCopy(blockNum8, 0, key, 0, 8);
            key[8] = TYPE_ACCOUNT;
            Buffer.BlockCopy(addressKey, 0, key, 9, AddressKeyLength);
            return key;
        }

        private static byte[] BuildStorageIndexKey(byte[] blockNum8, byte[] addressKey, byte[] slot32)
        {
            var key = new byte[StorageIndexKeyLength];
            Buffer.BlockCopy(blockNum8, 0, key, 0, 8);
            key[8] = TYPE_STORAGE;
            Buffer.BlockCopy(addressKey, 0, key, 9, AddressKeyLength);
            Buffer.BlockCopy(slot32, 0, key, 9 + AddressKeyLength, 32);
            return key;
        }

        private static bool KeyStartsWithAddress(byte[] key, byte[] addressKey)
        {
            if (key.Length < AddressKeyLength) return false;
            for (int i = 0; i < AddressKeyLength; i++)
            {
                if (key[i] != addressKey[i]) return false;
            }
            return true;
        }

        private static bool KeyStartsWithAddressAndSlot(byte[] key, byte[] addressKey, byte[] slot32)
        {
            if (key.Length < AddressKeyLength + 32) return false;
            for (int i = 0; i < AddressKeyLength; i++)
            {
                if (key[i] != addressKey[i]) return false;
            }
            for (int i = 0; i < 32; i++)
            {
                if (key[AddressKeyLength + i] != slot32[i]) return false;
            }
            return true;
        }

        private static byte[] ConcatInline(byte[] inlineAddress, byte[] body)
        {
            var value = new byte[InlineAddressLength + body.Length];
            Buffer.BlockCopy(inlineAddress, 0, value, 0, InlineAddressLength);
            if (body.Length > 0)
                Buffer.BlockCopy(body, 0, value, InlineAddressLength, body.Length);
            return value;
        }

        private static byte[] ReadInlineAddress(byte[] value)
        {
            if (value == null || value.Length < InlineAddressLength) return null;
            var addr = new byte[InlineAddressLength];
            Buffer.BlockCopy(value, 0, addr, 0, InlineAddressLength);
            return addr;
        }

        private static byte[] StripInlineAddress(byte[] value)
        {
            if (value == null) return EMPTY_VALUE;
            if (value.Length <= InlineAddressLength) return EMPTY_VALUE;
            var body = new byte[value.Length - InlineAddressLength];
            Buffer.BlockCopy(value, InlineAddressLength, body, 0, body.Length);
            return body;
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

        // Yellow Paper §4.1 storage-trie path. Diff entries are keyed by
        // keccak(slot) to match CF_STATE_STORAGE (R1) and the snap/1 wire
        // shape. The original BigInteger slot is not recoverable from the
        // persistent diff store; consumers see the 32-byte hash via
        // StorageDiffEntry.SlotKey.
        private static byte[] SlotToBytes(BigInteger slot)
        {
            return StateKeys.StorageSlotKey(slot);
        }

        private static byte[] OriginalAddressBytes(string address)
        {
            return AddressUtil.Current.ConvertToValid20ByteAddress(address).HexToByteArray();
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

                    if (type == TYPE_ACCOUNT && indexKey.Length == AccountIndexKeyLength)
                    {
                        var address = new byte[AddressKeyLength];
                        Buffer.BlockCopy(indexKey, 9, address, 0, AddressKeyLength);
                        var dataKey = BuildAccountDataKey(address, blockBytes);
                        keysToDelete.Add((dataKey, cfAccounts));
                    }
                    else if (type == TYPE_STORAGE && indexKey.Length == StorageIndexKeyLength)
                    {
                        var address = new byte[AddressKeyLength];
                        Buffer.BlockCopy(indexKey, 9, address, 0, AddressKeyLength);
                        var slot = new byte[32];
                        Buffer.BlockCopy(indexKey, 9 + AddressKeyLength, slot, 0, 32);
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
