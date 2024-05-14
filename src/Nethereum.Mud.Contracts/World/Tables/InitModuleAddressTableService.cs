using System.Numerics;
using Nethereum.BlockchainProcessing.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Mud.Contracts.Core.Tables;
using static Nethereum.Mud.Contracts.World.Tables.InitModuleAddressTableRecord;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Mud.Contracts.World;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Mud.Contracts.World.Tables
{
    public partial class InitModuleAddressTableService : TableSingletonService<InitModuleAddressTableRecord, InitModuleAddressValue>
    {
        public InitModuleAddressTableService(WorldService worldService, StoreEventsLogProcessingService storeEventsLogProcessingService, RegistrationSystemService registrationSystemService) : base(worldService, storeEventsLogProcessingService, registrationSystemService)
        {
        }

        public InitModuleAddressTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

      
        public virtual Task<string> SetRecordRequestAsync(string address)
        {
            var values = new InitModuleAddressValue();
            values.Value = address;

            return SetRecordRequestAsync(values);
        }

        public virtual Task<TransactionReceipt> SetRecordRequestAndWaitForReceipt(string address)
        {
            var values = new InitModuleAddressValue();
            values.Value = address;
            return SetRecordRequestAndWaitForReceiptAsync(values);
        }
    }
}
