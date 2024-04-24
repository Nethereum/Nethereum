using Nethereum.Mud.Contracts.World;
using Nethereum.RPC.Eth.DTOs;
using static Nethereum.Mud.Contracts.Tables.World.SystemRegistryTableRecord;
using static Nethereum.Mud.Contracts.Tables.World.SystemRegistryTableRecord.SystemRegistryKey;


namespace Nethereum.Mud.Contracts.Tables.World
{
    public static class SystemRegistryWorldServiceExtensions
    {
        public static async Task<SystemRegistryTableRecord> GetSystemRegistryTableRecord(this WorldService worldService, SystemRegistryKey key, BlockParameter blockParameter = null)
        {
            var table = new SystemRegistryTableRecord();
            table.Keys = key;
            return await worldService.GetRecordTableQueryAsync<SystemRegistryTableRecord, SystemRegistryKey, SystemRegistryValue>(table, blockParameter);
        }

        public static async Task<string> SetSystemRegistryTableRecordRequestAsync(this WorldService worldService, SystemRegistryKey key, byte[] systemId)
        {
            var table = new SystemRegistryTableRecord();
            table.Keys = key;
            table.Values = new SystemRegistryValue() { SystemId = systemId };
            return await worldService.SetRecordRequestAsync<SystemRegistryKey, SystemRegistryValue>(table);
        }
    }




}

