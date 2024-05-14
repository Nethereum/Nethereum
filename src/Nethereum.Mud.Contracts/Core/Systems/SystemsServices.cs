using Nethereum.Web3;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem.ContractDefinition;
using Nethereum.Mud.Contracts.World.Tables;

namespace Nethereum.Mud.Contracts.Core.Systems
{
    public abstract class SystemsServices
    {
        protected BatchCallSystemService BatchCallSystem { get; set; }

        public List<ISystemService> SystemServices { get; protected set; }

        public SystemsServices(IWeb3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractAddress = contractAddress;
            BatchCallSystem = new BatchCallSystemService(web3, contractAddress);
        }

        public IWeb3 Web3 { get; protected set; }
        public string ContractAddress { get; protected set; }

        public async Task<string> BatchRegisterAllSystemsRequestAsync(string deployedAddress, bool publicAccess = true, List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null, bool excludeDefaultSystemFunctions = true)
        {
            var systemCallData = new List<SystemCallData>();
            foreach (var systemService in SystemServices)
            {
                var registrator = systemService.SystemServiceResourceRegistrator;
                var callData = registrator.CreateRegisterSystemAndRegisterRootFunctionSelectorsBatchSystemCallData(deployedAddress, publicAccess, excludedFunctionSelectorRecords, excludeDefaultSystemFunctions);
                systemCallData.AddRange(callData);
            }
            return await BatchCallSystem.BatchCallRequestAsync(systemCallData);
        }

        public async Task<TransactionReceipt> BatchRegisterAllSystemsRequestAndWaitForReceiptAsync(string deployedAddress, bool publicAccess = true, List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null, bool excludeDefaultSystemFunctions = true)
        {
            var systemCallData = new List<SystemCallData>();
            foreach (var systemService in SystemServices)
            {
                var registrator = systemService.SystemServiceResourceRegistrator;
                var callData = registrator.CreateRegisterSystemAndRegisterRootFunctionSelectorsBatchSystemCallData(deployedAddress, publicAccess, excludedFunctionSelectorRecords, excludeDefaultSystemFunctions);
                systemCallData.AddRange(callData);
            }
            return await BatchCallSystem.BatchCallRequestAndWaitForReceiptAsync(systemCallData);
        }
    }
}
