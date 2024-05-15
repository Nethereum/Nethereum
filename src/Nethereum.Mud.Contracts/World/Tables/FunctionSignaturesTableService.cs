using System.Numerics;
using Nethereum.BlockchainProcessing.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Mud.Contracts.Core.Tables;
using static Nethereum.Mud.Contracts.World.Tables.FunctionSignaturesTableRecord;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Mud.Contracts.World;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Mud.Contracts.World.Tables
{

    public partial class FunctionSignaturesTableService : TableService<FunctionSignaturesTableRecord, FunctionSignaturesKey, FunctionSignaturesValue>
    {
       
        public FunctionSignaturesTableService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public virtual Task<FunctionSignaturesTableRecord> GetTableRecordAsync(byte[] functionSelector, BlockParameter blockParameter = null)
        {
            var key = new FunctionSignaturesKey();
            key.FunctionSelector = functionSelector;
            return GetTableRecordAsync(key, blockParameter);
        }

        public virtual Task<string> SetRecordRequestAsync(byte[] functionSelector, string functionSignature)
        {
            var key = new FunctionSignaturesKey();
            key.FunctionSelector = functionSelector;
            var values = new FunctionSignaturesValue();
            values.FunctionSignature = functionSignature;

            return SetRecordRequestAsync(key, values);
        }

        public virtual Task<TransactionReceipt> SetRecordRequestAndWaitForReceipt(byte[] functionSelector, string functionSignature)
        {
            var key = new FunctionSignaturesKey();
            key.FunctionSelector = functionSelector;
            var values = new FunctionSignaturesValue();
            values.FunctionSignature = functionSignature;

            return SetRecordRequestAndWaitForReceiptAsync(key, values);
        }
    }
}
