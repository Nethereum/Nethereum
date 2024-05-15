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

        public virtual async Task<TTableRecord> GetTableRecordAsync(TKey key, BlockParameter blockParameter = null)
        {
            var table = new TTableRecord();
            table.Keys = key;
            return await WorldService.GetRecordTableQueryAsync<TTableRecord, TKey, TValue>(table, blockParameter);
        }

        public virtual async Task<string> SetRecordRequestAsync(TTableRecord table)
        {
            return await WorldService.SetRecordRequestAsync(table);
        }

        public virtual async Task<string> SetRecordRequestAsync(TKey key, TValue value)
        {
            var table = new TTableRecord();
            table.Keys = key;
            table.Values = value;
            return await WorldService.SetRecordRequestAsync(table);
        }

        public virtual async Task<TransactionReceipt> SetRecordRequestAndWaitForReceiptAsync(TKey key, TValue value)
        {
            var table = new TTableRecord();
            table.Keys = key;
            table.Values = value;
            return await WorldService.SetRecordRequestAndWaitForReceiptAsync(table);
        }

        public virtual async Task<TransactionReceipt> SetRecordRequestAndWaitForReceiptAsync(TTableRecord table)
        {
            return await WorldService.SetRecordRequestAndWaitForReceiptAsync(table);
        }

       

    }
}
