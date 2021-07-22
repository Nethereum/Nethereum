using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using System.Threading;
using Nethereum.Contracts.ContractHandlers;
using SolidityCallAnotherContract.Contracts.TheOther.CQS;
using SolidityCallAnotherContract.Contracts.TheOther.DTOs;
namespace SolidityCallAnotherContract.Contracts.TheOther.Service
{

    public class TheOtherService
    {
    
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3 web3, TheOtherDeployment theOtherDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<TheOtherDeployment>().SendRequestAndWaitForReceiptAsync(theOtherDeployment, cancellationTokenSource);
        }
        public static Task<string> DeployContractAsync(Web3 web3, TheOtherDeployment theOtherDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<TheOtherDeployment>().SendRequestAsync(theOtherDeployment);
        }
        public static async Task<TheOtherService> DeployContractAndGetServiceAsync(Web3 web3, TheOtherDeployment theOtherDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, theOtherDeployment, cancellationTokenSource).ConfigureAwait(false);
            return new TheOtherService(web3, receipt.ContractAddress);
        }
    
        protected Web3 Web3{ get; }
        
        protected ContractHandler ContractHandler { get; }
        
        public TheOtherService(Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }
    
        public Task<byte[]> CallMeQueryAsync(CallMeFunction callMeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CallMeFunction, byte[]>(callMeFunction, blockParameter);
        }
    }
}
