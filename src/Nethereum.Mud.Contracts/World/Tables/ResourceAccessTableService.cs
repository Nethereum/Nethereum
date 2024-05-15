using System.Numerics;
using Nethereum.BlockchainProcessing.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Mud.Contracts.Core.Tables;
using static Nethereum.Mud.Contracts.World.Tables.ResourceAccessTableRecord;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Mud.Contracts.World;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Mud.Contracts.World.Tables
{
    public partial class ResourceAccessTableService : TableService<ResourceAccessTableRecord, ResourceAccessKey, ResourceAccessValue>
    {
      

        public ResourceAccessTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<ResourceAccessTableRecord> GetTableRecordAsync(byte[] resourceId, string caller, BlockParameter blockParameter = null)
        {
            var key = new ResourceAccessKey();
            key.ResourceId = resourceId;
            key.Caller = caller;
            return GetTableRecordAsync(key, blockParameter);
        }

        public virtual Task<string> SetRecordRequestAsync(byte[] resourceId, string caller, bool access)
        {
            var key = new ResourceAccessKey();
            key.ResourceId = resourceId;
            key.Caller = caller;
            var values = new ResourceAccessValue();
            values.Access = access;

            return SetRecordRequestAsync(key, values);
        }

        public virtual Task<TransactionReceipt> SetRecordRequestAndWaitForReceipt(byte[] resourceId, string caller, bool access)
        {
            var key = new ResourceAccessKey();
            key.ResourceId = resourceId;
            key.Caller = caller;
            var values = new ResourceAccessValue();
            values.Access = access;
            return SetRecordRequestAndWaitForReceiptAsync(key, values);
        }

    }
}
