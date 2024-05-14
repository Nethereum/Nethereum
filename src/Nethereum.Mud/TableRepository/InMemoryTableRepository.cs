using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Mud.EncodingDecoding;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using System.Linq;


namespace Nethereum.Mud.TableRepository
{
    public class InMemoryTableRepository : ITableRepository
    {
        public Dictionary<string, Dictionary<string, EncodedValues>> Tables { get; set; }

        public InMemoryTableRepository()
        {
            Tables = new Dictionary<string, Dictionary<string, EncodedValues>>();
        }

        public Task<EncodedValues> GetRecordAsync(byte[] tableId, byte[] key)
        {
            return GetRecordAsync(tableId.ToHex(true), key.ToHex(true));
        }

        public Task<EncodedValues> GetRecordAsync(string tableIdHex, string keyHex)
        {
            tableIdHex = tableIdHex.EnsureHexPrefix();
            keyHex = keyHex.EnsureHexPrefix();

            if (Tables.ContainsKey(tableIdHex))
            {
                if (Tables[tableIdHex].ContainsKey(keyHex))
                {
                    return Task.FromResult(Tables[tableIdHex][keyHex]);
                }
            }
            return Task.FromResult<EncodedValues>(null);
        }

        public Task<IEnumerable<EncodedTableRecord>> GetRecordsAsync(byte[] tableId)
        {
            return GetRecordsAsync(tableId.ToHex(true));
        }

        public Task<IEnumerable<EncodedTableRecord>> GetRecordsAsync(string tableIdHex)
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
                        Key = ConvertCommaSeparatedHexToKey(key),
                        EncodedValues = Tables[tableIdHex][key]
                    });
                }
            
            }
          
                return Task.FromResult<IEnumerable<EncodedTableRecord>>(records);
            
        }

        public async Task<IEnumerable<TTableRecord>> GetTableRecordsAsync<TTableRecord>(byte[] tableId) where TTableRecord : ITableRecordSingleton, new()
        {
            return await GetTableRecordsAsync<TTableRecord>(tableId.ToHex(true));
        }

        public async Task<IEnumerable<TTableRecord>> GetTableRecordsAsync<TTableRecord>() where TTableRecord : ITableRecordSingleton, new()
        {
            var resourceIdEncoded = ResourceRegistry.GetResourceEncoded<TTableRecord>();
            return await GetTableRecordsAsync<TTableRecord>(resourceIdEncoded.ToHex(true));
        }

        public async Task<IEnumerable<TTableRecord>> GetTableRecordsAsync<TTableRecord>(string tableIdHex) where TTableRecord : ITableRecordSingleton, new()
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



        public Task SetRecordAsync(byte[] tableId, byte[] key, EncodedValues encodedValues)
        {
            return SetRecordAsync(tableId.ToHex(true), key.ToHex(true), encodedValues);
        }

        public Task SetRecordAsync(string tableIdHex, string keyHex, EncodedValues encodedValues)
        {
            tableIdHex = tableIdHex.EnsureHexPrefix();
            keyHex = keyHex.EnsureHexPrefix();
            if (!Tables.ContainsKey(tableIdHex))
            {
                Tables[tableIdHex] = new Dictionary<string, EncodedValues>();
            }
            Tables[tableIdHex][keyHex] = encodedValues;
#if NET451 || NETSTANDARD1_1
            return Task.FromResult(0);
#else
            return Task.CompletedTask;
#endif
        }

        public static string ConvertKeyToCommaSeparatedHex(List<byte[]> key)
        {
            return string.Join(",", key.Select(k => k.ToHex(true)));
        }

        public static List<byte[]> ConvertCommaSeparatedHexToKey(string key)
        {
            return key.Split(',').Select(k => k.HexToByteArray()).ToList();
        }


        public Task SetRecordAsync(byte[] tableId, List<byte[]> key, byte[] staticData, byte[] encodedLengths, byte[] dynamicData)
        {
            return SetRecordAsync(tableId.ToHex(true), ConvertKeyToCommaSeparatedHex(key), staticData, encodedLengths, dynamicData);
        }

        public Task SetRecordAsync(string tableIdHex, string keyHex, byte[] staticData, byte[] encodedLengths, byte[] dynamicData)
        {
            tableIdHex = tableIdHex.EnsureHexPrefix();
            keyHex = keyHex.EnsureHexPrefix();

            if (!Tables.ContainsKey(tableIdHex))
            {
                Tables[tableIdHex] = new Dictionary<string, EncodedValues>();
            }
            Tables[tableIdHex][keyHex] = new EncodedValues()
            {
                StaticData = staticData,
                EncodedLengths = encodedLengths,
                DynamicData = dynamicData
            };

#if NET451 || NETSTANDARD1_1
            return Task.FromResult(0);
#else
            return Task.CompletedTask;
#endif
        }

        public byte[] SpliceBytes(byte[] data, int start, int deleteCount, byte[] newData)
        {
            var dataNibbles = data.ToList();
            var newDataNibbles = newData;
            if (start + deleteCount > dataNibbles.Count)
            {
                for (var i = dataNibbles.Count; i < start + deleteCount; i++)
                {
                    dataNibbles.Add(0);
                }
            }
            dataNibbles.RemoveRange(start, deleteCount);
            return dataNibbles.Concat(newDataNibbles).ToArray();
        }

        public Task SetSpliceStaticDataAsync(byte[] tableId, List<byte[]> key, ulong start, byte[] newData)
        {
            return SetSpliceStaticDataAsync(tableId.ToHex(true), ConvertKeyToCommaSeparatedHex(key), start, newData);
        }

        public async Task SetSpliceStaticDataAsync(string tableIdHex, string keyHex, ulong start, byte[] newData)
        {
            tableIdHex = tableIdHex.EnsureHexPrefix();
            keyHex = keyHex.EnsureHexPrefix();

            var record = await GetRecordAsync(tableIdHex, keyHex);
            if (record == null)
            {
                record = new EncodedValues();
                record.EncodedLengths = new byte[0];
                record.DynamicData = new byte[0];
                record.StaticData = new byte[0];
            }

            record.StaticData = SpliceBytes(record.StaticData, (int)start, newData.Length, newData);
            await SetRecordAsync(tableIdHex, keyHex, record);
        }

        public Task SetSpliceDynamicDataAsync(byte[] tableId, List<byte[]> key, ulong start, byte[] newData, ulong deleteCount, byte[] encodedLengths)
        {
            return SetSpliceDynamicDataAsync(tableId.ToHex(true), ConvertKeyToCommaSeparatedHex(key), start, newData, deleteCount, encodedLengths);
        }

        public async Task SetSpliceDynamicDataAsync(string tableIdHex, string keyHex, ulong start, byte[] newData, ulong deleteCount, byte[] encodedLengths)
        {
            tableIdHex = tableIdHex.EnsureHexPrefix();
            keyHex = keyHex.EnsureHexPrefix();

            var record = await GetRecordAsync(tableIdHex, keyHex);
            if (record == null)
            {
                record = new EncodedValues();
                record.EncodedLengths = new byte[0];
                record.DynamicData = new byte[0];
                record.StaticData = new byte[0];
            }
            record.EncodedLengths = encodedLengths;
            record.DynamicData = SpliceBytes(record.DynamicData, (int)start, (int)deleteCount, newData);
            await SetRecordAsync(tableIdHex, keyHex, record);
        }

        public Task DeleteRecordAsync(byte[] tableId, List<byte[]> key)
        {
            return DeleteRecordAsync(tableId.ToHex(true), ConvertKeyToCommaSeparatedHex(key));
        }

        public Task DeleteRecordAsync(string tableIdHex, string keyHex)
        {
            tableIdHex = tableIdHex.EnsureHexPrefix();
            keyHex = keyHex.EnsureHexPrefix();

            if (Tables.ContainsKey(tableIdHex))
            {
                if (Tables[tableIdHex].ContainsKey(keyHex))
                {
                    Tables[tableIdHex].Remove(keyHex);
                }
            }
#if NET451 || NETSTANDARD1_1
            return Task.FromResult(0);
#else
            return Task.CompletedTask;
#endif
        }

        public Task DeleteTableAsync(string tableIdHex)
        {
            tableIdHex = tableIdHex.EnsureHexPrefix();
            

            if (Tables.ContainsKey(tableIdHex))
            {
                Tables.Remove(tableIdHex);
            }
#if NET451 || NETSTANDARD1_1
            return Task.FromResult(0);
#else
            return Task.CompletedTask;
#endif
        }

    }
}
