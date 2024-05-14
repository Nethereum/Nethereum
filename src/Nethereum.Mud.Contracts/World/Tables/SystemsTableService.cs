using System.Numerics;
using Nethereum.BlockchainProcessing.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Mud.Contracts.Core.Tables;
using static Nethereum.Mud.Contracts.World.Tables.SystemsTableRecord;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Mud.Contracts.World;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Mud.Contracts.World.Tables
{
    public partial class SystemsTableService : TableService<SystemsTableRecord, SystemsKey, SystemsValue>
    {
        public SystemsTableService(WorldService worldService, StoreEventsLogProcessingService storeEventsLogProcessingService, RegistrationSystemService registrationSystemService) : base(worldService, storeEventsLogProcessingService, registrationSystemService)
        {
        }

        public SystemsTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<SystemsTableRecord> GetTableRecordAsync(byte[] systemId, BlockParameter blockParameter = null)
        {
            var key = new SystemsKey();
            key.SystemId = systemId;
            return GetTableRecordAsync(key, blockParameter);
        }

        public virtual Task<string> SetRecordRequestAsync(byte[] systemId, string system, bool publicAccess)
        {
            var key = new SystemsKey();
            key.SystemId = systemId;
            var values = new SystemsValue();
            values.System = system;
            values.PublicAccess = publicAccess;
            return SetRecordRequestAsync(key, values);
        }

        public virtual Task<TransactionReceipt> SetRecordRequestAndWaitForReceipt(byte[] systemId, string system, bool publicAccess)
        {
            var key = new SystemsKey();
            key.SystemId = systemId;
            var values = new SystemsValue();
            values.System = system;
            values.PublicAccess = publicAccess;
            return SetRecordRequestAndWaitForReceiptAsync(key, values);
        }

    }
}
