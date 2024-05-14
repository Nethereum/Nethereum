using System.Numerics;
using Nethereum.BlockchainProcessing.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Mud.Contracts.Core.Tables;
using static Nethereum.Mud.Contracts.World.Tables.UserDelegationControlTableRecord;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Mud.Contracts.World;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Mud.Contracts.World.Tables
{
    public partial class UserDelegationControlTableService : TableService<UserDelegationControlTableRecord, UserDelegationControlKey, UserDelegationControlValue>
    {
        public UserDelegationControlTableService(WorldService worldService, StoreEventsLogProcessingService storeEventsLogProcessingService, RegistrationSystemService registrationSystemService) : base(worldService, storeEventsLogProcessingService, registrationSystemService)
        {
        }

        public UserDelegationControlTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<UserDelegationControlTableRecord> GetTableRecordAsync(string delegator, string delegatee, BlockParameter blockParameter = null)
        {
            var key = new UserDelegationControlKey();
            key.Delegator = delegator;
            key.Delegatee = delegatee;
            return GetTableRecordAsync(key, blockParameter);
        }

        public virtual Task<string> SetRecordRequestAsync(string delegator, string delegatee, byte[] delegationControlId)
        {
            var key = new UserDelegationControlKey();
            key.Delegator = delegator;
            key.Delegatee = delegatee;
            var values = new UserDelegationControlValue();
            values.DelegationControlId = delegationControlId;

            return SetRecordRequestAsync(key, values);
        }

        public virtual Task<TransactionReceipt> SetRecordRequestAndWaitForReceipt(string delegator, string delegatee, byte[] delegationControlId)
        {
            var key = new UserDelegationControlKey();
            key.Delegator = delegator;
            key.Delegatee = delegatee;
            var values = new UserDelegationControlValue();
            values.DelegationControlId = delegationControlId;

            return SetRecordRequestAndWaitForReceiptAsync(key, values);
        }

    }
}
