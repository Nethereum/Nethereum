using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Mud.Contracts.Core.Systems;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts.Create2Deployment;
using Nethereum.Mud;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts;
using System.Threading;
using MyProject.Contracts.MyWorld.Systems.IncrementSystem.ContractDefinition;

namespace MyProject.Contracts.MyWorld.Systems.IncrementSystem
{
    public class IncrementSystemServiceResource : SystemResource
    {
        public IncrementSystemServiceResource() : base("Increment", "MyWorld") { }
    }
    public partial class IncrementSystemService: ISystemService<IncrementSystemServiceResource> 
    {
        public IResource Resource => this.GetResource();

        public ISystemServiceResourceRegistration SystemServiceResourceRegistrator
        {
            get
            {
                return this.GetSystemServiceResourceRegistration<IncrementSystemServiceResource, IncrementSystemService>();
            }
        }
        

        public List<FunctionABI> GetSystemFunctionABIs()
        {
            return GetAllFunctionABIs();
        }

        public string CalculateCreate2Address(string deployerAddress, string salt, params ByteCodeLibrary[] byteCodeLibraries)
        {
            return new IncrementSystemDeployment().CalculateCreate2Address(deployerAddress, salt, byteCodeLibraries);
        }

        public Task<Create2ContractDeploymentTransactionResult> DeployCreate2ContractAsync(string deployerAddress, string salt, params ByteCodeLibrary[] byteCodeLibraries)
        {
            var create2ProxyDeployerService = Web3.Eth.Create2DeterministicDeploymentProxyService;
            var deployment = new IncrementSystemDeployment();
            return create2ProxyDeployerService.DeployContractRequestAsync(deployment, deployerAddress, salt, byteCodeLibraries);
        }
        public Task<Create2ContractDeploymentTransactionReceiptResult> DeployCreate2ContractAndWaitForReceiptAsync(string deployerAddress, string salt, ByteCodeLibrary[] byteCodeLibraries, CancellationToken cancellationToken = default)
        {
            var create2ProxyDeployerService = Web3.Eth.Create2DeterministicDeploymentProxyService;
            var deployment = new IncrementSystemDeployment();
            return create2ProxyDeployerService.DeployContractRequestAndWaitForReceiptAsync(deployment, deployerAddress, salt, byteCodeLibraries, cancellationToken);
        }
    }
}
