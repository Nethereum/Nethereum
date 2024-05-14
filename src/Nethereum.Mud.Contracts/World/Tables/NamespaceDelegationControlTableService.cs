using System.Numerics;
using Nethereum.BlockchainProcessing.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Mud.Contracts.Core.Tables;
using static Nethereum.Mud.Contracts.World.Tables.NamespaceDelegationControlTableRecord;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Mud.Contracts.World;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Mud.Contracts.World.Tables
{
    public partial class NamespaceDelegationControlTableService : TableService<NamespaceDelegationControlTableRecord, NamespaceDelegationControlKey, NamespaceDelegationControlValue>
    {
        public NamespaceDelegationControlTableService(WorldService worldService, StoreEventsLogProcessingService storeEventsLogProcessingService, RegistrationSystemService registrationSystemService) : base(worldService, storeEventsLogProcessingService, registrationSystemService)
        {
        }

        public NamespaceDelegationControlTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<NamespaceDelegationControlTableRecord> GetTableRecordAsync(byte[] namespaceId, BlockParameter blockParameter = null)
        {
            var key = new NamespaceDelegationControlKey();
            key.NamespaceId = namespaceId;
            return GetTableRecordAsync(key, blockParameter);
        }

        public virtual Task<string> SetRecordRequestAsync(byte[] namespaceId, byte[] DelegationControlId)
        {
            var key = new NamespaceDelegationControlKey();
            key.NamespaceId = namespaceId;
            var values = new NamespaceDelegationControlValue();
            values.DelegationControlId = DelegationControlId;
            return SetRecordRequestAsync(key, values);
        }

        public virtual Task<TransactionReceipt> SetRecordRequestAndWaitForReceipt(byte[] namespaceId, byte[] DelegationControlId)
        {
            var key = new NamespaceDelegationControlKey();
            key.NamespaceId = namespaceId;
            var values = new NamespaceDelegationControlValue();
            values.DelegationControlId = DelegationControlId;
            return SetRecordRequestAndWaitForReceiptAsync(key, values);
        }

    }
}
