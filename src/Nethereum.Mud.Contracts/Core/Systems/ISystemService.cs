using Nethereum.ABI.Model;
using Nethereum.Contracts;
using System.Collections.Generic;
using Nethereum.ABI.FunctionEncoding;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.Create2Deployment;
using System.Threading;

namespace Nethereum.Mud.Contracts.Core.Systems
{
    public interface ISystemService<TSystemResource>: ISystemService where TSystemResource : SystemResource
    {

    }

    public interface ISystemService: IContractService
    {
        public List<FunctionABI> GetSystemFunctionABIs();
        public IResource Resource { get; }
        public string CalculateCreate2Address(string deployerAddress, string salt, params ByteCodeLibrary[] byteCodeLibraries);
        public Task<Create2ContractDeploymentTransactionResult> DeployCreate2ContractAsync(string deployerAddress, string salt, params ByteCodeLibrary[] byteCodeLibraries);
        public Task<Create2ContractDeploymentTransactionReceiptResult> DeployCreate2ContractAndWaitForReceiptAsync(string deployerAddress, string salt, ByteCodeLibrary[] byteCodeLibraries, CancellationToken cancellationToken = default);
        public ISystemServiceResourceRegistration SystemServiceResourceRegistrator { get; }

       
    }
}
