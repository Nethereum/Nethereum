using Nethereum.Web3;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem.ContractDefinition;
using Nethereum.Mud.Contracts.World;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;

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

        public async Task<string> BatchRegisterAllTablesRequestAsync(params IResource[] excludedTables)
        {
            var batchSystemCallData = new List<SystemCallData>();
            foreach (var tableService in TableServices)
            {
                if (excludedTables != null && 
                    excludedTables.Any(x => tableService.Resource.ResourceIdEncoded.ToHex().IsTheSameHex(x.ResourceIdEncoded.ToHex())))
                {
                    continue;
                }
                batchSystemCallData.Add(tableService.GetRegisterTableFunctionBatchSystemCallData());
            }
            return await BatchCallSystem.BatchCallRequestAsync(batchSystemCallData);
        }
         
        public async Task<TransactionReceipt> BatchRegisterAllTablesRequestAndWaitForReceiptAsync(params IResource[] excludedTables)
        {
            var batchSystemCallData = new List<SystemCallData>();
            foreach (var tableService in TableServices)
            {
                if(excludedTables != null && 
                   excludedTables.Any(x => tableService.Resource.ResourceIdEncoded.ToHex().IsTheSameHex(x.ResourceIdEncoded.ToHex())))
                {
                    continue;
                }
                batchSystemCallData.Add(tableService.GetRegisterTableFunctionBatchSystemCallData());
            }
            return await BatchCallSystem.BatchCallRequestAndWaitForReceiptAsync(batchSystemCallData);
        }

        public async Task<string> BatchRegisterTablesAsync(params IResource[] tables)
        {
            var batchSystemCallData = new List<SystemCallData>();

            
            foreach (var table in tables)
            {
                var tableService = TableServices.FirstOrDefault(x => x.Resource.ResourceIdEncoded.ToHex().IsTheSameHex(table.ResourceIdEncoded.ToHex()));
                if (tableService != null)
                {
                    batchSystemCallData.Add(tableService.GetRegisterTableFunctionBatchSystemCallData());
                }
                else
                {
                    throw new System.Exception($"Table {table.Name} not found in the TableServices");
                }
            }
            return await BatchCallSystem.BatchCallRequestAsync(batchSystemCallData);
        }

        public async Task<TransactionReceipt> BatchRegisterTablesAndWaitForReceiptAsync(params IResource[] tables)
        {
            var batchSystemCallData = new List<SystemCallData>();


            foreach (var table in tables)
            {
                var tableService = TableServices.FirstOrDefault(x => x.Resource.ResourceIdEncoded.ToHex().IsTheSameHex(table.ResourceIdEncoded.ToHex()));
                if (tableService != null)
                {
                    batchSystemCallData.Add(tableService.GetRegisterTableFunctionBatchSystemCallData());
                }
                else
                {
                    throw new System.Exception($"Table {table.Name} not found in the TableServices");
                }
            }
            return await BatchCallSystem.BatchCallRequestAndWaitForReceiptAsync(batchSystemCallData);
        }
    }
}
