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
    /// <summary>
    /// Class responsible for building system registration functionalities.
    /// </summary>
    public class SystemRegistrationBuilder
    {
        /// <summary>
        /// Creates a list of RegisterRootFunctionSelectorFunction for a specified system resource using the provided contract service.
        /// </summary>
        /// <typeparam name="TSystemResource">The type of the system resource.</typeparam>
        /// <param name="systemService">The system service to be used.</param>
        /// <param name="excludedFunctionSelectorRecords">The list of function selector records to be excluded (optional).</param>
        /// <param name="excludeDefaultSystemFunctions">Whether to exclude default system functions (default is true).</param>
        /// <returns>A list of RegisterRootFunctionSelectorFunction.</returns>
        public List<RegisterRootFunctionSelectorFunction> CreateRegisterRootFunctionSelectors<TSystemResource>(ContractServiceBase systemService, List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null, bool excludeDefaultSystemFunctions = true)
            where TSystemResource : SystemResource, new()
        {
            return CreateRegisterFunctionSelectors<TSystemResource>(systemService.GetAllFunctionABIs(), excludedFunctionSelectorRecords, excludeDefaultSystemFunctions);
        }

        /// <summary>
        /// Creates a list of RegisterRootFunctionSelectorFunction for a specified system resource using the provided system service.
        /// </summary>
        /// <typeparam name="TSystemResource">The type of the system resource.</typeparam>
        /// <param name="systemService">The system service to be used.</param>
        /// <param name="excludedFunctionSelectorRecords">The list of function selector records to be excluded (optional).</param>
        /// <param name="excludeDefaultSystemFunctions">Whether to exclude default system functions (default is true).</param>
        /// <returns>A list of RegisterRootFunctionSelectorFunction.</returns>
        public List<RegisterRootFunctionSelectorFunction> CreateRegisterRootFunctionSelectors<TSystemResource>(ISystemService<TSystemResource> systemService, List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null, bool excludeDefaultSystemFunctions = true)
            where TSystemResource : SystemResource, new()
        {
            return CreateRegisterRootFunctionSelectors(systemService.GetSystemFunctionABIs(), systemService.GetResource().ResourceIdEncoded, excludedFunctionSelectorRecords, excludeDefaultSystemFunctions);
        }

        /// <summary>
        /// Creates a list of RegisterRootFunctionSelectorFunction for the specified function ABIs and system resource type.
        /// </summary>
        /// <param name="functionABIs">The list of function ABIs to be used.</param>
        /// <param name="systemResourceType">The type of the system resource.</param>
        /// <param name="excludedFunctionSelectorRecords">The list of function selector records to be excluded (optional).</param>
        /// <param name="excludeDefaultSystemFunctions">Whether to exclude default system functions (default is true).</param>
        /// <returns>A list of RegisterRootFunctionSelectorFunction.</returns>
        public List<RegisterRootFunctionSelectorFunction> CreateRegisterFunctionSelectors(List<FunctionABI> functionABIs, Type systemResourceType, List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null, bool excludeDefaultSystemFunctions = true)
        {
            return CreateRegisterRootFunctionSelectors(functionABIs, ResourceRegistry.GetResourceEncoded(systemResourceType), excludedFunctionSelectorRecords, excludeDefaultSystemFunctions);
        }

        /// <summary>
        /// Creates a list of RegisterRootFunctionSelectorFunction for the specified function ABIs and system resource ID.
        /// </summary>
        /// <param name="functionABIs">The list of function ABIs to be used.</param>
        /// <param name="systemResourceId">The ID of the system resource.</param>
        /// <param name="excludedFunctionSelectorRecords">The list of function selector records to be excluded (optional).</param>
        /// <param name="excludeDefaultSystemFunctions">Whether to exclude default system functions (default is true).</param>
        /// <returns>A list of RegisterRootFunctionSelectorFunction.</returns>
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

        /// <summary>
        /// Creates a list of RegisterRootFunctionSelectorFunction for a specified system resource type.
        /// </summary>
        /// <typeparam name="TSystemResource">The type of the system resource.</typeparam>
        /// <param name="functionABIs">The list of function ABIs to be used.</param>
        /// <param name="excludedFunctionSelectorRecords">The list of function selector records to be excluded (optional).</param>
        /// <param name="excludeDefaultSystemFunctions">Whether to exclude default system functions (default is true).</param>
        /// <returns>A list of RegisterRootFunctionSelectorFunction.</returns>
        public List<RegisterRootFunctionSelectorFunction> CreateRegisterFunctionSelectors<TSystemResource>(List<FunctionABI> functionABIs, List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null, bool excludeDefaultSystemFunctions = true)
            where TSystemResource : SystemResource, new()
        {
            return CreateRegisterFunctionSelectors(functionABIs, typeof(TSystemResource), excludedFunctionSelectorRecords, excludeDefaultSystemFunctions);
        }

        /// <summary>
        /// Creates a RegisterSystemFunction for a specified system resource and service.
        /// </summary>
        /// <typeparam name="TSystemResource">The type of the system resource.</typeparam>
        /// <typeparam name="TService">The type of the system service.</typeparam>
        /// <param name="systemServiceResourceRegistration">The system service resource registration to be used.</param>
        /// <param name="deployedAddress">The deployed address of the system (optional).</param>
        /// <param name="publicAccess">Whether the system has public access (default is true).</param>
        /// <returns>A RegisterSystemFunction.</returns>
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

        /// <summary>
        /// Creates a batch of SystemCallData for registering root function selectors for a specified system resource.
        /// </summary>
        /// <typeparam name="TSystemResource">The type of the system resource.</typeparam>
        /// <param name="systemService">The system service to be used.</param>
        /// <param name="excludedFunctionSelectorRecords">The list of function selector records to be excluded (optional).</param>
        /// <param name="excludeDefaultSystemFunctions">Whether to exclude default system functions (default is true).</param>
        /// <returns>A list of SystemCallData.</returns>
        public List<SystemCallData> CreateRegisterRootFunctionSelectorsBatchSystemCallData<TSystemResource>(ISystemService<TSystemResource> systemService,
                                                                                                            List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null,
                                                                                                            bool excludeDefaultSystemFunctions = true)
            where TSystemResource : SystemResource, new()
        {
            var registerFunctionSelectors = CreateRegisterRootFunctionSelectors(systemService, excludedFunctionSelectorRecords, excludeDefaultSystemFunctions);
            return registerFunctionSelectors.CreateBatchSystemCallData<RegistrationSystemResource, RegisterRootFunctionSelectorFunction>();
        }

        /// <summary>
        /// Creates a batch of SystemCallData for registering root function selectors for a specified system resource using provided function ABIs.
        /// </summary>
        /// <typeparam name="TSystemResource">The type of the system resource.</typeparam>
        /// <param name="systemService">The system service to be used.</param>
        /// <param name="functionABIs">The list of function ABIs to be used.</param>
        /// <returns>A list of SystemCallData.</returns>
        public List<SystemCallData> CreateRegisterRootFunctionSelectorsBatchSystemCallData<TSystemResource>(ISystemService<TSystemResource> systemService, List<FunctionABI> functionABIs)
            where TSystemResource : SystemResource, new()
        {
            var registerFunctionSelectors = CreateRegisterRootFunctionSelectors(functionABIs, systemService.Resource.ResourceIdEncoded);
            return registerFunctionSelectors.CreateBatchSystemCallData<RegistrationSystemResource, RegisterRootFunctionSelectorFunction>();
        }
    }
}