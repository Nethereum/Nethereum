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
using Nethereum.AccountAbstraction.Contracts.Interfaces.IAccountRegistry.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Interfaces.IAccountRegistry
{
    public partial class IAccountRegistryService: IAccountRegistryServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, IAccountRegistryDeployment iAccountRegistryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<IAccountRegistryDeployment>().SendRequestAndWaitForReceiptAsync(iAccountRegistryDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, IAccountRegistryDeployment iAccountRegistryDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<IAccountRegistryDeployment>().SendRequestAsync(iAccountRegistryDeployment);
        }

        public static async Task<IAccountRegistryService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, IAccountRegistryDeployment iAccountRegistryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, iAccountRegistryDeployment, cancellationTokenSource);
            return new IAccountRegistryService(web3, receipt.ContractAddress);
        }

        public IAccountRegistryService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class IAccountRegistryServiceBase: ContractWeb3ServiceBase
    {

        public IAccountRegistryServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<bool> IsActiveQueryAsync(IsActiveFunction isActiveFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsActiveFunction, bool>(isActiveFunction, blockParameter);
        }

        
        public virtual Task<bool> IsActiveQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var isActiveFunction = new IsActiveFunction();
                isActiveFunction.Account = account;
            
            return ContractHandler.QueryAsync<IsActiveFunction, bool>(isActiveFunction, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(IsActiveFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {

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
