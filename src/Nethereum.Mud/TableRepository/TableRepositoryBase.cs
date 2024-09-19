using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Mud.EncodingDecoding;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;


namespace Nethereum.Mud.TableRepository
{
    public abstract class TableRepositoryBase
    {
        // Convert key to combined hex format
        public static string ConvertKeyToCombinedHex(List<byte[]> key)
        {
            return KeyUtils.ConvertKeyToCombinedHex(key);
        }

        // Convert combined hex back to byte arrays
        public static List<byte[]> ConvertKeyFromCombinedHex(string key)
        {
            return KeyUtils.ConvertKeyFromCombinedHex(key);
        }

        // Helper method to directly set key bytes (key0-key3)
        protected void SetKeyBytes(StoredRecord record, List<byte[]> keys)
        {
            record.Key0Bytes = keys.Count > 0 ? keys[0] : null;
            record.Key1Bytes = keys.Count > 1 ? keys[1] : null;
            record.Key2Bytes = keys.Count > 2 ? keys[2] : null;
            record.Key3Bytes = keys.Count > 3 ? keys[3] : null;
        }

        // Get table records by tableId (byte array version)
        public virtual async Task<IEnumerable<TTableRecord>> GetTableRecordsAsync<TTableRecord>(byte[] tableId) where TTableRecord : ITableRecordSingleton, new()
        {
            return await GetTableRecordsAsync<TTableRecord>(tableId.ToHex(true));
        }

        // Get table records by resourceId encoded in ResourceRegistry
        public virtual async Task<IEnumerable<TTableRecord>> GetTableRecordsAsync<TTableRecord>() where TTableRecord : ITableRecordSingleton, new()
        {
            var resourceIdEncoded = ResourceRegistry.GetResourceEncoded<TTableRecord>();
            return await GetTableRecordsAsync<TTableRecord>(resourceIdEncoded.ToHex(true));
        }

        // Get a record by tableId and key (byte array version)
        public virtual Task<StoredRecord> GetRecordAsync(byte[] tableId, byte[] key)
        {
            return GetRecordAsync(tableId.ToHex(true), key.ToHex(true));
        }

        // Get multiple records by tableId (byte array version)
        public virtual Task<IEnumerable<EncodedTableRecord>> GetRecordsAsync(byte[] tableId)
        {
            return GetRecordsAsync(tableId.ToHex(true));
        }

        // Abstract methods that must be implemented in derived classes
        public abstract Task<StoredRecord> GetRecordAsync(string tableIdHex, string keyHex);
        public abstract Task<IEnumerable<EncodedTableRecord>> GetRecordsAsync(string tableIdHex);
        public abstract Task<IEnumerable<TTableRecord>> GetTableRecordsAsync<TTableRecord>(string tableIdHex) where TTableRecord : ITableRecordSingleton, new();

        // Splice bytes in a byte array (used for dynamic/static data)
        public byte[] SpliceBytes(byte[] data, int start, int deleteCount, byte[] newData)
        {
            var dataNibbles = data.ToList();
            if (start + deleteCount > dataNibbles.Count)
            {
                for (var i = dataNibbles.Count; i < start + deleteCount; i++)
                {
                    dataNibbles.Add(0);
                }
            }
            dataNibbles.RemoveRange(start, deleteCount);
            dataNibbles.InsertRange(start, newData);
            return dataNibbles.ToArray();
        }

        // Set multiple records (useful for bulk updates)
        public async Task SetRecordsAsync<TTableRecord>(IEnumerable<TTableRecord> records, string address = null, BigInteger? blockNumber = null, int? logIndex = null) where TTableRecord : ITableRecord
        {
            foreach (var record in records)
            {
                await SetRecordAsync(record, address, blockNumber, logIndex);
            }
        }

        // Set an individual record (resolves to abstract method SetRecordAsync)
        public Task SetRecordAsync<TTableRecord>(TTableRecord record, string address = null, BigInteger? blockNumber = null, int? logIndex = null) where TTableRecord : ITableRecord
        {
            return SetRecordAsync(record.ResourceIdEncoded, record.GetEncodedKey(), record.GetEncodeValues(), address, blockNumber, logIndex);
        }

        // Abstract methods to be implemented in derived classes
        public abstract Task SetRecordAsync(byte[] tableId, List<byte[]> key, byte[] staticData, byte[] encodedLengths, byte[] dynamicData, string address = null, BigInteger? blockNumber = null, int? logIndex = null);

        public abstract Task SetRecordAsync(byte[] tableId, List<byte[]> key, EncodedValues encodedValues, string address = null, BigInteger? blockNumber = null, int? logIndex = null);
    }

}
