using Nethereum.Mud.Contracts.World;
using Nethereum.RPC.Eth.DTOs;
using static Nethereum.Mud.Contracts.Tables.World.NamespaceDelegationControlTableRecord;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;


namespace Nethereum.Mud.Contracts.Tables.World
{
    public static class NamespaceDelegationControlWorldServiceExtensions
    {
        public static async Task<NamespaceDelegationControlTableRecord> GetNamespaceDelegationControlTableRecord(this WorldService worldService, NamespaceDelegationControlKey key, BlockParameter blockParameter = null)
        {
            var table = new NamespaceDelegationControlTableRecord();
            table.Keys = key;
            return await worldService.GetRecordTableQueryAsync<NamespaceDelegationControlTableRecord, NamespaceDelegationControlKey, NamespaceDelegationControlValue>(table, blockParameter);
        }

        public static async Task<string> SetNamespaceDelegationControlTableRecordRequestAsync(this WorldService worldService, NamespaceDelegationControlKey key, byte[] delegationControlId)
        {
            var table = new NamespaceDelegationControlTableRecord();
            table.Keys = key;
            table.Values = new NamespaceDelegationControlValue() { DelegationControlId = delegationControlId };
            return await worldService.SetRecordRequestAsync<NamespaceDelegationControlKey, NamespaceDelegationControlValue>(table);
        }
    }




}

