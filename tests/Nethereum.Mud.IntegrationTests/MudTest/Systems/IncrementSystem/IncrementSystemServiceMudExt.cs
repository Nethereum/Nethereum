using Nethereum.Mud.Contracts.Core.Systems;
using Nethereum.ABI.Model;
using Nethereum.Mud.IntegrationTests.MudTest.Systems.IncrementSystem.ContractDefinition;
using Nethereum.Contracts.Create2Deployment;
using Nethereum.Contracts;
using Nethereum.ABI.FunctionEncoding;

namespace Nethereum.Mud.IntegrationTests.MudTest.Systems.IncrementSystem
{
    public class IncrementSystemServiceResource : SystemResource
    {
        public IncrementSystemServiceResource() : base("IncrementSystem") { }
    }

    public partial class IncrementSystemService : ISystemService<IncrementSystemServiceResource>
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
            var accessManagementSystemDeployment = new IncrementSystemDeployment();
            return create2ProxyDeployerService.DeployContractRequestAsync(accessManagementSystemDeployment, deployerAddress, salt, byteCodeLibraries);
        }

        public Task<Create2ContractDeploymentTransactionReceiptResult> DeployCreate2ContractAndWaitForReceiptAsync(string deployerAddress, string salt, ByteCodeLibrary[] byteCodeLibraries, CancellationToken cancellationToken = default)
        {
            var create2ProxyDeployerService = Web3.Eth.Create2DeterministicDeploymentProxyService;
            var accessManagementSystemDeployment = new IncrementSystemDeployment();
            return create2ProxyDeployerService.DeployContractRequestAndWaitForReceiptAsync(accessManagementSystemDeployment, deployerAddress, salt, byteCodeLibraries, cancellationToken);
        }
    }
}
