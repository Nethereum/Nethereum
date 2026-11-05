using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Serialization;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.History;
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

        private readonly bool _stateValuesVersioned;

        public RocksDbStateDiffStore(
            RocksDbManager manager,
            RocksDbSerializer serializer = null,
            IAccountLayoutStrategy accountLayout = null,
            bool stateValuesVersioned = false)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _serializer = serializer ?? RocksDbSerializer.Default;
            _accountLayout = accountLayout ?? RlpAccountLayout.Instance;
            _stateValuesVersioned = stateValuesVersioned;
        }

        private byte[] Wrap(byte[] value) => _stateValuesVersioned ? StateValueEnvelope.Encode(value) : value;
        private byte[] Unwrap(byte[] value)
            => (_stateValuesVersioned && value != null) ? StateValueEnvelope.Decode(value, out _).ToArray() : value;

        public Task SaveBlockDiffAsync(BlockStateDiff diff)
        {
            var blockBytes = HistoryKeys.BlockKey((ulong)diff.BlockNumber);
            using var batch = _manager.CreateWriteBatch();
            var cfMeta = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_META);

            StageBlockRowDeletes(batch, blockBytes);
            StageAccountDiffs(batch, diff, blockBytes);
            StageStorageDiffs(batch, diff, blockBytes);

            UpdateMetaBounds(batch, cfMeta, diff.BlockNumber);
            _manager.Write(batch);
            return Task.CompletedTask;
        }

        private void StageAccountDiffs(WriteBatch batch, BlockStateDiff diff, byte[] blockBytes)
        {
            var cfAccounts = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_ACCOUNTS);
            var cfIndex = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX);
            foreach (var entry in diff.AccountDiffs)
            {
                var keccakKey = StateKeys.AccountKey(entry.Address);
                var inlineAddress = OriginalAddressBytes(entry.Address);
                var dataKey = BuildAccountDataKey(keccakKey, blockBytes);
                byte[] body = entry.PreValue != null ? _accountLayout.EncodeAccount(entry.PreValue) : SENTINEL_NOT_EXIST;
                var value = ConcatInline(inlineAddress, body);
                batch.Put(dataKey, Wrap(value), cfAccounts);

                var indexKey = BuildAccountIndexKey(blockBytes, keccakKey);
                batch.Put(indexKey, EMPTY_VALUE, cfIndex);
            }
        }

        private void StageStorageDiffs(WriteBatch batch, BlockStateDiff diff, byte[] blockBytes)
        {
            var cfStorage = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_STORAGE);
            var cfIndex = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX);
            foreach (var entry in diff.StorageDiffs)
            {
                var keccakKey = StateKeys.AccountKey(entry.Address);
                var inlineAddress = OriginalAddressBytes(entry.Address);
                var slotBytes = entry.SlotKey ?? throw new System.ArgumentException("StorageDiffEntry.SlotKey is required (keccak(slot), 32 bytes)");
                if (slotBytes.Length != 32)
                    throw new System.ArgumentException($"StorageDiffEntry.SlotKey must be 32 bytes (keccak(slot)), got {slotBytes.Length}");
                var dataKey = BuildStorageDataKey(keccakKey, slotBytes, blockBytes);
                byte[] body = entry.PreValue ?? EMPTY_VALUE;
                var value = ConcatInline(inlineAddress, body);
                batch.Put(dataKey, Wrap(value), cfStorage);

                var indexKey = BuildStorageIndexKey(blockBytes, keccakKey, slotBytes);
                batch.Put(indexKey, EMPTY_VALUE, cfIndex);
            }
        }

        private void StageBlockRowDeletes(WriteBatch batch, byte[] blockBytes)
        {
            var cfIndex = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX);
            foreach (var indexKey in IndexKeysOfBlock(blockBytes))
                StageIndexRowDelete(batch, cfIndex, indexKey, blockBytes);
        }

        private void StageIndexRowDelete(WriteBatch batch, ColumnFamilyHandle cfIndex, byte[] indexKey, byte[] blockBytes)
        {
            var row = DataRowOf(indexKey, blockBytes);
            if (row.HasValue) batch.Delete(row.Value.DataKey, _manager.GetColumnFamily(row.Value.Cf));
            batch.Delete(indexKey, cfIndex);
        }

        public Task<(bool Found, Account PreValue)> GetFirstAccountPreValueAfterBlockAsync(string address, BigInteger blockNumber)
        {
            var addressBytes = StateKeys.AccountKey(address);
            var searchFromBlock = blockNumber + 1;
            var seekKey = BuildAccountDataKey(addressBytes, HistoryKeys.BlockKey((ulong)searchFromBlock));

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_HISTORY_ACCOUNTS);
            iterator.Seek(seekKey);

            if (iterator.Valid())
            {
                var key = iterator.Key();
                if (key.Length == AccountDataKeyLength && ByteUtil.StartsWith(key, addressBytes))
                {
                    var value = Unwrap(iterator.Value());
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
            var seekKey = BuildStorageDataKey(addressBytes, slotBytes, HistoryKeys.BlockKey((ulong)searchFromBlock));

            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_HISTORY_STORAGE);
            iterator.Seek(seekKey);

            if (iterator.Valid())
            {
                var key = iterator.Key();
                if (key.Length == StorageDataKeyLength && ByteUtil.StartsWith(key, ByteUtil.Merge(addressBytes, slotBytes)))
                {
                    var value = Unwrap(iterator.Value());
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
            var blockBytes = HistoryKeys.BlockKey((ulong)blockNumber);
            var diff = new BlockStateDiff { BlockNumber = blockNumber };
            var indexKeys = IndexKeysOfBlock(blockBytes);

            foreach (var indexKey in indexKeys)
            {
                var type = indexKey[8];
                if (type == TYPE_ACCOUNT && indexKey.Length == AccountIndexKeyLength)
                    diff.AccountDiffs.Add(ReadAccountDiff(AddressOf(indexKey), blockBytes));
                else if (type == TYPE_STORAGE && indexKey.Length == StorageIndexKeyLength)
                    diff.StorageDiffs.Add(ReadStorageDiff(AddressOf(indexKey), SlotOf(indexKey), blockBytes));
            }

            return Task.FromResult(indexKeys.Count > 0 ? diff : null);
        }

        private AccountDiffEntry ReadAccountDiff(byte[] addr, byte[] blockBytes)
        {
            var dataKey = BuildAccountDataKey(addr, blockBytes);
            var raw = Unwrap(_manager.Get(RocksDbManager.CF_STATE_HISTORY_ACCOUNTS, dataKey));
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
            return new AccountDiffEntry
            {
                Address = inlineAddress != null ? "0x" + inlineAddress.ToHex() : null,
                PreValue = pre
            };
        }

        private StorageDiffEntry ReadStorageDiff(byte[] addr, byte[] slot, byte[] blockBytes)
        {
            var dataKey = BuildStorageDataKey(addr, slot, blockBytes);
            var raw = Unwrap(_manager.Get(RocksDbManager.CF_STATE_HISTORY_STORAGE, dataKey));
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
            return new StorageDiffEntry
            {
                Address = inlineAddress != null ? "0x" + inlineAddress.ToHex() : null,
                SlotKey = slot,
                PreValue = body
            };
        }

        public Task DeleteBlockDiffAsync(BigInteger blockNumber)
        {
            var blockBytes = HistoryKeys.BlockKey((ulong)blockNumber);
            using var batch = _manager.CreateWriteBatch();
            StageBlockRowDeletes(batch, blockBytes);
            _manager.Write(batch);
            return Task.CompletedTask;
        }

        public Task DeleteDiffsAboveBlockAsync(BigInteger blockNumber)
        {
            var startKey = IndexSeekKey(HistoryKeys.BlockKey((ulong)(blockNumber + 1)));

            DeleteDiffsViaIndex(startKey, forward: true);
            UpdateMetaAfterDelete();
            return Task.CompletedTask;
        }

        public Task DeleteDiffsBelowBlockAsync(BigInteger blockNumber)
        {
            var endKey = IndexSeekKey(HistoryKeys.BlockKey((ulong)blockNumber));

            DeleteDiffsViaIndex(null, forward: true, endKey: endKey);
            UpdateMetaAfterDelete();
            return Task.CompletedTask;
        }

        public Task<BigInteger?> GetOldestDiffBlockAsync()
        {
            var value = _manager.Get(RocksDbManager.CF_STATE_HISTORY_META, META_KEY_OLDEST);
            if (value == null) return Task.FromResult<BigInteger?>(null);
            return Task.FromResult<BigInteger?>(new BigInteger(HistoryKeys.ReadBlockNumber(value)));
        }

        public Task<BigInteger?> GetNewestDiffBlockAsync()
        {
            var value = _manager.Get(RocksDbManager.CF_STATE_HISTORY_META, META_KEY_NEWEST);
            if (value == null) return Task.FromResult<BigInteger?>(null);
            return Task.FromResult<BigInteger?>(new BigInteger(HistoryKeys.ReadBlockNumber(value)));
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

        private List<byte[]> IndexKeysOfBlock(byte[] blockBytes)
        {
            var keys = new List<byte[]>();
            using var it = _manager.CreateIterator(RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX);
            for (it.Seek(IndexSeekKey(blockBytes)); it.Valid(); it.Next())
            {
                var indexKey = it.Key();
                if (indexKey.Length < 9) continue;
                if (!ByteUtil.StartsWith(indexKey, blockBytes)) break;
                keys.Add(indexKey);
            }
            return keys;
        }

        private static byte[] IndexSeekKey(byte[] blockBytes) => ByteUtil.Merge(blockBytes, new byte[] { 0x00 });

        private static (byte[] DataKey, string Cf)? DataRowOf(byte[] indexKey, byte[] blockBytes)
        {
            var type = indexKey[8];
            if (type == TYPE_ACCOUNT && indexKey.Length == AccountIndexKeyLength)
                return (BuildAccountDataKey(AddressOf(indexKey), blockBytes), RocksDbManager.CF_STATE_HISTORY_ACCOUNTS);
            if (type == TYPE_STORAGE && indexKey.Length == StorageIndexKeyLength)
                return (BuildStorageDataKey(AddressOf(indexKey), SlotOf(indexKey), blockBytes), RocksDbManager.CF_STATE_HISTORY_STORAGE);
            return null;
        }

        private static byte[] AddressOf(byte[] indexKey) => indexKey.AsSpan(9, AddressKeyLength).ToArray();

        private static byte[] SlotOf(byte[] indexKey) => indexKey.AsSpan(9 + AddressKeyLength, 32).ToArray();

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
            var cfIndex = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX);
            using var batch = _manager.CreateWriteBatch();
            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX);

            if (seekKey != null)
                iterator.Seek(seekKey);
            else
                iterator.SeekToFirst();

            while (iterator.Valid())
            {
                var indexKey = iterator.Key();

                if (endKey != null && ByteArrayComparer.Current.Compare(indexKey, endKey) >= 0)
                    break;

                if (indexKey.Length >= 9)
                    StageIndexRowDelete(batch, cfIndex, indexKey, HistoryKeys.BlockKey(HistoryKeys.ReadBlockNumber(indexKey)));

                iterator.Next();
            }

            if (batch.Count() == 0)
                return;

            _manager.Write(batch);
        }

        private void UpdateMetaBounds(WriteBatch batch, ColumnFamilyHandle cfMeta, BigInteger blockNumber)
        {
            var blockBytes = HistoryKeys.BlockKey((ulong)blockNumber);
            var currentOldest = _manager.Get(RocksDbManager.CF_STATE_HISTORY_META, META_KEY_OLDEST);
            var currentNewest = _manager.Get(RocksDbManager.CF_STATE_HISTORY_META, META_KEY_NEWEST);

            if (currentOldest == null || HistoryKeys.ReadBlockNumber(currentOldest) > blockNumber)
                batch.Put(META_KEY_OLDEST, blockBytes, cfMeta);

            if (currentNewest == null || HistoryKeys.ReadBlockNumber(currentNewest) < blockNumber)
                batch.Put(META_KEY_NEWEST, blockBytes, cfMeta);
        }

        private void UpdateMetaAfterDelete()
        {
            using var iterator = _manager.CreateIterator(RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX);
            iterator.SeekToFirst();
            var oldest = BlockOfCurrentRow(iterator);

            iterator.SeekToLast();
            var newest = BlockOfCurrentRow(iterator);

            using var batch = _manager.CreateWriteBatch();
            var cfMeta = _manager.GetColumnFamily(RocksDbManager.CF_STATE_HISTORY_META);

            if (oldest.HasValue)
                batch.Put(META_KEY_OLDEST, HistoryKeys.BlockKey((ulong)oldest.Value), cfMeta);
            else
                batch.Delete(META_KEY_OLDEST, cfMeta);

            if (newest.HasValue)
                batch.Put(META_KEY_NEWEST, HistoryKeys.BlockKey((ulong)newest.Value), cfMeta);
            else
                batch.Delete(META_KEY_NEWEST, cfMeta);

            _manager.Write(batch);
        }

        private static BigInteger? BlockOfCurrentRow(Iterator iterator)
        {
            if (!iterator.Valid()) return null;
            var number = HistoryKeys.TryReadBlockNumber(iterator.Key());
            return number.HasValue ? new BigInteger(number.Value) : (BigInteger?)null;
        }

        #endregion
    }
}
