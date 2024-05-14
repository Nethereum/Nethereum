using System.Numerics;
using Nethereum.BlockchainProcessing.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Mud.Contracts.Core.Tables;
using static Nethereum.Mud.Contracts.Store.Tables.ResourceIdsTableRecord;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Mud.Contracts.World;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs; 

namespace Nethereum.Mud.Contracts.Store.Tables
{

    public partial class ResourceIdsTableService : TableService<ResourceIdsTableRecord, ResourceIdsKey, ResourceIdsValue>
    {
        public ResourceIdsTableService(WorldService worldService, StoreEventsLogProcessingService storeEventsLogProcessingService, RegistrationSystemService registrationSystemService) : base(worldService, storeEventsLogProcessingService, registrationSystemService)
        {

        }

        public ResourceIdsTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
           
        }

        public virtual Task<ResourceIdsTableRecord> GetTableRecordAsync(byte[] resourceId, BlockParameter blockParameter = null)
        {
            var key = new ResourceIdsKey();
            key.ResourceId = resourceId;
            return GetTableRecordAsync(key, blockParameter);
        }

        public virtual Task<string> SetRecordRequestAsync(byte[] resourceId, bool exists = true)
        {
            var key = new ResourceIdsKey();
            key.ResourceId = resourceId;
            var table = new ResourceIdsTableRecord();
            table.Keys = key;
            table.Values = new ResourceIdsValue();
            table.Values.Exists = exists;
            return SetRecordRequestAsync(key, table.Values);
        }

        public virtual Task<TransactionReceipt> SetRecordRequestAndWaitForReceipt(byte[] resourceId, bool exists = true)
        {
            var key = new ResourceIdsKey();
            key.ResourceId = resourceId;
            var table = new ResourceIdsTableRecord();
            table.Keys = key;
            table.Values = new ResourceIdsValue();
            table.Values.Exists = exists;
            return SetRecordRequestAndWaitForReceiptAsync(key, table.Values);
        }
    }

}

