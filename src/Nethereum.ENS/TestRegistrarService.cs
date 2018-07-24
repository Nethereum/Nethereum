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
using Nethereum.ENS.TestRegistrar.ContractDefinition;

namespace Nethereum.ENS
{ 

    public class TestRegistrarService
    {
    
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3.Web3 web3, TestRegistrarDeployment testRegistrarDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<TestRegistrarDeployment>().SendRequestAndWaitForReceiptAsync(testRegistrarDeployment, cancellationTokenSource);
        }
        public static Task<string> DeployContractAsync(Web3.Web3 web3, TestRegistrarDeployment testRegistrarDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<TestRegistrarDeployment>().SendRequestAsync(testRegistrarDeployment);
        }
        public static async Task<TestRegistrarService> DeployContractAndGetServiceAsync(Web3.Web3 web3, TestRegistrarDeployment testRegistrarDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, testRegistrarDeployment, cancellationTokenSource);
            return new TestRegistrarService(web3, receipt.ContractAddress);
        }
    
        protected Web3.Web3 Web3{ get; }
        
        public ContractHandler ContractHandler { get; }
        
        public TestRegistrarService(Web3.Web3 web3, string contractAddress)
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
        public Task<string> RegisterRequestAsync(RegisterFunction registerFunction)
        {
             return ContractHandler.SendRequestAsync(registerFunction);
        }
        public Task<TransactionReceipt> RegisterRequestAndWaitForReceiptAsync(RegisterFunction registerFunction, CancellationTokenSource cancellationToken = null)
        {
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
