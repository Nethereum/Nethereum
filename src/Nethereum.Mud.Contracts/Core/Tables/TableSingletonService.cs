using Nethereum.Mud.Contracts.World;
using Nethereum.Web3;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Mud.Contracts.Core.StoreEvents;

namespace Nethereum.Mud.Contracts.Core.Tables
{
    public abstract class TableSingletonService<TTableRecordSingleton, TValue> : TableServiceBase<TTableRecordSingleton, TValue>
        where TTableRecordSingleton : TableRecordSingleton<TValue>, new()
        where TValue : class, new()
    {

        protected TableSingletonService(WorldService worldService, StoreEventsLogProcessingService storeEventsLogProcessingService, RegistrationSystemService registrationSystemService) : base(worldService, storeEventsLogProcessingService, registrationSystemService)
        {

        }

        public TableSingletonService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {

        }

        public virtual async Task<TTableRecordSingleton> GetTableRecordAsync(BlockParameter blockParameter = null)
        {
            return await WorldService.GetRecordTableQueryAsync<TTableRecordSingleton, TValue>(blockParameter);
        }

        public virtual async Task<string> SetRecordRequestAsync(TValue value)
        {
            var table = new TTableRecordSingleton();
            table.Values = value;
            return await WorldService.SetRecordRequestAsync(table);
        }
        public virtual async Task<TransactionReceipt> SetRecordRequestAndWaitForReceiptAsync(TValue value)
        {
            var table = new TTableRecordSingleton();
            table.Values = value;
            return await WorldService.SetRecordRequestAndWaitForReceiptAsync(table);
        }

    }
}
