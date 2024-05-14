using Nethereum.Mud.Contracts.World;
using Nethereum.RPC.Eth.DTOs;
using static Nethereum.Mud.Contracts.Store.Tables.StoreHooksTableRecord;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Mud.Contracts.Core.Tables;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Web3;

namespace Nethereum.Mud.Contracts.Store.Tables
{
    public partial class StoreHooksTableService : TableService<StoreHooksTableRecord, StoreHooksKey, StoreHooksValue>
    {
        public StoreHooksTableService(WorldService worldService, StoreEventsLogProcessingService storeEventsLogProcessingService, RegistrationSystemService registrationSystemService) : base(worldService, storeEventsLogProcessingService, registrationSystemService)
        {

        }

        public StoreHooksTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {

        }

        public virtual Task<StoreHooksTableRecord> GetTableRecordAsync(byte[] tableId, BlockParameter blockParameter = null)
        {
            var key = new StoreHooksKey();
            key.TableId = tableId;
            return GetTableRecordAsync(key, blockParameter);
        }

        public virtual Task<string> SetRecordRequestAsync(byte[] tableId, List<byte[]> hooks)
        {
            var key = new StoreHooksKey();
            key.TableId = tableId;
            var table = new StoreHooksTableRecord();
            table.Keys = key;
            table.Values = new StoreHooksValue();
            table.Values.Hooks = hooks;
            return SetRecordRequestAsync(key, table.Values);
        }

        public virtual Task<TransactionReceipt> SetRecordRequestAndWaitForReceipt(byte[] tableId, List<byte[]> hooks)
        {
            var key = new StoreHooksKey();
            key.TableId = tableId;
            var table = new StoreHooksTableRecord();
            table.Keys = key;
            table.Values = new StoreHooksValue();
            table.Values.Hooks = hooks;
            return SetRecordRequestAndWaitForReceiptAsync(key, table.Values);
        }
    }
}


