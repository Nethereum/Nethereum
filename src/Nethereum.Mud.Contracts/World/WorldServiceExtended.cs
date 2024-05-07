using Nethereum.Mud.Contracts.World.ContractDefinition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.RPC.Eth.DTOs;
using Org.BouncyCastle.Crypto.Tls;
using Nethereum.Mud.EncodingDecoding;
using System.Threading;

namespace Nethereum.Mud.Contracts.World
{
    public partial class WorldService
    {
        public Task<string> StoreVersionQueryAsStringAsync()
        {
            return ContractHandler.QueryAsync<StoreVersionFunction, string>();
        }

        public async Task<TTableSingleton> GetRecordTableQueryAsync<TTableSingleton, TValue>(TTableSingleton tableSingleton, BlockParameter blockParameter = null) where TTableSingleton : TableRecordSingleton<TValue>
            where TValue : class, new()
        {
            var getRecordFunction = new GetRecordFunction();
            getRecordFunction.TableId = tableSingleton.ResourceId;
            getRecordFunction.KeyTuple = new List<byte[]>();

            var result = await ContractHandler.QueryDeserializingToObjectAsync<GetRecordFunction, GetRecordOutputDTO>(getRecordFunction, blockParameter);
            tableSingleton.DecodeValues(result.StaticData, result.EncodedLengths, result.DynamicData);
            return tableSingleton;
        }

        public async Task<TTable> GetRecordTableQueryAsync<TTable, TKey, TValue>(TTable table, BlockParameter blockParameter = null)
            where TTable : TableRecord<TKey, TValue>
            where TValue : class, new()
            where TKey : class, new()
        {
            var getRecordFunction = new GetRecordFunction();
            getRecordFunction.TableId = table.ResourceId;
            getRecordFunction.KeyTuple = table.GetEncodedKey();

            var result = await ContractHandler.QueryDeserializingToObjectAsync<GetRecordFunction, GetRecordOutputDTO>(getRecordFunction, blockParameter);
            table.DecodeValues(result.StaticData, result.EncodedLengths, result.DynamicData);
            return table;
        }

        public Task<GetRecordOutputDTO> GetRecordQueryAsync(string tableName, BlockParameter blockParameter = null)
        {
            var getRecordFunction = new GetRecordFunction();
            getRecordFunction.TableId = ResourceEncoder.EncodeRootTable(tableName);
            getRecordFunction.KeyTuple = new List<byte[]>();

            return ContractHandler.QueryDeserializingToObjectAsync<GetRecordFunction, GetRecordOutputDTO>(getRecordFunction, blockParameter);
        }

        public Task<GetRecordOutputDTO> GetRecordQueryAsync(string tableName, List<byte[]> keys, BlockParameter blockParameter = null)
        {
            var getRecordFunction = new GetRecordFunction();
            getRecordFunction.TableId = ResourceEncoder.EncodeRootTable(tableName);
            getRecordFunction.KeyTuple = keys;

            return ContractHandler.QueryDeserializingToObjectAsync<GetRecordFunction, GetRecordOutputDTO>(getRecordFunction, blockParameter);
        }

        public Task<string> SetRecordRequestAsync<TKey, TValue>(TableRecord<TKey, TValue> table) where TKey : class, new() where TValue : class, new()    
        {
            var setRecordFunction = BuildSetRecordFunction<TKey, TValue>(table);
            return ContractHandler.SendRequestAsync(setRecordFunction);
        }

        public Task<string> SetRecordRequestAsync<TValue>(TableRecordSingleton<TValue> table) where TValue : class, new()
        {
            var setRecordFunction = BuildSetRecordFunction(table);
            return ContractHandler.SendRequestAsync(setRecordFunction);
        }

        public Task<TransactionReceipt> SetRecordRequestAndWaitForReceiptAsync<TKey, TValue>(TableRecord<TKey, TValue> table, CancellationTokenSource cancellationTokenSource = null)
            where TKey : class, new() where TValue : class, new()
        {
            var setRecordFunction = BuildSetRecordFunction(table);
            return ContractHandler.SendRequestAndWaitForReceiptAsync(setRecordFunction, cancellationTokenSource);
        }

        public Task<TransactionReceipt> SetRecordRequestAndWaitForReceiptAsync<TValue>(TableRecordSingleton<TValue> table, CancellationTokenSource cancellationTokenSource = null)
           where TValue : class, new()
        {
            var setRecordFunction = BuildSetRecordFunction(table);
            return ContractHandler.SendRequestAndWaitForReceiptAsync(setRecordFunction, cancellationTokenSource);
        }

        public static SetRecordFunction BuildSetRecordFunction<TKey, TValue>(TableRecord<TKey, TValue> table)
            where TKey : class, new() where TValue : class, new()
        {
            var setRecordFunction = new SetRecordFunction();
            setRecordFunction.TableId = table.ResourceId;
            var encodedValues = table.GetEncodeValues();
            setRecordFunction.StaticData = encodedValues.StaticData;
            setRecordFunction.DynamicData = encodedValues.DynamicData;
            setRecordFunction.EncodedLengths = encodedValues.EncodedLengths;
            setRecordFunction.KeyTuple = table.GetEncodedKey();
            return setRecordFunction;
        }

        public static SetRecordFunction BuildSetRecordFunction<TValue>(TableRecordSingleton<TValue> table)
            where TValue : class, new()
        {
            var setRecordFunction = new SetRecordFunction();
            setRecordFunction.TableId = table.ResourceId;
            var encodedValues = table.GetEncodeValues();
            setRecordFunction.StaticData = encodedValues.StaticData;
            setRecordFunction.DynamicData = encodedValues.DynamicData;
            setRecordFunction.EncodedLengths = encodedValues.EncodedLengths;
            setRecordFunction.KeyTuple = new List<byte[]>();
            return setRecordFunction;
        }

        public Task<string> SetRecordRequestAsync(string tableName, byte[] staticData)
        {
            var setRecordFunction = new SetRecordFunction();
            setRecordFunction.TableId = ResourceEncoder.EncodeRootTable(tableName);
            setRecordFunction.StaticData = staticData;
            setRecordFunction.KeyTuple = new List<byte[]>();
            setRecordFunction.EncodedLengths = new byte[] { };
            setRecordFunction.DynamicData = new byte[] { };
            return ContractHandler.SendRequestAsync(setRecordFunction);
        } 

        public Task<TransactionReceipt> SetRecordRequestAndWaitForReceiptAsync(string tableName, byte[] staticData, CancellationTokenSource cancellationTokenSource = null)
        {
            var setRecordFunction = new SetRecordFunction();
            setRecordFunction.TableId = ResourceEncoder.EncodeRootTable(tableName);
            setRecordFunction.StaticData = staticData;
            setRecordFunction.KeyTuple = new List<byte[]>();
            setRecordFunction.EncodedLengths = new byte[] { };
            setRecordFunction.DynamicData = new byte[] { };
            return ContractHandler.SendRequestAndWaitForReceiptAsync(setRecordFunction, cancellationTokenSource);
        }

        public Task<TransactionReceipt> SetRecordRequestAndWaitForReceiptAsync(string tableName, List<byte[]> key, byte[] staticData, CancellationTokenSource cancellationTokenSource = null)
        {
            var setRecordFunction = new SetRecordFunction();
            setRecordFunction.TableId = ResourceEncoder.EncodeRootTable(tableName);
            setRecordFunction.StaticData = staticData;
            setRecordFunction.KeyTuple = key;
            setRecordFunction.EncodedLengths = new byte[] { };
            setRecordFunction.DynamicData = new byte[] { };
            return ContractHandler.SendRequestAndWaitForReceiptAsync(setRecordFunction, cancellationTokenSource);
        }


    }
}
