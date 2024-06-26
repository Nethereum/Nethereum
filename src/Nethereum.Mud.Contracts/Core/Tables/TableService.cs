
using Nethereum.Mud.Contracts.World;
using Nethereum.Web3;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.Services;
using Nethereum.RPC.Eth.DTOs;
using System.Numerics;
using System.Threading;
using System.Collections.Generic;
using Nethereum.Mud.TableRepository;
using Nethereum.Mud.EncodingDecoding;
using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;

namespace Nethereum.Mud.Contracts.Core.Tables
{
    public abstract class TableService<TTableRecord, TKey, TValue> : 
        TableServiceBase<TTableRecord, TValue>
        where TTableRecord : TableRecord<TKey, TValue>, new()
        where TValue : class, new() where TKey : class, new()
    {

        protected TableService(WorldNamespace world) : base(world)
        {

        }

        public TableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {

        }

        public virtual Task<IEnumerable<TTableRecord>> GetTableRecordsAsync(ITableRepository tableRepository)
        {
            return tableRepository.GetTableRecordsAsync<TTableRecord>();
        }

        public virtual Task<List<TTableRecord>> GetTableRecordsMulticallRpcAsync(List<TKey> key, BlockParameter blockParameter = null)
        {
            var input = new List<TTableRecord>();
            foreach (var k in key)
            {
                var table = new TTableRecord();
                table.Keys = k;
                input.Add(table);
            }
            return WorldService.GetRecordTableMultiQueryRpcAsync<TTableRecord, TKey, TValue>(input, blockParameter);
        }

        /// <summary>
        /// Gets the record from the table asynchronously using the provided key.
        /// </summary>
        /// <param name="key">The key to identify the record.</param>
        /// <param name="blockParameter">Optional block parameter.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the table record.</returns>
        public virtual async Task<TTableRecord> GetTableRecordAsync(TKey key, BlockParameter blockParameter = null)
        {
            var table = new TTableRecord();
            table.Keys = key;
            return await WorldService.GetRecordTableQueryAsync<TTableRecord, TKey, TValue>(table, blockParameter);
        }

        /// <summary>
        /// Sets a record in the table asynchronously.
        /// </summary>
        /// <param name="table">The table record to set.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the transaction hash.</returns>
        public virtual async Task<string> SetRecordRequestAsync(TTableRecord table)
        {
            return await WorldService.SetRecordRequestAsync(table);
        }

        /// <summary>
        /// Sets a record in the table asynchronously using the provided key and value.
        /// </summary>
        /// <param name="key">The key to identify the record.</param>
        /// <param name="value">The value to set in the record.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the transaction hash.</returns>
        public virtual async Task<string> SetRecordRequestAsync(TKey key, TValue value)
        {
            var table = new TTableRecord();
            table.Keys = key;
            table.Values = value;
            return await WorldService.SetRecordRequestAsync(table);
        }

        /// <summary>
        /// Deletes a record from the table asynchronously using the provided key.
        /// </summary>
        /// <param name="key">The key to identify the record.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the transaction hash.</returns>
        public virtual async Task<string> DeleteRecordRequestAsync(TKey key)
        {
            var table = new TTableRecord();
            table.Keys = key;
            return await WorldService.DeleteRecordRequestAsync(table);
        }

        /// <summary>
        /// Deletes a record from the table asynchronously using the provided key and waits for the transaction receipt.
        /// </summary>
        /// <param name="key">The key to identify the record.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the transaction receipt.</returns>
        public virtual async Task<TransactionReceipt> DeleteRecordRequestAndWaitForReceiptAsync(TKey key)
        {
            var table = new TTableRecord();
            table.Keys = key;
            return await WorldService.DeleteRecordRequestAndWaitForReceiptAsync(table);
        }

        /// <summary>
        /// Sets a record in the table asynchronously using the provided key and value, and waits for the transaction receipt.
        /// </summary>
        /// <param name="key">The key to identify the record.</param>
        /// <param name="value">The value to set in the record.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the transaction receipt.</returns>
        public virtual async Task<TransactionReceipt> SetRecordRequestAndWaitForReceiptAsync(TKey key, TValue value)
        {
            var table = new TTableRecord();
            table.Keys = key;
            table.Values = value;
            return await WorldService.SetRecordRequestAndWaitForReceiptAsync(table);
        }

        /// <summary>
        /// Sets a record in the table asynchronously and waits for the transaction receipt.
        /// </summary>
        /// <param name="table">The table record to set.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the transaction receipt.</returns>
        public virtual async Task<TransactionReceipt> SetRecordRequestAndWaitForReceiptAsync(TTableRecord table)
        {
            return await WorldService.SetRecordRequestAndWaitForReceiptAsync(table);
        }
    }
}
