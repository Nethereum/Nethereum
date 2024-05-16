using Nethereum.ABI.Model;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem.ContractDefinition;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem.ContractDefinition;
using Nethereum.Mud.Contracts.World.Tables;
using Nethereum.Web3;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Mud.Contracts.Core.Systems
{
    public class SystemRegistrationBuilder
    {
        public List<RegisterRootFunctionSelectorFunction> CreateRegisterRootFunctionSelectors<TSystemResource>(ContractServiceBase systemService, List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null, bool excludeDefaultSystemFunctions = true)
           where TSystemResource : SystemResource, new()
        {
            return CreateRegisterFunctionSelectors<TSystemResource>(systemService.GetAllFunctionABIs(), excludedFunctionSelectorRecords, excludeDefaultSystemFunctions);
        }

        public List<RegisterRootFunctionSelectorFunction> CreateRegisterRootFunctionSelectors<TSystemResource>(ISystemService<TSystemResource> systemService, List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null, bool excludeDefaultSystemFunctions = true)
            where TSystemResource : SystemResource, new()
        {
            return CreateRegisterRootFunctionSelectors(systemService.GetSystemFunctionABIs(), systemService.GetResource().ResourceIdEncoded, excludedFunctionSelectorRecords, excludeDefaultSystemFunctions);
        }


        public List<RegisterRootFunctionSelectorFunction> CreateRegisterFunctionSelectors(List<FunctionABI> functionABIs, Type systemResourceType, List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null, bool excludeDefaultSystemFunctions = true)
        {
            return CreateRegisterRootFunctionSelectors(functionABIs, ResourceRegistry.GetResourceEncoded(systemResourceType), excludedFunctionSelectorRecords, excludeDefaultSystemFunctions);
        }

        public List<RegisterRootFunctionSelectorFunction> CreateRegisterRootFunctionSelectors(List<FunctionABI> functionABIs, byte[] systemResourceId, List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null, bool excludeDefaultSystemFunctions = true)
        {
            if (excludedFunctionSelectorRecords == null)
            {
                excludedFunctionSelectorRecords = new List<FunctionSelectorsTableRecord>();
            }
            var excludedRegisteredSelectors = excludedFunctionSelectorRecords.Select(x => x.Values.SystemFunctionSelector.ToString()).ToList();
            if (excludeDefaultSystemFunctions)
            {
                excludedRegisteredSelectors.AddRange(new SystemDefaultFunctions().GetAllFunctionSignatures().ToList());
            }
          
            var functionAbis = functionABIs;
            var functionSelectorsToRegister = functionAbis.Where(x => !excludedRegisteredSelectors.Any(y => y.IsTheSameHex(x.Sha3Signature))).ToList();

            var registerFunctionSelectors = new List<RegisterRootFunctionSelectorFunction>();
            foreach (var functionSelectorToRegister in functionSelectorsToRegister)
            {
                var registerFunction = new RegisterRootFunctionSelectorFunction();
                registerFunction.SystemFunctionSignature = functionSelectorToRegister.Signature;
                registerFunction.WorldFunctionSignature = functionSelectorToRegister.Signature;
                registerFunction.SystemId = systemResourceId;
                registerFunctionSelectors.Add(registerFunction);
            }
            return registerFunctionSelectors;
        }

        public List<RegisterRootFunctionSelectorFunction> CreateRegisterFunctionSelectors<TSystemResource>(List<FunctionABI> functionABIs, List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null, bool excludeDefaultSystemFunctions = true)
          where TSystemResource : SystemResource, new()
        {

            return CreateRegisterFunctionSelectors(functionABIs, typeof(TSystemResource), excludedFunctionSelectorRecords, excludeDefaultSystemFunctions);
        }

        public RegisterSystemFunction CreateRegisterSystemFunction<TSystemResource, TService>(SystemServiceResourceRegistration<TSystemResource, TService> systemServiceResourceRegistration, string deployedAddress = null, bool publicAccess = true)
            where TSystemResource : SystemResource, new()
            where TService : ContractWeb3ServiceBase, ISystemService<TSystemResource>
        {
            var registerSystemFunction = new RegisterSystemFunction();
            registerSystemFunction.SystemId = systemServiceResourceRegistration.Resource.ResourceIdEncoded;
            registerSystemFunction.System = deployedAddress;
            registerSystemFunction.PublicAccess = publicAccess;
            return registerSystemFunction;
        }

        public List<SystemCallData> CreateRegisterRootFunctionSelectorsBatchSystemCallData<TSystemResource>(ISystemService<TSystemResource> systemService,
                                                                                                            List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null,
                                                                                                             bool excludeDefaultSystemFunctions = true)
             where TSystemResource : SystemResource, new()
        {

            var registerFunctionSelectors = CreateRegisterRootFunctionSelectors(systemService, excludedFunctionSelectorRecords, excludeDefaultSystemFunctions);
            return registerFunctionSelectors.CreateBatchSystemCallData<RegistrationSystemResource, RegisterRootFunctionSelectorFunction>();
        }
    }
}
