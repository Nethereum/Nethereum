using Nethereum.Mud.EncodingDecoding;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using System.Numerics;
using System.Linq.Expressions;
using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System.Reflection;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Mud.TableRepository
{
    public interface ITableRepository: ITablePredicateQueryRepository
    {
        Task DeleteRecordAsync(byte[] tableId, List<byte[]> key, string address = null, BigInteger? blockNumber = null, int? logIndex = null);
        Task<StoredRecord> GetRecordAsync(byte[] tableId, byte[] key);
        Task<StoredRecord> GetRecordAsync(string tableIdHex, string keyHex);
        Task<IEnumerable<EncodedTableRecord>> GetRecordsAsync(byte[] tableId);
        Task<IEnumerable<EncodedTableRecord>> GetRecordsAsync(string tableIdHex);
        Task<IEnumerable<TTableRecord>> GetTableRecordsAsync<TTableRecord>(string tableIdHex) where TTableRecord : ITableRecordSingleton, new();
        Task<IEnumerable<TTableRecord>> GetTableRecordsAsync<TTableRecord>() where TTableRecord : ITableRecordSingleton, new();
        Task SetRecordAsync(byte[] tableId, List<byte[]> key, EncodedValues encodedValues, string address = null, BigInteger? blockNumber = null, int? logIndex = null);
        Task SetRecordAsync(byte[] tableId, List<byte[]> key, byte[] staticData, byte[] encodedLengths, byte[] dynamicData, string address = null, BigInteger? blockNumber = null, int? logIndex = null);
        Task SetRecordAsync<TTableRecord>(TTableRecord record, string address = null, BigInteger? blockNumber = null, int? logIndex = null) where TTableRecord : ITableRecord;
        Task SetRecordsAsync<TTableRecord>(IEnumerable<TTableRecord> records, string address = null, BigInteger? blockNumber = null, int? logIndex = null) where TTableRecord : ITableRecord;
        Task SetSpliceDynamicDataAsync(byte[] tableId, List<byte[]> key, ulong start, byte[] newData, ulong deleteCount, byte[] encodedLengths, string address = null, BigInteger? blockNumber = null, int? logIndex = null);
        Task SetSpliceStaticDataAsync(byte[] tableId, List<byte[]> key, ulong start, byte[] newData, string address = null, BigInteger? blockNumber = null, int? logIndex = null);
     

    }

    public interface  ITablePredicateQueryRepository
    {
        Task<IEnumerable<TTableRecord>> GetTableRecordsAsync<TTableRecord>(TablePredicate predicate) where TTableRecord : ITableRecord, new();
        Task<List<StoredRecord>> GetRecordsAsync(TablePredicate predicate);
    }
}