using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Mud.Contracts.AccessManagementSystem.ContractDefinition;
using Nethereum.Mud.Contracts.BalanceTransferSystem.ContractDefinition;
using Nethereum.Mud.Contracts.BatchCallSystem.ContractDefinition;
using Nethereum.Mud.Contracts.InitModule.ContractDefinition;
using Nethereum.Mud.Contracts.RegistrationSystem.ContractDefinition;
using Nethereum.Mud.Contracts.World.ContractDefinition;
using Nethereum.Mud.Contracts.WorldFactory;
using Nethereum.Mud.Contracts.WorldFactory.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


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
            var accessManagementSystemAddress = create2ProxyDeployerService.CalculateCreate2Address(accessManagementSystemDeployment, create2DeployerAddress, salt);
            var accessManagementSystemTxnHash = await create2ProxyDeployerService.DeployContractRequestAsync(accessManagementSystemDeployment, create2DeployerAddress, salt);

            var balanceTransferSystemDeployment = new BalanceTransferSystemDeployment();
            var balanceTransferSystemAddress = create2ProxyDeployerService.CalculateCreate2Address(balanceTransferSystemDeployment, create2DeployerAddress, salt);
            var balanceTransferSystemTxnHash = await create2ProxyDeployerService.DeployContractRequestAsync(balanceTransferSystemDeployment, create2DeployerAddress, salt);

            var batchCallSystemDeployment = new BatchCallSystemDeployment();
            var batchCallSystemAddress = create2ProxyDeployerService.CalculateCreate2Address(batchCallSystemDeployment, create2DeployerAddress, salt);
            var batchCallSystemTxnHash = await create2ProxyDeployerService.DeployContractRequestAsync(batchCallSystemDeployment, create2DeployerAddress, salt);

            var registrationSystemDeployment = new RegistrationSystemDeployment();
            var registrationSystemAddress = create2ProxyDeployerService.CalculateCreate2Address(registrationSystemDeployment, create2DeployerAddress, salt);
            var registrationSystemTxnHash = await create2ProxyDeployerService.DeployContractRequestAsync(registrationSystemDeployment, create2DeployerAddress, salt);

            var initModuleDeployment = new InitModuleDeployment();
            initModuleDeployment.AccessManagementSystem = accessManagementSystemAddress;
            initModuleDeployment.BalanceTransferSystem = balanceTransferSystemAddress;
            initModuleDeployment.BatchCallSystem = batchCallSystemAddress;
            initModuleDeployment.RegistrationSystem = registrationSystemAddress;
            var initModuleAddress = create2ProxyDeployerService.CalculateCreate2Address(initModuleDeployment, create2DeployerAddress, salt);
            var initModuleTxnHash = await create2ProxyDeployerService.DeployContractRequestAsync(initModuleDeployment, create2DeployerAddress, salt);

            var worldFactoryDeployment = new WorldFactoryDeployment();
            worldFactoryDeployment.InitModule = initModuleAddress;
            var worldFactoryAddress = create2ProxyDeployerService.CalculateCreate2Address(worldFactoryDeployment, create2DeployerAddress, salt);
            var worldFactoryTransactionReceipt = await create2ProxyDeployerService.DeployContractRequestAndWaitForReceiptAsync(worldFactoryDeployment, create2DeployerAddress, salt);  
            
            return new WorldFactoryContractAddresses
            {
                AccessManagementSystemAddress = accessManagementSystemAddress,
                BalanceTransferSystemAddress = balanceTransferSystemAddress,
                BatchCallSystemAddress = batchCallSystemAddress,
                RegistrationSystemAddress = registrationSystemAddress,
                InitModuleAddress = initModuleAddress,
                WorldFactoryAddress = worldFactoryAddress
            };

        }
    }
}
