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
using Nethereum.ENS.ReverseRegistrar.ContractDefinition;
namespace Nethereum.ENS
{

    public class ReverseRegistrarService
    {
    
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3.Web3 web3, ReverseRegistrarDeployment reverseRegistrarDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<ReverseRegistrarDeployment>().SendRequestAndWaitForReceiptAsync(reverseRegistrarDeployment, cancellationTokenSource);
        }
        public static Task<string> DeployContractAsync(Web3.Web3 web3, ReverseRegistrarDeployment reverseRegistrarDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<ReverseRegistrarDeployment>().SendRequestAsync(reverseRegistrarDeployment);
        }
        public static async Task<ReverseRegistrarService> DeployContractAndGetServiceAsync(Web3.Web3 web3, ReverseRegistrarDeployment reverseRegistrarDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, reverseRegistrarDeployment, cancellationTokenSource);
            return new ReverseRegistrarService(web3, receipt.ContractAddress);
        }
    
        protected Web3.Web3 Web3{ get; }
        
        public ContractHandler ContractHandler { get; }
        
        public ReverseRegistrarService(Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }
    
        public Task<string> ClaimWithResolverRequestAsync(ClaimWithResolverFunction claimWithResolverFunction)
        {
             return ContractHandler.SendRequestAsync(claimWithResolverFunction);
        }
        public Task<TransactionReceipt> ClaimWithResolverRequestAndWaitForReceiptAsync(ClaimWithResolverFunction claimWithResolverFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(claimWithResolverFunction, cancellationToken);
        }
        public Task<string> ClaimRequestAsync(ClaimFunction claimFunction)
        {
             return ContractHandler.SendRequestAsync(claimFunction);
        }
        public Task<TransactionReceipt> ClaimRequestAndWaitForReceiptAsync(ClaimFunction claimFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(claimFunction, cancellationToken);
        }
        public Task<string> EnsQueryAsync(EnsFunction ensFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EnsFunction, string>(ensFunction, blockParameter);
        }        
        public Task<string> EnsQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EnsFunction, string>(null, blockParameter);
        }
        public Task<string> DefaultResolverQueryAsync(DefaultResolverFunction defaultResolverFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DefaultResolverFunction, string>(defaultResolverFunction, blockParameter);
        }        
        public Task<string> DefaultResolverQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DefaultResolverFunction, string>(null, blockParameter);
        }
        public Task<byte[]> NodeQueryAsync(NodeFunction nodeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NodeFunction, byte[]>(nodeFunction, blockParameter);
        }
        public Task<string> SetNameRequestAsync(SetNameFunction setNameFunction)
        {
             return ContractHandler.SendRequestAsync(setNameFunction);
        }
        public Task<TransactionReceipt> SetNameRequestAndWaitForReceiptAsync(SetNameFunction setNameFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setNameFunction, cancellationToken);
        }
    }
}
