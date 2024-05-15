using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem.ContractDefinition;
using Nethereum.Mud.Contracts.World;
using Nethereum.Mud.Contracts.World.ContractDefinition;
using Nethereum.Mud.Contracts.WorldFactory;
using Nethereum.Mud.Contracts.WorldFactory.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Mud.Contracts.World.Systems.AccessManagementSystem.ContractDefinition;
using Nethereum.Mud.Contracts.World.Systems.BalanceTransferSystem.ContractDefinition;
using Nethereum.Mud.Contracts.World.Systems.BatchCallSystem.ContractDefinition;
using Nethereum.Mud.Contracts.World.Modules.InitModule.ContractDefinition;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;

using Nethereum.Web3;



namespace Nethereum.Mud.Contracts
{

    public class WorldFactoryContractAddresses
    {
        public string AccessManagementSystemAddress { get; set; }
        public string BalanceTransferSystemAddress { get; set; }
        public string BatchCallSystemAddress { get; set; }
        public string RegistrationSystemAddress { get; set; }
        public string InitModuleAddress { get; set; }
        public string WorldFactoryAddress { get; set; }
    }

    public class WorldFactoryDeployService
    {
        public async Task<WorldDeployedEventDTO> DeployWorldAsync(Nethereum.Web3.IWeb3 web3, string salt, WorldFactoryContractAddresses worldFactoryContractAddresses)
        {
            var worldFactoryService = new WorldFactoryService(web3, worldFactoryContractAddresses.WorldFactoryAddress);
            var worldDeployedTransactionReceipt = await worldFactoryService.DeployWorldRequestAndWaitForReceiptAsync(salt.HexToByteArray());
            if (worldDeployedTransactionReceipt.Failed()) throw new Exception("World deployment failed");
            var log = worldDeployedTransactionReceipt.DecodeAllEvents<WorldDeployedEventDTO>().FirstOrDefault() 
                ?? throw new Exception("WorldDeployedEvent not found in transaction receipt");
            return log.Event;
        }

        public async Task<WorldFactoryContractAddresses> DeployWorldFactoryContractAndSystemDependenciesAsync(Nethereum.Web3.IWeb3 web3, string create2DeployerAddress, string salt)
        {
            var create2ProxyDeployerService = web3.Eth.Create2DeterministicDeploymentProxyService;
            var accessManagementSystemDeployment = new AccessManagementSystemDeployment();
            var accessManagementDeploymentResult = await create2ProxyDeployerService.DeployContractRequestAsync(accessManagementSystemDeployment, create2DeployerAddress, salt);


            var balanceTransferSystemDeployment = new BalanceTransferSystemDeployment();
            var balanceTransferDeploymentResult = await create2ProxyDeployerService.DeployContractRequestAsync(balanceTransferSystemDeployment, create2DeployerAddress, salt);

            var batchCallSystemDeployment = new BatchCallSystemDeployment();
            var batchCallSystemDeploymentResult = await create2ProxyDeployerService.DeployContractRequestAsync(batchCallSystemDeployment, create2DeployerAddress, salt);


            var registrationSystemDeployment = new RegistrationSystemDeployment();
            var registrationSystemDeploymentResult = await create2ProxyDeployerService.DeployContractRequestAsync(registrationSystemDeployment, create2DeployerAddress, salt);

            var initModuleDeployment = new InitModuleDeployment();
            initModuleDeployment.AccessManagementSystem = accessManagementDeploymentResult.Address;
            initModuleDeployment.BalanceTransferSystem = balanceTransferDeploymentResult.Address;
            initModuleDeployment.BatchCallSystem = batchCallSystemDeploymentResult.Address;
            initModuleDeployment.RegistrationSystem = registrationSystemDeploymentResult.Address; ;

            var initModuleDeploymentResult = await create2ProxyDeployerService.DeployContractRequestAsync(initModuleDeployment, create2DeployerAddress, salt);

            
            var worldFactoryDeployment = new WorldFactoryDeployment();
            worldFactoryDeployment.InitModule = initModuleDeploymentResult.Address;

            var worldFactoryDeploymentResult = await create2ProxyDeployerService.DeployContractRequestAndWaitForReceiptAsync(worldFactoryDeployment, create2DeployerAddress, salt);
            
            return new WorldFactoryContractAddresses
            {
                AccessManagementSystemAddress = accessManagementDeploymentResult.Address,
                BalanceTransferSystemAddress = balanceTransferDeploymentResult.Address,
                BatchCallSystemAddress = batchCallSystemDeploymentResult.Address,
                RegistrationSystemAddress = registrationSystemDeploymentResult.Address,
                InitModuleAddress = initModuleDeploymentResult.Address,
                WorldFactoryAddress = worldFactoryDeploymentResult.Address
            };

        }
    }
}
