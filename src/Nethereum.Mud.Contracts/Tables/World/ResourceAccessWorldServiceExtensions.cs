using static Nethereum.Mud.Contracts.Tables.World.ResourceAccessTableRecord;
using Nethereum.Mud.Contracts.World;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Mud.Contracts.Tables.World
{
    public static class  ResourceAccessWorldServiceExtensions
    {
        public static async Task<ResourceAccessTableRecord> GetResourceAccessRecordQueryAsync(this WorldService worldService, byte[] resourceId, string caller, BlockParameter blockParameter = null)
        {
            var resourceAccessTable = new ResourceAccessTableRecord();
            resourceAccessTable.Keys = new ResourceAccessKey() { ResourceId = resourceId, Caller = caller };
            return await worldService.GetRecordTableQueryAsync<ResourceAccessTableRecord, ResourceAccessKey, ResourceAccessValue>(resourceAccessTable, blockParameter);
        }

        public static async Task<ResourceAccessTableRecord> GetResourceAccessRecordQueryAsync(this WorldService worldService, ResourceAccessKey resourceAccessKey, BlockParameter blockParameter = null)
        {
            var resourceAccessTable = new ResourceAccessTableRecord();
            resourceAccessTable.Keys = resourceAccessKey;
            return await worldService.GetRecordTableQueryAsync<ResourceAccessTableRecord, ResourceAccessKey, ResourceAccessValue>(resourceAccessTable, blockParameter);
        }

        public static async Task<string> SetResourceAccessRequestAsync(this WorldService worldService, byte[] resourceId, string caller, bool access)
        {
            var resourceAccessTable = new ResourceAccessTableRecord();
            resourceAccessTable.Keys = new ResourceAccessKey() { ResourceId = resourceId, Caller = caller };
            resourceAccessTable.Values = new ResourceAccessValue() { Access = access };
            return await worldService.SetRecordRequestAsync(resourceAccessTable);
        }

        public static async Task<string> SetResourceAccessRequestAsync(this WorldService worldService, ResourceAccessKey resourceAccessKey, ResourceAccessValue resourceAccessValue)
        {
            var resourceAccessTable = new ResourceAccessTableRecord();
            resourceAccessTable.Keys = resourceAccessKey;
            resourceAccessTable.Values = resourceAccessValue;
            return await worldService.SetRecordRequestAsync(resourceAccessTable);
        }

        public static async Task<string> SetResourceAccessRequestAsync(this WorldService worldService, ResourceAccessTableRecord resourceAccessTable)
        {
            return await worldService.SetRecordRequestAsync(resourceAccessTable);
        }

    }
}
