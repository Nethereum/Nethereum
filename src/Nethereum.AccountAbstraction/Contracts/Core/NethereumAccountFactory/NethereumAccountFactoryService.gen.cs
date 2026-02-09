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
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory
{
    public partial class NethereumAccountFactoryService: NethereumAccountFactoryServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, NethereumAccountFactoryDeployment nethereumAccountFactoryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<NethereumAccountFactoryDeployment>().SendRequestAndWaitForReceiptAsync(nethereumAccountFactoryDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, NethereumAccountFactoryDeployment nethereumAccountFactoryDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<NethereumAccountFactoryDeployment>().SendRequestAsync(nethereumAccountFactoryDeployment);
        }

        public static async Task<NethereumAccountFactoryService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, NethereumAccountFactoryDeployment nethereumAccountFactoryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, nethereumAccountFactoryDeployment, cancellationTokenSource);
            return new NethereumAccountFactoryService(web3, receipt.ContractAddress);
        }

        public NethereumAccountFactoryService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class NethereumAccountFactoryServiceBase: ContractWeb3ServiceBase
    {

        public NethereumAccountFactoryServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
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

        public virtual Task<string> CreateAccountRequestAsync(byte[] salt, byte[] initData)
        {
            var createAccountFunction = new CreateAccountFunction();
                createAccountFunction.Salt = salt;
                createAccountFunction.InitData = initData;
            
             return ContractHandler.SendRequestAsync(createAccountFunction);
        }

        public virtual Task<TransactionReceipt> CreateAccountRequestAndWaitForReceiptAsync(byte[] salt, byte[] initData, CancellationTokenSource cancellationToken = null)
        {
            var createAccountFunction = new CreateAccountFunction();
                createAccountFunction.Salt = salt;
                createAccountFunction.InitData = initData;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(createAccountFunction, cancellationToken);
        }

        public Task<string> EntryPointQueryAsync(EntryPointFunction entryPointFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EntryPointFunction, string>(entryPointFunction, blockParameter);
        }

        
        public virtual Task<string> EntryPointQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EntryPointFunction, string>(null, blockParameter);
        }

        public Task<string> GetAddressQueryAsync(GetAddressFunction getAddressFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetAddressFunction, string>(getAddressFunction, blockParameter);
        }

        
        public virtual Task<string> GetAddressQueryAsync(byte[] salt, byte[] initData, BlockParameter blockParameter = null)
        {
            var getAddressFunction = new GetAddressFunction();
                getAddressFunction.Salt = salt;
                getAddressFunction.InitData = initData;
            
            return ContractHandler.QueryAsync<GetAddressFunction, string>(getAddressFunction, blockParameter);
        }

        public Task<byte[]> GetInitCodeQueryAsync(GetInitCodeFunction getInitCodeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetInitCodeFunction, byte[]>(getInitCodeFunction, blockParameter);
        }

        
        public virtual Task<byte[]> GetInitCodeQueryAsync(byte[] salt, byte[] initData, BlockParameter blockParameter = null)
        {
            var getInitCodeFunction = new GetInitCodeFunction();
                getInitCodeFunction.Salt = salt;
                getInitCodeFunction.InitData = initData;
            
            return ContractHandler.QueryAsync<GetInitCodeFunction, byte[]>(getInitCodeFunction, blockParameter);
        }

        public Task<bool> IsDeployedQueryAsync(IsDeployedFunction isDeployedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsDeployedFunction, bool>(isDeployedFunction, blockParameter);
        }

        
        public virtual Task<bool> IsDeployedQueryAsync(byte[] salt, byte[] initData, BlockParameter blockParameter = null)
        {
            var isDeployedFunction = new IsDeployedFunction();
                isDeployedFunction.Salt = salt;
                isDeployedFunction.InitData = initData;
            
            return ContractHandler.QueryAsync<IsDeployedFunction, bool>(isDeployedFunction, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(AccountImplementationFunction),
                typeof(CreateAccountFunction),
                typeof(EntryPointFunction),
                typeof(GetAddressFunction),
                typeof(GetInitCodeFunction),
                typeof(IsDeployedFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(AccountCreatedEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(AccountCreationFailedError),
                typeof(InvalidEntryPointError),
                typeof(InvalidInitDataError)
            };
        }
    }
}
