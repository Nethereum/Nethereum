using System.Numerics;
using Nethereum.BlockchainProcessing.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Mud.Contracts.Core.Tables;
using static Nethereum.Mud.Contracts.World.Tables.FunctionSelectorsTableRecord;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Mud.Contracts.World;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Mud.Contracts.World.Tables
{

    public partial class FunctionSelectorsTableService : TableService<FunctionSelectorsTableRecord, FunctionSelectorsKey, FunctionSelectorsValue>
    {
       

        public FunctionSelectorsTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<FunctionSelectorsTableRecord> GetTableRecordAsync(byte[] worldFunctionSelector, BlockParameter blockParameter = null)
        {
            var key = new FunctionSelectorsKey();
            key.WorldFunctionSelector = worldFunctionSelector;
            return GetTableRecordAsync(key, blockParameter);
        }

        public virtual Task<string> SetRecordRequestAsync(byte[] worldFunctionSelector, byte[] systemId, byte[] systemFunctionSelector)
        {
            var key = new FunctionSelectorsKey();
            key.WorldFunctionSelector = worldFunctionSelector;
            var values = new FunctionSelectorsValue();
            values.SystemId = systemId;
            values.SystemFunctionSelector = systemFunctionSelector;
            return SetRecordRequestAsync(key, values);
        }

        public virtual Task<TransactionReceipt> SetRecordRequestAndWaitForReceipt(byte[] worldFunctionSelector, byte[] systemId, byte[] systemFunctionSelector)
        {
            var key = new FunctionSelectorsKey();
            key.WorldFunctionSelector = worldFunctionSelector;
            var values = new FunctionSelectorsValue();
            values.SystemId = systemId;
            values.SystemFunctionSelector = systemFunctionSelector;
            return SetRecordRequestAndWaitForReceiptAsync(key, values);
        }
    }

}
