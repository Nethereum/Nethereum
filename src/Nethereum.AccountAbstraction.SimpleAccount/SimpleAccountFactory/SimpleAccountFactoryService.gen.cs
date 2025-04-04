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
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition;

namespace Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory
{
    public partial class SimpleAccountFactoryService: SimpleAccountFactoryServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, SimpleAccountFactoryDeployment simpleAccountFactoryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<SimpleAccountFactoryDeployment>().SendRequestAndWaitForReceiptAsync(simpleAccountFactoryDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, SimpleAccountFactoryDeployment simpleAccountFactoryDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<SimpleAccountFactoryDeployment>().SendRequestAsync(simpleAccountFactoryDeployment);
        }

        public static async Task<SimpleAccountFactoryService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, SimpleAccountFactoryDeployment simpleAccountFactoryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, simpleAccountFactoryDeployment, cancellationTokenSource);
            return new SimpleAccountFactoryService(web3, receipt.ContractAddress);
        }

        public SimpleAccountFactoryService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class SimpleAccountFactoryServiceBase: ContractWeb3ServiceBase
    {

        public SimpleAccountFactoryServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<string> AccountImplementationQueryAsync(AccountImplementationFunction accountImplementationFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AccountImplementationFunction, string>(accountImplementationFunction, blockParameter);
        }

        
        public virtual Task<string> AccountImplementationQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AccountImplementationFunction, string>(null, blockParameter);
        }

        public virtual Task<string> CreateAccountRequestAsync(CreateAccountFunction createAccountFunction)
        {
             return ContractHandler.SendRequestAsync(createAccountFunction);
        }

        public virtual Task<TransactionReceipt> CreateAccountRequestAndWaitForReceiptAsync(CreateAccountFunction createAccountFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(createAccountFunction, cancellationToken);
        }

        public virtual Task<string> CreateAccountRequestAsync(string owner, BigInteger salt)
        {
            var createAccountFunction = new CreateAccountFunction();
                createAccountFunction.Owner = owner;
                createAccountFunction.Salt = salt;
            
             return ContractHandler.SendRequestAsync(createAccountFunction);
        }

        public virtual Task<TransactionReceipt> CreateAccountRequestAndWaitForReceiptAsync(string owner, BigInteger salt, CancellationTokenSource cancellationToken = null)
        {
            var createAccountFunction = new CreateAccountFunction();
                createAccountFunction.Owner = owner;
                createAccountFunction.Salt = salt;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(createAccountFunction, cancellationToken);
        }

        public Task<string> GetAddressQueryAsync(GetAddressFunction getAddressFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetAddressFunction, string>(getAddressFunction, blockParameter);
        }

        
        public virtual Task<string> GetAddressQueryAsync(string owner, BigInteger salt, BlockParameter blockParameter = null)
        {
            var getAddressFunction = new GetAddressFunction();
                getAddressFunction.Owner = owner;
                getAddressFunction.Salt = salt;
            
            return ContractHandler.QueryAsync<GetAddressFunction, string>(getAddressFunction, blockParameter);
        }

        public Task<string> SenderCreatorQueryAsync(SenderCreatorFunction senderCreatorFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SenderCreatorFunction, string>(senderCreatorFunction, blockParameter);
        }

        
        public virtual Task<string> SenderCreatorQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SenderCreatorFunction, string>(null, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(AccountImplementationFunction),
                typeof(CreateAccountFunction),
                typeof(GetAddressFunction),
                typeof(SenderCreatorFunction)
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
