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
using Nethereum.ENS.DefaultReverseResolver.ContractDefinition;

namespace Nethereum.ENS
{

    public class DefaultReverseResolverService
    {
    
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3.Web3 web3, DefaultReverseResolverDeployment defaultReverseResolverDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<DefaultReverseResolverDeployment>().SendRequestAndWaitForReceiptAsync(defaultReverseResolverDeployment, cancellationTokenSource);
        }
        public static Task<string> DeployContractAsync(Web3.Web3 web3, DefaultReverseResolverDeployment defaultReverseResolverDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<DefaultReverseResolverDeployment>().SendRequestAsync(defaultReverseResolverDeployment);
        }
        public static async Task<DefaultReverseResolverService> DeployContractAndGetServiceAsync(Web3.Web3 web3, DefaultReverseResolverDeployment defaultReverseResolverDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, defaultReverseResolverDeployment, cancellationTokenSource);
            return new DefaultReverseResolverService(web3, receipt.ContractAddress);
        }
    
        protected Web3.Web3 Web3{ get; }
        
        public ContractHandler ContractHandler { get; }
        
        public DefaultReverseResolverService(Web3.Web3 web3, string contractAddress)
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
        public Task<string> NameQueryAsync(NameFunction nameFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(nameFunction, blockParameter);
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
