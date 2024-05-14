using System.Numerics;
using Nethereum.BlockchainProcessing.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Mud.Contracts.Core.Tables;
using static Nethereum.Mud.Contracts.World.Tables.SystemHooksTableRecord;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Mud.Contracts.World;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Mud.Contracts.World.Tables
{
    public partial class SystemHooksTableService : TableService<SystemHooksTableRecord, SystemHooksKey, SystemHooksValue>
    {
        public SystemHooksTableService(WorldService worldService, StoreEventsLogProcessingService storeEventsLogProcessingService, RegistrationSystemService registrationSystemService) : base(worldService, storeEventsLogProcessingService, registrationSystemService)
        {
        }

        public SystemHooksTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<SystemHooksTableRecord> GetTableRecordAsync(byte[] systemId, BlockParameter blockParameter = null)
        {
            var key = new SystemHooksKey();
            key.SystemId = systemId;
            return GetTableRecordAsync(key, blockParameter);
        }

        public virtual Task<string> SetRecordRequestAsync(byte[] systemId, List<byte[]> value)
        {
            var key = new SystemHooksKey();
            key.SystemId = systemId;
            var values = new SystemHooksValue();
            values.Value = value;

            return SetRecordRequestAsync(key, values);
        }

        public virtual Task<TransactionReceipt> SetRecordRequestAndWaitForReceipt(byte[] systemId, List<byte[]> value)
        {
            var key = new SystemHooksKey();
            key.SystemId = systemId;
            var values = new SystemHooksValue();
            values.Value = value;

            return SetRecordRequestAndWaitForReceiptAsync(key, values);
        }

    }
}
