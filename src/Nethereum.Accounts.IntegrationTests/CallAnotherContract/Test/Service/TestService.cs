using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using System.Threading;
using Nethereum.Contracts.ContractHandlers;
using SolidityCallAnotherContract.Contracts.Test.CQS;
using SolidityCallAnotherContract.Contracts.Test.DTOs;
namespace SolidityCallAnotherContract.Contracts.Test.Service
{

    public class TestService
    {
    
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3 web3, TestDeployment testDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<TestDeployment>().SendRequestAndWaitForReceiptAsync(testDeployment, cancellationTokenSource);
        }
        public static Task<string> DeployContractAsync(Web3 web3, TestDeployment testDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<TestDeployment>().SendRequestAsync(testDeployment);
        }
        public static async Task<TestService> DeployContractAndGetServiceAsync(Web3 web3, TestDeployment testDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, testDeployment, cancellationTokenSource);
            return new TestService(web3, receipt.ContractAddress);
        }
    
        protected Web3 Web3{ get; }
        
        protected ContractHandler ContractHandler { get; }
        
        public TestService(Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }
    
        public Task<List<byte[]>> CallManyContractsVariableReturnQueryAsync(CallManyContractsVariableReturnFunction callManyContractsVariableReturnFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CallManyContractsVariableReturnFunction, List<byte[]>>(callManyContractsVariableReturnFunction, blockParameter);
        }
        public Task<List<byte[]>> CallManyOtherContractsVariableArrayReturnQueryAsync(CallManyOtherContractsVariableArrayReturnFunction callManyOtherContractsVariableArrayReturnFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CallManyOtherContractsVariableArrayReturnFunction, List<byte[]>>(callManyOtherContractsVariableArrayReturnFunction, blockParameter);
        }
        public Task<List<byte[]>> CallManyContractsSameQueryQueryAsync(CallManyContractsSameQueryFunction callManyContractsSameQueryFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CallManyContractsSameQueryFunction, List<byte[]>>(callManyContractsSameQueryFunction, blockParameter);
        }
        public Task<List<byte[]>> CallManyOtherContractsFixedArrayReturnQueryAsync(CallManyOtherContractsFixedArrayReturnFunction callManyOtherContractsFixedArrayReturnFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CallManyOtherContractsFixedArrayReturnFunction, List<byte[]>>(callManyOtherContractsFixedArrayReturnFunction, blockParameter);
        }
        public Task<byte[]> CallAnotherContractQueryAsync(CallAnotherContractFunction callAnotherContractFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CallAnotherContractFunction, byte[]>(callAnotherContractFunction, blockParameter);
        }
    }
}
