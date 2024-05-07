using Nethereum.Mud.Contracts.World;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;
using static Nethereum.Mud.Contracts.Tables.World.UserDelegationControlTableRecord;


namespace Nethereum.Mud.Contracts.Tables.World
{
    public static class UserDelegationControlWorldServiceExtensions
    {
        public static async Task<UserDelegationControlTableRecord> GetUserDelegationControlTableRecord(this WorldService worldService, UserDelegationControlKey key, BlockParameter blockParameter = null)
        {
            var table = new UserDelegationControlTableRecord();
            table.Keys = key;
            return await worldService.GetRecordTableQueryAsync<UserDelegationControlTableRecord, UserDelegationControlKey, UserDelegationControlValue>(table, blockParameter);
        }

        public static async Task<string> SetUserDelegationControlTableRecordRequestAsync(this WorldService worldService, UserDelegationControlKey key, byte[] delegationControlId)
        {
            var table = new UserDelegationControlTableRecord();
            table.Keys = key;
            table.Values = new UserDelegationControlValue() { DelegationControlId = delegationControlId };
            return await worldService.SetRecordRequestAsync<UserDelegationControlKey, UserDelegationControlValue>(table);
        }
    }




}

