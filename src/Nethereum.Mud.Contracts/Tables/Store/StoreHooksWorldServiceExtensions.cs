using Nethereum.Mud.Contracts.World;
using Nethereum.RPC.Eth.DTOs;
using static Nethereum.Mud.Contracts.Tables.Store.StoreHooksTableRecord;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;


namespace Nethereum.Mud.Contracts.Tables.Store
{
    public static class StoreHooksWorldServiceExtensions
    {
        public static async Task<StoreHooksTableRecord> GetStoreHooksTableRecord(this WorldService storeService, StoreHooksKey key, BlockParameter blockParameter = null)
        {
            var table = new StoreHooksTableRecord();
            table.Keys = key;
            return await storeService.GetRecordTableQueryAsync<StoreHooksTableRecord, StoreHooksKey, StoreHooksValue>(table, blockParameter);
        }

        public static async Task<string> SetStoreHooksTableRecordRequestAsync(this WorldService storeService, StoreHooksKey key, List<byte[]> hooks)
        {
            var table = new StoreHooksTableRecord();
            table.Keys = key;
            table.Values = new StoreHooksValue() { Hooks = hooks };
            return await storeService.SetRecordRequestAsync<StoreHooksKey, StoreHooksValue>(table);
        }
    }
}


