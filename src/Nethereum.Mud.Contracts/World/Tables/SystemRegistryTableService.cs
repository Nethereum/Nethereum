using System.Numerics;
using Nethereum.BlockchainProcessing.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Mud.Contracts.Core.Tables;
using static Nethereum.Mud.Contracts.World.Tables.SystemRegistryTableRecord;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Mud.Contracts.World;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Mud.Contracts.World.Tables
{
    public partial class SystemRegistryTableService : TableService<SystemRegistryTableRecord, SystemRegistryKey, SystemRegistryValue>
    {
        public SystemRegistryTableService(WorldService worldService, StoreEventsLogProcessingService storeEventsLogProcessingService, RegistrationSystemService registrationSystemService) : base(worldService, storeEventsLogProcessingService, registrationSystemService)
        {
        }

        public SystemRegistryTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<SystemRegistryTableRecord> GetTableRecordAsync(string system, BlockParameter blockParameter = null)
        {
            var key = new SystemRegistryKey();
            key.System = system;
            return GetTableRecordAsync(key, blockParameter);
        }

        public virtual Task<string> SetRecordRequestAsync(string system, byte[] systemId)
        {
            var key = new SystemRegistryKey();
            key.System = system;
            var values = new SystemRegistryValue();
            values.SystemId = systemId;
            return SetRecordRequestAsync(key, values);
        }

        public virtual Task<TransactionReceipt> SetRecordRequestAndWaitForReceipt(string system, byte[] systemId)
        {
            var key = new SystemRegistryKey();
            key.System = system;
            var values = new SystemRegistryValue();
            values.SystemId = systemId;
            return SetRecordRequestAndWaitForReceiptAsync(key, values);
        }

    }
}
