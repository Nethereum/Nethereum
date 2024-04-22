using Nethereum.Mud.Contracts.World;
using System.Numerics;
using static Nethereum.Mud.Contracts.Tables.World.BalancesTableRecord;
using Nethereum.RPC.Eth.DTOs;


namespace Nethereum.Mud.Contracts.Tables.World
{
    public static class BalancesWorldServiceExtensions
    {
        public static async Task<BalancesTableRecord> GetBalancesTableRecord(this WorldService worldService, BalancesKey key, BlockParameter blockParameter = null)
        {
            var table = new BalancesTableRecord();
            table.Keys = key;
            return await worldService.GetRecordTableQueryAsync<BalancesTableRecord, BalancesKey, BalancesValue>(table, blockParameter);
        }

        public static async Task<string> SetBalancesTableRecordRequestAsync(this WorldService worldService, BalancesKey key, BigInteger balance)
        {
            var table = new BalancesTableRecord();
            table.Keys = key;
            table.Values = new BalancesValue() { Balance = balance };
            return await worldService.SetRecordRequestAsync<BalancesKey, BalancesValue>(table);
        }
    }




}

