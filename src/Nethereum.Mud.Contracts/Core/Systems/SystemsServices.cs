using Nethereum.Web3;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem.ContractDefinition;
using Nethereum.Mud.Contracts.World.Tables;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts.Create2Deployment;
using System.Runtime.CompilerServices;
using System.Threading;
using Nethereum.Contracts;
using System;

namespace Nethereum.Mud.Contracts.Core.Systems
{
    public class SystemDeploymentResult
    {
        public  Create2ContractDeploymentTransactionResult DeploymentResult { get; set; }
        public ISystemService SystemService { get; set; }
    }

    public abstract class SystemsServices
    {
        public BatchCallSystemService BatchCallSystem { get; protected set; }

        public List<ISystemService> SystemServices { get; protected set; }

        public SystemsServices(IWeb3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractAddress = contractAddress;
            BatchCallSystem = new BatchCallSystemService(web3, contractAddress);
        }

        public IWeb3 Web3 { get; protected set; }
        public string ContractAddress { get; protected set; }

        public virtual async Task<List<SystemDeploymentResult>> DeployAllCreate2ContractSystemsRequestAsync(string deployerAddress, string salt, params ByteCodeLibrary[] byteCodeLibraries)
        {
            var results = new List<SystemDeploymentResult>();   
            foreach (var systemService in SystemServices)
            {
                var result = await systemService.DeployCreate2ContractAsync(deployerAddress, salt, byteCodeLibraries);
                results.Add(new SystemDeploymentResult { DeploymentResult = result, SystemService = systemService });
            }
            return results;
        }

        public virtual async Task<List<SystemDeploymentResult>> DeployAllCreate2ContractSystemsRequestAndWaitForReceiptAsync(string deployerAddress, string salt, ByteCodeLibrary[] byteCodeLibraries = null, CancellationToken cancellationToken = default)
        {
            var results = new List<SystemDeploymentResult>();
            foreach (var systemService in SystemServices)
            {
                var result = await systemService.DeployCreate2ContractAndWaitForReceiptAsync(deployerAddress, salt, byteCodeLibraries, cancellationToken);
                results.Add(new SystemDeploymentResult { DeploymentResult = 
                    new Create2ContractDeploymentTransactionResult()
                    {
                        AlreadyDeployed = result.AlreadyDeployed,
                        Address = result.Address,
                        TransactionHash = result.TransactionReceipt.TransactionHash,
                    }, 
                    SystemService = systemService });
            }
            return results;
        }

        public virtual void SetSystemsCallFromDelegatorContractHandler(string delegatorAddress)
        {
            foreach (var systemService in SystemServices)
            {
                systemService.SetSystemCallFromDelegatorContractHandler(delegatorAddress);
            }
        }

        public virtual async Task<string> BatchRegisterAllSystemsRequestAsync(string deployerAddress, string salt, bool publicAccess = true, ByteCodeLibrary[] byteCodeLibraries= null, List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null, bool excludeDefaultSystemFunctions = true)
        {
            var systemCallData = new List<SystemCallData>();
            foreach (var systemService in SystemServices)
            {
                var deployedAddress = systemService.CalculateCreate2Address(deployerAddress, salt, byteCodeLibraries);
                var registrator = systemService.SystemServiceResourceRegistrator;
                var callData = registrator.CreateRegisterSystemAndRegisterRootFunctionSelectorsBatchSystemCallData(deployedAddress, publicAccess, excludedFunctionSelectorRecords, excludeDefaultSystemFunctions);
                systemCallData.AddRange(callData);
            }
            return await BatchCallSystem.BatchCallRequestAsync(systemCallData);
        }

        public virtual async Task<TransactionReceipt> BatchRegisterAllSystemsRequestAndWaitForReceiptAsync(string deployerAddress, string salt, bool publicAccess = true, ByteCodeLibrary[] byteCodeLibraries = null, List<FunctionSelectorsTableRecord> excludedFunctionSelectorRecords = null, bool excludeDefaultSystemFunctions = true)
        {
            var systemCallData = new List<SystemCallData>();
            foreach (var systemService in SystemServices)
            {
                var deployedAddress = systemService.CalculateCreate2Address(deployerAddress, salt, byteCodeLibraries);
                var registrator = systemService.SystemServiceResourceRegistrator;
                var callData = registrator.CreateRegisterSystemAndRegisterRootFunctionSelectorsBatchSystemCallData(deployedAddress, publicAccess, excludedFunctionSelectorRecords, excludeDefaultSystemFunctions);
                systemCallData.AddRange(callData);
            }
            return await BatchCallSystem.BatchCallRequestAndWaitForReceiptAsync(systemCallData);
        }

        public void HandleCustomErrorException(SmartContractCustomErrorRevertException exception)
        {
            foreach (var systemService in SystemServices)
            {
                systemService.HandleCustomErrorException(exception);
            }
        }

        public SmartContractCustomErrorRevertExceptionErrorABI FindCustomErrorException(SmartContractCustomErrorRevertException exception)
        {
            foreach (var systemService in SystemServices)
            {
                var error = systemService.FindCustomErrorException(exception);
                if (error != null)
                {
                    return error;
                }
            }
            return null;
        }
    }
}
