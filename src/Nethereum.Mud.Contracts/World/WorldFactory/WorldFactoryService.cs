using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts;
using System.Threading;
using Nethereum.Mud.Contracts.WorldFactory.ContractDefinition;

namespace Nethereum.Mud.Contracts.WorldFactory
{
    public partial class WorldFactoryService: ContractWeb3ServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, WorldFactoryDeployment worldFactoryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<WorldFactoryDeployment>().SendRequestAndWaitForReceiptAsync(worldFactoryDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, WorldFactoryDeployment worldFactoryDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<WorldFactoryDeployment>().SendRequestAsync(worldFactoryDeployment);
        }

        public static async Task<WorldFactoryService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, WorldFactoryDeployment worldFactoryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, worldFactoryDeployment, cancellationTokenSource);
            return new WorldFactoryService(web3, receipt.ContractAddress);
        }

        public WorldFactoryService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<string> DeployWorldRequestAsync(DeployWorldFunction deployWorldFunction)
        {
             return ContractHandler.SendRequestAsync(deployWorldFunction);
        }

        public Task<TransactionReceipt> DeployWorldRequestAndWaitForReceiptAsync(DeployWorldFunction deployWorldFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(deployWorldFunction, cancellationToken);
        }

        public Task<string> DeployWorldRequestAsync(byte[] salt)
        {
            var deployWorldFunction = new DeployWorldFunction();
                deployWorldFunction.Salt = salt;
            
             return ContractHandler.SendRequestAsync(deployWorldFunction);
        }

        public Task<TransactionReceipt> DeployWorldRequestAndWaitForReceiptAsync(byte[] salt, CancellationTokenSource cancellationToken = null)
        {
            var deployWorldFunction = new DeployWorldFunction();
                deployWorldFunction.Salt = salt;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(deployWorldFunction, cancellationToken);
        }

        public Task<string> InitModuleQueryAsync(InitModuleFunction initModuleFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<InitModuleFunction, string>(initModuleFunction, blockParameter);
        }

        
        public Task<string> InitModuleQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<InitModuleFunction, string>(null, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(DeployWorldFunction),
                typeof(InitModuleFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(WorldDeployedEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {

            };
        }
    }
}
