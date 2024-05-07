using Nethereum.Mud.EncodingDecoding;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace Nethereum.Mud.TableRepository
{
    public interface ITableRepository
    {
        Task DeleteRecordAsync(byte[] tableId, List<byte[]> key);
        Task DeleteRecordAsync(string tableIdHex, string keyHex);
        Task DeleteTableAsync(string tableIdHex);
        Task<EncodedValues> GetRecordAsync(byte[] tableId, byte[] key);
        Task<EncodedValues> GetRecordAsync(string tableIdHex, string keyHex);
        Task<IEnumerable<EncodedTableRecord>> GetRecordsAsync(byte[] tableId);
        Task<IEnumerable<EncodedTableRecord>> GetRecordsAsync(string tableIdHex);
        Task<IEnumerable<TTableRecord>> GetTableRecordsAsync<TTableRecord>(string tableIdHex) where TTableRecord : ITableRecordSingleton, new();
        Task SetRecordAsync(byte[] tableId, byte[] key, EncodedValues encodedValues);
        Task SetRecordAsync(byte[] tableId, List<byte[]> key, byte[] staticData, byte[] encodedLengths, byte[] dynamicData);
        Task SetRecordAsync(string tableIdHex, string keyHex, byte[] staticData, byte[] encodedLengths, byte[] dynamicData);
        Task SetRecordAsync(string tableIdHex, string keyHex, EncodedValues encodedValues);
        Task SetSpliceDynamicDataAsync(byte[] tableId, List<byte[]> key, ulong start, byte[] newData, ulong deleteCount, byte[] encodedLengths);
        Task SetSpliceDynamicDataAsync(string tableIdHex, string keyHex, ulong start, byte[] newData, ulong deleteCount, byte[] encodedLengths);
        Task SetSpliceStaticDataAsync(byte[] tableId, List<byte[]> key, ulong start, byte[] newData);
        Task SetSpliceStaticDataAsync(string tableIdHex, string keyHex, ulong start, byte[] newData);
    }
}