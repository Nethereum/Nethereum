using Nethereum.Mud.Contracts.World;
using Nethereum.RPC.Eth.DTOs;
using static Nethereum.Mud.Contracts.Tables.World.SystemsTableRecord;


namespace Nethereum.Mud.Contracts.Tables.World
{
    public static class SystemsWorldServiceExtensions
    {
        public static async Task<SystemsTableRecord> GetSystemsTableRecord(this WorldService worldService, SystemsKey key, BlockParameter blockParameter = null)
        {
            var table = new SystemsTableRecord();
            table.Keys = key;
            return await worldService.GetRecordTableQueryAsync<SystemsTableRecord, SystemsKey, SystemsValue>(table, blockParameter);
        }

        public static async Task<string> SetSystemsTableRecordRequestAsync(this WorldService worldService, SystemsKey key, string system, bool publicAccess)
        {
            var table = new SystemsTableRecord();
            table.Keys = key;
            table.Values = new SystemsValue() { System = system, PublicAccess = publicAccess };
            return await worldService.SetRecordRequestAsync<SystemsKey, SystemsValue>(table);
        }
    }




}

