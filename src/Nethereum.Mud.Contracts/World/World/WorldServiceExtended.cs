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
using Nethereum.Contracts;
using System.Runtime.InteropServices;
using Nethereum.Mud.TableRepository;
using Nethereum.Contracts.QueryHandlers.MultiCall;

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
            getRecordFunction.TableId = tableSingleton.ResourceIdEncoded;
            getRecordFunction.KeyTuple = new List<byte[]>();

            var result = await ContractHandler.QueryDeserializingToObjectAsync<GetRecordFunction, GetRecordOutputDTO>(getRecordFunction, blockParameter);
            tableSingleton.DecodeValues(result.StaticData, result.EncodedLengths, result.DynamicData);
            return tableSingleton;
        }

        public async Task<TTableSingleton> GetRecordTableQueryAsync<TTableSingleton, TValue>(BlockParameter blockParameter = null) where TTableSingleton : TableRecordSingleton<TValue>, new()
            where TValue : class, new()
        {
            var tableSingleton = new TTableSingleton();
            return await GetRecordTableQueryAsync<TTableSingleton, TValue>(tableSingleton, blockParameter);
        }

        public async Task<TTable> GetRecordTableQueryAsync<TTable, TKey, TValue>(TTable table, BlockParameter blockParameter = null)
            where TTable : TableRecord<TKey, TValue>
            where TValue : class, new()
            where TKey : class, new()
        {
            var getRecordFunction = new GetRecordFunction();
            getRecordFunction.TableId = table.ResourceIdEncoded;
            getRecordFunction.KeyTuple = table.GetEncodedKey();

            var result = await ContractHandler.QueryDeserializingToObjectAsync<GetRecordFunction, GetRecordOutputDTO>(getRecordFunction, blockParameter);
            table.DecodeValues(result.StaticData, result.EncodedLengths, result.DynamicData);
            return table;
        }


        public async Task<List<TTable>> GetRecordTableMultiQueryRpcAsync<TTable, TKey, TValue>(List<TTable> tableRecords, BlockParameter blockParameter = null)
           where TTable : TableRecord<TKey, TValue>, new()
           where TValue : class, new()
           where TKey : class, new()
        {
            var getRecordFunctions = new List<IMulticallInputOutput>();
            foreach (var record in tableRecords)
            {
                getRecordFunctions.Add(new MulticallInputOutput<GetRecordFunction, GetRecordOutputDTO>(new GetRecordFunction()
                {
                    TableId = record.ResourceIdEncoded,
                    KeyTuple = record.GetEncodedKey()
                }, ContractAddress));
            }
            
            var result = await Web3.Eth.GetMultiQueryBatchRpcHandler().MultiCallAsync(blockParameter, MultiQueryBatchRpcHandler.DEFAULT_CALLS_PER_REQUEST, getRecordFunctions.ToArray());
            for (var i = 0; i < result.Length; i++)
            {
               var record   = result[i] as MulticallInputOutput<GetRecordFunction, GetRecordOutputDTO>;
               var table = tableRecords.Where(x => KeyUtils.ConvertKeyToCombinedHex(x.GetEncodedKey()) == KeyUtils.ConvertKeyToCombinedHex(record.Input.KeyTuple)).FirstOrDefault();
               table.DecodeValues(record.Output.StaticData, record.Output.EncodedLengths, record.Output.DynamicData);
            }
            return tableRecords;
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

        public Task<string> DeleteRecordRequestAsync<TKey, TValue>(TableRecord<TKey, TValue> table) where TKey : class, new() where TValue : class, new()
        {
            var deleteRecordFunction = BuildDeleteRecordFunction(table);
            return ContractHandler.SendRequestAsync(deleteRecordFunction);
        }

        public Task<TransactionReceipt> DeleteRecordRequestAndWaitForReceiptAsync<TKey, TValue>(TableRecord<TKey, TValue> table, CancellationTokenSource cancellationTokenSource = null)
            where TKey : class, new() where TValue : class, new()
        {
            var deleteRecordFunction = BuildDeleteRecordFunction(table);
            return ContractHandler.SendRequestAndWaitForReceiptAsync(deleteRecordFunction, cancellationTokenSource);
        }

        public static SetRecordFunction BuildSetRecordFunction<TKey, TValue>(TableRecord<TKey, TValue> table)
            where TKey : class, new() where TValue : class, new()
        {
            var setRecordFunction = new SetRecordFunction();
            setRecordFunction.TableId = table.ResourceIdEncoded;
            var encodedValues = table.GetEncodeValues();
            setRecordFunction.StaticData = encodedValues.StaticData;
            setRecordFunction.DynamicData = encodedValues.DynamicData;
            setRecordFunction.EncodedLengths = encodedValues.EncodedLengths;
            setRecordFunction.KeyTuple = table.GetEncodedKey();
            return setRecordFunction;
        }

        public static DeleteRecordFunction BuildDeleteRecordFunction<TKey, TValue>(TableRecord<TKey, TValue> table)
             where TKey : class, new() where TValue : class, new()
        {
            var deleteRecordFunction = new DeleteRecordFunction();
            deleteRecordFunction.TableId = table.ResourceIdEncoded;
            deleteRecordFunction.KeyTuple = table.GetEncodedKey();
            return deleteRecordFunction;
        }

        public static SetRecordFunction BuildSetRecordFunction<TValue>(TableRecordSingleton<TValue> table)
            where TValue : class, new()
        {
            var setRecordFunction = new SetRecordFunction();
            setRecordFunction.TableId = table.ResourceIdEncoded;
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
