using Nethereum.Mud.Contracts.World;
using static Nethereum.Mud.Contracts.Tables.World.SystemHooksTableRecord;
using Nethereum.RPC.Eth.DTOs;


namespace Nethereum.Mud.Contracts.Tables.World
{
    public static class SystemHooksWorldServiceExtensions
    {
        public static async Task<SystemHooksTableRecord> GetSystemHooksTableRecord(this WorldService worldService, SystemHooksKey key, BlockParameter blockParameter = null)
        {
            var table = new SystemHooksTableRecord();
            table.Keys = key;
            return await worldService.GetRecordTableQueryAsync<SystemHooksTableRecord, SystemHooksKey, SystemHooksValue>(table, blockParameter);
        }

        // SystemHooks may typically not have a set operation due to their potential complexity and size,
        // but if setting is supported, here is a generic example:
        public static async Task<string> SetSystemHooksTableRecordRequestAsync(this WorldService worldService, SystemHooksKey key, List<byte[]> value)
        {
            var table = new SystemHooksTableRecord();
            table.Keys = key;
            table.Values = new SystemHooksValue() { Value = value };
            return await worldService.SetRecordRequestAsync<SystemHooksKey, SystemHooksValue>(table);
        }
    }




}

