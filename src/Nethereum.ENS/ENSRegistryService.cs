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
using Nethereum.ENS.ENSRegistry.ContractDefinition;

namespace Nethereum.ENS
{

    public class ENSRegistryService
    {
    
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3.Web3 web3, ENSRegistryDeployment eNSRegistryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<ENSRegistryDeployment>().SendRequestAndWaitForReceiptAsync(eNSRegistryDeployment, cancellationTokenSource);
        }
        public static Task<string> DeployContractAsync(Web3.Web3 web3, ENSRegistryDeployment eNSRegistryDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<ENSRegistryDeployment>().SendRequestAsync(eNSRegistryDeployment);
        }
        public static async Task<ENSRegistryService> DeployContractAndGetServiceAsync(Web3.Web3 web3, ENSRegistryDeployment eNSRegistryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, eNSRegistryDeployment, cancellationTokenSource);
            return new ENSRegistryService(web3, receipt.ContractAddress);
        }
    
        protected Web3.Web3 Web3{ get; }
        
        public ContractHandler ContractHandler { get; }
        
        public ENSRegistryService(Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }
    
        public Task<string> ResolverQueryAsync(ResolverFunction resolverFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ResolverFunction, string>(resolverFunction, blockParameter);
        }
        public Task<string> OwnerQueryAsync(OwnerFunction ownerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }
        public Task<string> SetSubnodeOwnerRequestAsync(SetSubnodeOwnerFunction setSubnodeOwnerFunction)
        {
             return ContractHandler.SendRequestAsync(setSubnodeOwnerFunction);
        }
        public Task<TransactionReceipt> SetSubnodeOwnerRequestAndWaitForReceiptAsync(SetSubnodeOwnerFunction setSubnodeOwnerFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setSubnodeOwnerFunction, cancellationToken);
        }
        public Task<string> SetTTLRequestAsync(SetTTLFunction setTTLFunction)
        {
             return ContractHandler.SendRequestAsync(setTTLFunction);
        }
        public Task<TransactionReceipt> SetTTLRequestAndWaitForReceiptAsync(SetTTLFunction setTTLFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setTTLFunction, cancellationToken);
        }
        public Task<ulong> TtlQueryAsync(TtlFunction ttlFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TtlFunction, ulong>(ttlFunction, blockParameter);
        }
        public Task<string> SetResolverRequestAsync(SetResolverFunction setResolverFunction)
        {
             return ContractHandler.SendRequestAsync(setResolverFunction);
        }
        public Task<TransactionReceipt> SetResolverRequestAndWaitForReceiptAsync(SetResolverFunction setResolverFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setResolverFunction, cancellationToken);
        }
        public Task<string> SetOwnerRequestAsync(SetOwnerFunction setOwnerFunction)
        {
             return ContractHandler.SendRequestAsync(setOwnerFunction);
        }
        public Task<TransactionReceipt> SetOwnerRequestAndWaitForReceiptAsync(SetOwnerFunction setOwnerFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setOwnerFunction, cancellationToken);
        }
    }
}
