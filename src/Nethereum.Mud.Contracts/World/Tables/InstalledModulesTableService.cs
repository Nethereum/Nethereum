using System.Numerics;
using Nethereum.BlockchainProcessing.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Mud.Contracts.Core.Tables;
using static Nethereum.Mud.Contracts.World.Tables.InstalledModulesTableRecord;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Mud.Contracts.World;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Mud.Contracts.World.Tables
{
    public partial class InstalledModulesTableService : TableService<InstalledModulesTableRecord, InstalledModulesKey, InstalledModulesValue>
    {
        public InstalledModulesTableService(WorldService worldService, StoreEventsLogProcessingService storeEventsLogProcessingService, RegistrationSystemService registrationSystemService) : base(worldService, storeEventsLogProcessingService, registrationSystemService)
        {
        }

        public InstalledModulesTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<InstalledModulesTableRecord> GetTableRecordAsync(string moduleAddress, byte[] argumentsHash, BlockParameter blockParameter = null)
        {
            var key = new InstalledModulesKey();
            key.ModuleAddress = moduleAddress;
            key.ArgumentsHash = argumentsHash;
            return GetTableRecordAsync(key, blockParameter);
        }

        public virtual Task<string> SetRecordRequestAsync(string moduleAddress, byte[] argumentsHash, bool isInstalled)
        {
            var key = new InstalledModulesKey();
            key.ModuleAddress = moduleAddress;
            key.ArgumentsHash = argumentsHash;
            var values = new InstalledModulesValue();
            values.IsInstalled = isInstalled;

            return SetRecordRequestAsync(key, values);
        }

        public virtual Task<TransactionReceipt> SetRecordRequestAndWaitForReceipt(string moduleAddress, byte[] argumentsHash, bool isInstalled)
        {
            var key = new InstalledModulesKey();
            key.ModuleAddress = moduleAddress;
            key.ArgumentsHash = argumentsHash;
            var values = new InstalledModulesValue();
            values.IsInstalled = isInstalled;
            return SetRecordRequestAndWaitForReceiptAsync(key, values);
        }

    }
}
