using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem.ContractDefinition;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem.ContractDefinition;
using Nethereum.Mud.Contracts.World.Tables;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.Mud.Contracts.Core.Systems
{
    public interface ISystemServiceResourceRegistration
    {
        Task<string> BatchRegisterRootFunctionSelectorsRequestAsync(List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null, bool excludeDefaultSystemFunctions = true);
        Task<string> BatchRegisterSystemAndRootFunctionSelectorsRequestAsync(string deployedAddress, bool publicAccess = true, List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null, bool excludeDefaultSystemFunctions = true);
        List<RegisterRootFunctionSelectorFunction> CreateRegisterRootFunctionSelectors(List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null, bool excludeDefaultSystemFunctions = true);
        List<SystemCallData> CreateRegisterRootFunctionSelectorsBatchSystemCallData(List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null, bool excludeDefaultSystemFunctions = true);
        List<SystemCallData> CreateRegisterSystemAndRegisterRootFunctionSelectorsBatchSystemCallData(string deployedAddress, bool publicAccess, List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords, bool excludeDefaultSystemFunctions);
        RegisterSystemFunction GetRegisterSystemFunction(string deployedAddress, bool publicAccess = true);
        Task<TransactionReceipt> RegisterSystemAndWaitForReceiptAsync(string deployedAddress, bool publicAccess = true);
        Task<string> RegisterSystemAsync(string deployedAddress, bool publicAccess = true);
    }
}