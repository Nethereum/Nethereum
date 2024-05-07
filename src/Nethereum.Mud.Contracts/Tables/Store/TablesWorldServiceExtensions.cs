using static Nethereum.Mud.Contracts.Tables.Store.TablesTableRecord;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;
using Nethereum.Mud.Contracts.World;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace Nethereum.Mud.Contracts.Tables.Store
{
    public static class TablesWorldServiceExtensions
    {
        public static async Task<TablesTableRecord> GetTablesTableRecord(this WorldService storeService, TablesKey key, BlockParameter blockParameter = null)
        {
            var table = new TablesTableRecord();
            table.Keys = key;
            return await storeService.GetRecordTableQueryAsync<TablesTableRecord, TablesKey, TablesValue>(table, blockParameter);
        }

    }
}

