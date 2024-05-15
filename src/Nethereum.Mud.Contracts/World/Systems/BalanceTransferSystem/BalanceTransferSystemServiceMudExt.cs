using Nethereum.ABI.Model;
using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.Mud.Contracts.World.Tables;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem.ContractDefinition;
using Nethereum.Contracts;
using System.Threading.Tasks;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Web3;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem.ContractDefinition;
using Nethereum.Contracts.Standards.ENS.ETHRegistrarController.ContractDefinition;
using Nethereum.Mud.Contracts.Core.Systems;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Mud.Contracts.World.Systems.BalanceTransferSystem.ContractDefinition;
using Nethereum.Contracts.Create2Deployment;
using System.Threading;

namespace Nethereum.Mud.Contracts.World.Systems.BalanceTransferSystem
{

    public class BalanceTransferSystemResource:SystemResource
    {
        public BalanceTransferSystemResource():base("BalanceTransfer", String.Empty){}
    }


    public partial class BalanceTransferSystemService : ISystemService<BalanceTransferSystemResource>
    {
        public IResource Resource => this.GetResource();

        public ISystemServiceResourceRegistration SystemServiceResourceRegistrator
        {
            get
            {
                return this.GetSystemServiceResourceRegistration<BalanceTransferSystemResource, BalanceTransferSystemService>();
            }
        }

        public List<FunctionABI> GetSystemFunctionABIs()
        {
            return GetAllFunctionABIs();
        }

        public string CalculateCreate2Address(string deployerAddress, string salt, params ByteCodeLibrary[] byteCodeLibraries)
        {
            return new BalanceTransferSystemDeployment().CalculateCreate2Address(deployerAddress, salt, byteCodeLibraries);
        }

        public Task<Create2ContractDeploymentTransactionResult> DeployCreate2ContractAsync(string deployerAddress, string salt, params ByteCodeLibrary[] byteCodeLibraries)
        {
            var create2ProxyDeployerService = Web3.Eth.Create2DeterministicDeploymentProxyService;
            var accessManagementSystemDeployment = new BalanceTransferSystemDeployment();
            return create2ProxyDeployerService.DeployContractRequestAsync(accessManagementSystemDeployment, deployerAddress, salt, byteCodeLibraries);
        }

        public Task<Create2ContractDeploymentTransactionReceiptResult> DeployCreate2ContractAndWaitForReceiptAsync(string deployerAddress, string salt, ByteCodeLibrary[] byteCodeLibraries, CancellationToken cancellationToken = default)
        {
            var create2ProxyDeployerService = Web3.Eth.Create2DeterministicDeploymentProxyService;
            var accessManagementSystemDeployment = new BalanceTransferSystemDeployment();
            return create2ProxyDeployerService.DeployContractRequestAndWaitForReceiptAsync(accessManagementSystemDeployment, deployerAddress, salt, byteCodeLibraries, cancellationToken);
        }
    }




}
