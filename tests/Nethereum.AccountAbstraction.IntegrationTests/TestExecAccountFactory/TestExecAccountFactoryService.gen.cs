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
using Nethereum.AccountAbstraction.IntegrationTests.TestExecAccountFactory.ContractDefinition;

namespace Nethereum.AccountAbstraction.IntegrationTests.TestExecAccountFactory
{
    public partial class TestExecAccountFactoryService: TestExecAccountFactoryServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, TestExecAccountFactoryDeployment testExecAccountFactoryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<TestExecAccountFactoryDeployment>().SendRequestAndWaitForReceiptAsync(testExecAccountFactoryDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, TestExecAccountFactoryDeployment testExecAccountFactoryDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<TestExecAccountFactoryDeployment>().SendRequestAsync(testExecAccountFactoryDeployment);
        }

        public static async Task<TestExecAccountFactoryService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, TestExecAccountFactoryDeployment testExecAccountFactoryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, testExecAccountFactoryDeployment, cancellationTokenSource);
            return new TestExecAccountFactoryService(web3, receipt.ContractAddress);
        }

        public TestExecAccountFactoryService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class TestExecAccountFactoryServiceBase: ContractWeb3ServiceBase
    {

        public TestExecAccountFactoryServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
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

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(AccountImplementationFunction),
                typeof(CreateAccountFunction),
                typeof(GetAddressFunction)
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
