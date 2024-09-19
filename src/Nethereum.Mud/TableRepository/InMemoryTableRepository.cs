using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Mud.EncodingDecoding;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using Org.BouncyCastle.Asn1.Cms;
using System.Numerics;
using Org.BouncyCastle.Utilities.Net;
using System;
using Nethereum.Util;


namespace Nethereum.Mud.TableRepository
{

    public class InMemoryTableRepository : TableRepositoryBase, ITableRepository
    {
        public Dictionary<string, Dictionary<string, StoredRecord>> Tables { get; set; }
        public List<StoredRecord> AllRecords { get; set; } = new List<StoredRecord>();

        public InMemoryTableRepository()
        {
            Tables = new Dictionary<string, Dictionary<string, StoredRecord>>();
        }

        private void AddToAllRecords(StoredRecord record)
        {
            if (!AllRecords.Contains(record))
            {
                AllRecords.Add(record);
            }
        }

      
        private void RemoveFromAllRecords(StoredRecord record)
        {
            AllRecords.Remove(record);
        }

    
        public override Task<StoredRecord> GetRecordAsync(string tableIdHex, string keyHex)
        {
            tableIdHex = tableIdHex.EnsureHexPrefix();
            keyHex = keyHex.EnsureHexPrefix();

            if (Tables.ContainsKey(tableIdHex) && Tables[tableIdHex].ContainsKey(keyHex))
            {
                return Task.FromResult(Tables[tableIdHex][keyHex]);
            }

            return Task.FromResult<StoredRecord>(null);
        }

        // Get all records for a table
        public override Task<IEnumerable<EncodedTableRecord>> GetRecordsAsync(string tableIdHex)
        {
            tableIdHex = tableIdHex.EnsureHexPrefix();
            var records = new List<EncodedTableRecord>();

            if (Tables.ContainsKey(tableIdHex))
            {
                foreach (var key in Tables[tableIdHex].Keys)
                {
                    records.Add(new EncodedTableRecord()
                    {
                        TableId = tableIdHex.HexToByteArray(),
                        Key = ConvertKeyFromCombinedHex(key),
                        EncodedValues = Tables[tableIdHex][key]
                    });
                }
            }

            return Task.FromResult<IEnumerable<EncodedTableRecord>>(records);
        }

        // Get all table records decoded for a specific type
        public override async Task<IEnumerable<TTableRecord>> GetTableRecordsAsync<TTableRecord>(string tableIdHex)
        {
            tableIdHex.EnsureHexPrefix();
            var records = await GetRecordsAsync(tableIdHex);
            var result = new List<TTableRecord>();

            foreach (var record in records)
            {
                var tableRecord = new TTableRecord();
                tableRecord.DecodeValues(record.EncodedValues);

                if (tableRecord is ITableRecord tableRecordKey)
                {
                    tableRecordKey.DecodeKey(record.Key);
                }

                result.Add(tableRecord);
            }

            return result;
        }

        // Set a record with encoded values and update AllRecords
        public override Task SetRecordAsync(byte[] tableId, List<byte[]> key, EncodedValues encodedValues, string address = null, BigInteger? blockNumber = null, int? logIndex = null)
        {
            var tableIdHex = tableId.ToHex(true);
            var keyHex = ConvertKeyToCombinedHex(key).EnsureHexPrefix();

            if (!Tables.ContainsKey(tableIdHex))
            {
                Tables[tableIdHex] = new Dictionary<string, StoredRecord>();
            }

            var record = new StoredRecord
            {
                TableIdBytes = tableId,
                KeyBytes = ConvertKeyToCombinedHex(key).HexToByteArray(),
                AddressBytes = address.HexToByteArray(),
                BlockNumber = blockNumber,
                LogIndex = logIndex,
                DynamicData = encodedValues.DynamicData,
                StaticData = encodedValues.StaticData,
                EncodedLengths = encodedValues.EncodedLengths
            };

            SetKeyBytes(record, key); // Directly set the key bytes for key0, key1, key2, key3

            Tables[tableIdHex][keyHex] = record;

            AddToAllRecords(record); // Add to AllRecords

#if NET451 || NETSTANDARD1_1
            return Task.FromResult(0);
#else
        return Task.CompletedTask;
#endif
        }

        // Set a record with individual data fields and update AllRecords
        public override Task SetRecordAsync(byte[] tableId, List<byte[]> key, byte[] staticData, byte[] encodedLengths, byte[] dynamicData, string address = null, BigInteger? blockNumber = null, int? logIndex = null)
        {
            var tableIdHex = tableId.ToHex(true);
            var keyHex = ConvertKeyToCombinedHex(key).EnsureHexPrefix();

            if (!Tables.ContainsKey(tableIdHex))
            {
                Tables[tableIdHex] = new Dictionary<string, StoredRecord>();
            }

            var record = new StoredRecord
            {
                TableIdBytes = tableId,
                KeyBytes = ConvertKeyToCombinedHex(key).HexToByteArray(),
                AddressBytes = address.HexToByteArray(),
                BlockNumber = blockNumber,
                LogIndex = logIndex,
                StaticData = staticData,
                EncodedLengths = encodedLengths,
                DynamicData = dynamicData
            };

            SetKeyBytes(record, key); // Directly set the key bytes for key0, key1, key2, key3

            Tables[tableIdHex][keyHex] = record;

            AddToAllRecords(record); // Add to AllRecords

#if NET451 || NETSTANDARD1_1
            return Task.FromResult(0);
#else
        return Task.CompletedTask;
#endif
        }

        // Set or update static data by splicing and update AllRecords
        public async Task SetSpliceStaticDataAsync(byte[] tableId, List<byte[]> key, ulong start, byte[] newData, string address = null, BigInteger? blockNumber = null, int? logIndex = null)
        {
            var tableIdHex = tableId.ToHex(true);
            var keyHex = ConvertKeyToCombinedHex(key).EnsureHexPrefix();

            var record = await GetRecordAsync(tableIdHex, keyHex);
            if (record == null)
            {
                record = new StoredRecord
                {
                    TableIdBytes = tableId,
                    KeyBytes = ConvertKeyToCombinedHex(key).HexToByteArray(),
                    AddressBytes = address.HexToByteArray(),
                    EncodedLengths = new byte[0],
                    DynamicData = new byte[0],
                    StaticData = new byte[0]
                };

                SetKeyBytes(record, key); // Directly set the key bytes for key0, key1, key2, key3
            }

            record.BlockNumber = blockNumber;
            record.LogIndex = logIndex;
            record.StaticData = SpliceBytes(record.StaticData, (int)start, newData.Length, newData);
            await SetRecordAsync(tableId, key, record.StaticData, record.EncodedLengths, record.DynamicData, address, blockNumber, logIndex);
            AddToAllRecords(record); // Add to AllRecords


        }

        // Set or update dynamic data by splicing and update AllRecords
        public async Task SetSpliceDynamicDataAsync(byte[] tableId, List<byte[]> key, ulong start, byte[] newData, ulong deleteCount, byte[] encodedLengths, string address = null, BigInteger? blockNumber = null, int? logIndex = null)
        {
            var tableIdHex = tableId.ToHex(true);
            var keyHex = ConvertKeyToCombinedHex(key).EnsureHexPrefix();

            var record = await GetRecordAsync(tableIdHex, keyHex);
            if (record == null)
            {
                record = new StoredRecord
                {
                    TableIdBytes = tableId,
                    KeyBytes = ConvertKeyToCombinedHex(key).HexToByteArray(),
                    AddressBytes = address.HexToByteArray(),
                    EncodedLengths = new byte[0],
                    DynamicData = new byte[0],
                    StaticData = new byte[0]
                };

                SetKeyBytes(record, key); // Directly set the key bytes for key0, key1, key2, key3
            }

            record.BlockNumber = blockNumber;
            record.LogIndex = logIndex;
            record.EncodedLengths = encodedLengths;
            record.DynamicData = SpliceBytes(record.DynamicData, (int)start, (int)deleteCount, newData);
            await SetRecordAsync(tableId, key, record.StaticData, record.EncodedLengths, record.DynamicData, address, blockNumber, logIndex);
            AddToAllRecords(record); // Add to AllRecords


        }

        // Delete a specific record and remove from AllRecords
        public Task DeleteRecordAsync(byte[] tableId, List<byte[]> key, string address = null, BigInteger? blockNumber = null, int? logIndex = null)
        {
            var tableIdHex = tableId.ToHex(true);
            var keyHex = ConvertKeyToCombinedHex(key).EnsureHexPrefix();

            if (Tables.ContainsKey(tableIdHex))
            {
                if (Tables[tableIdHex].ContainsKey(keyHex))
                {
                    var record = Tables[tableIdHex][keyHex];
                    RemoveFromAllRecords(record); // Remove from AllRecords
                    Tables[tableIdHex].Remove(keyHex);
                }
            }

#if NET451 || NETSTANDARD1_1
            return Task.FromResult(0);
#else
        return Task.CompletedTask;
#endif
        }

        // Delete all records in a table and remove from AllRecords
        public Task DeleteTableAsync(byte[] tableId)
        {
            var tableIdHex = tableId.ToHex(true).EnsureHexPrefix();

            if (Tables.ContainsKey(tableIdHex))
            {
                var recordsToRemove = Tables[tableIdHex].Values.ToList();
                foreach (var record in recordsToRemove)
                {
                    RemoveFromAllRecords(record); // Remove from AllRecords
                }

                Tables.Remove(tableIdHex);
            }

#if NET451 || NETSTANDARD1_1
            return Task.FromResult(0);
#else
        return Task.CompletedTask;
#endif
        }

        public Task<IEnumerable<TTableRecord>> GetTableRecordsAsync<TTableRecord>(TablePredicate predicate) where TTableRecord : ITableRecord, new()
        {
            throw new NotSupportedException($"InMemoryTableRepository does not support predicate filtering, use AllRecords and Linq directly ");
        }

        public Task<List<StoredRecord>> GetRecordsAsync(TablePredicate predicate)
        {
            throw new NotSupportedException($"InMemoryTableRepository does not support predicate filtering, use AllRecords and Linq directly ");
        }
    }

}
