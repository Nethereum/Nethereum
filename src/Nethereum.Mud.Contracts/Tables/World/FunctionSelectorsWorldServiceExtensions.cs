using Nethereum.Mud.Contracts.World;
using Nethereum.RPC.Eth.DTOs;
using static Nethereum.Mud.Contracts.Tables.World.FunctionSelectorsTableRecord;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;


namespace Nethereum.Mud.Contracts.Tables.World
{
    public static class FunctionSelectorsWorldServiceExtensions
    {
        public static async Task<FunctionSelectorsTableRecord> GetFunctionSelectorsTableRecord(this WorldService worldService, FunctionSelectorsKey key, BlockParameter blockParameter = null)
        {
            var table = new FunctionSelectorsTableRecord();
            table.Keys = key;
            return await worldService.GetRecordTableQueryAsync<FunctionSelectorsTableRecord, FunctionSelectorsKey, FunctionSelectorsValue>(table, blockParameter);
        }

        // Assuming setting is supported:
        public static async Task<string> SetFunctionSelectorsTableRecordRequestAsync(this WorldService worldService, FunctionSelectorsKey key, byte[] systemId, byte[] systemFunctionSelector)
        {
            var table = new FunctionSelectorsTableRecord();
            table.Keys = key;
            table.Values = new FunctionSelectorsValue() { SystemId = systemId, SystemFunctionSelector = systemFunctionSelector };
            return await worldService.SetRecordRequestAsync<FunctionSelectorsKey, FunctionSelectorsValue>(table);
        }
    }




}

