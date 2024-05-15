using Nethereum.Web3;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem.ContractDefinition;
using Nethereum.Mud.Contracts.World;

namespace Nethereum.Mud.Contracts.Core.Tables
{
    public abstract class TablesServices
    {
        protected BatchCallSystemService BatchCallSystem { get; set; }

        public List<ITableServiceBase> TableServices { get; protected set; }

        public TablesServices(IWeb3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractAddress = contractAddress;
            BatchCallSystem = new BatchCallSystemService(web3, contractAddress);
        }

        public IWeb3 Web3 { get; protected set; }
        public string ContractAddress { get; protected set; }

        public async Task<string> BatchRegisterAllTablesRequestAsync()
        {
            var batchSystemCallData = new List<SystemCallData>();
            foreach (var tableService in TableServices)
            {
                batchSystemCallData.Add(tableService.GetRegisterTableFunctionBatchSystemCallData());
            }
            return await BatchCallSystem.BatchCallRequestAsync(batchSystemCallData);
        }

        public async Task<TransactionReceipt> BatchRegisterAllTablesRequestAndWaitForReceiptAsync()
        {
            var batchSystemCallData = new List<SystemCallData>();
            foreach (var tableService in TableServices)
            {
                batchSystemCallData.Add(tableService.GetRegisterTableFunctionBatchSystemCallData());
            }
            return await BatchCallSystem.BatchCallRequestAndWaitForReceiptAsync(batchSystemCallData);
        }
    }
}
