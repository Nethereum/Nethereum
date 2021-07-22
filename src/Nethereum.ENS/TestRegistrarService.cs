using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.ENS.TestRegistrar.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.ENS
{

    public partial class TestRegistrarService
    {
    
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.Web3 web3, TestRegistrarDeployment testRegistrarDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<TestRegistrarDeployment>().SendRequestAndWaitForReceiptAsync(testRegistrarDeployment, cancellationTokenSource);
        }
        public static Task<string> DeployContractAsync(Nethereum.Web3.Web3 web3, TestRegistrarDeployment testRegistrarDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<TestRegistrarDeployment>().SendRequestAsync(testRegistrarDeployment);
        }
        public static async Task<TestRegistrarService> DeployContractAndGetServiceAsync(Nethereum.Web3.Web3 web3, TestRegistrarDeployment testRegistrarDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, testRegistrarDeployment, cancellationTokenSource).ConfigureAwait(false);
            return new TestRegistrarService(web3, receipt.ContractAddress);
        }
    
        protected Nethereum.Web3.Web3 Web3{ get; }
        
        public ContractHandler ContractHandler { get; }
        
        public TestRegistrarService(Nethereum.Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }
    
        public Task<string> EnsQueryAsync(EnsFunction ensFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EnsFunction, string>(ensFunction, blockParameter);
        }

        
        public Task<string> EnsQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EnsFunction, string>(null, blockParameter);
        }



        public Task<BigInteger> ExpiryTimesQueryAsync(ExpiryTimesFunction expiryTimesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ExpiryTimesFunction, BigInteger>(expiryTimesFunction, blockParameter);
        }

        
        public Task<BigInteger> ExpiryTimesQueryAsync(byte[] returnValue1, BlockParameter blockParameter = null)
        {
            var expiryTimesFunction = new ExpiryTimesFunction();
                expiryTimesFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<ExpiryTimesFunction, BigInteger>(expiryTimesFunction, blockParameter);
        }



        public Task<string> RegisterRequestAsync(RegisterFunction registerFunction)
        {
             return ContractHandler.SendRequestAsync(registerFunction);
        }

        public Task<TransactionReceipt> RegisterRequestAndWaitForReceiptAsync(RegisterFunction registerFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerFunction, cancellationToken);
        }

        public Task<string> RegisterRequestAsync(byte[] subnode, string owner)
        {
            var registerFunction = new RegisterFunction();
                registerFunction.Subnode = subnode;
                registerFunction.Owner = owner;
            
             return ContractHandler.SendRequestAsync(registerFunction);
        }

        public Task<TransactionReceipt> RegisterRequestAndWaitForReceiptAsync(byte[] subnode, string owner, CancellationTokenSource cancellationToken = null)
        {
            var registerFunction = new RegisterFunction();
                registerFunction.Subnode = subnode;
                registerFunction.Owner = owner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerFunction, cancellationToken);
        }

        public Task<byte[]> RootNodeQueryAsync(RootNodeFunction rootNodeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RootNodeFunction, byte[]>(rootNodeFunction, blockParameter);
        }

        
        public Task<byte[]> RootNodeQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RootNodeFunction, byte[]>(null, blockParameter);
        }


    }
}
