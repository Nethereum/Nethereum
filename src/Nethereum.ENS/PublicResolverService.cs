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
using Nethereum.ENS.PublicResolver.ContractDefinition;

namespace Nethereum.ENS
{
    public class PublicResolverService
    {
    
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3.Web3 web3, PublicResolverDeployment publicResolverDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<PublicResolverDeployment>().SendRequestAndWaitForReceiptAsync(publicResolverDeployment, cancellationTokenSource);
        }
        public static Task<string> DeployContractAsync(Web3.Web3 web3, PublicResolverDeployment publicResolverDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<PublicResolverDeployment>().SendRequestAsync(publicResolverDeployment);
        }
        public static async Task<PublicResolverService> DeployContractAndGetServiceAsync(Web3.Web3 web3, PublicResolverDeployment publicResolverDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, publicResolverDeployment, cancellationTokenSource);
            return new PublicResolverService(web3, receipt.ContractAddress);
        }
    
        protected Web3.Web3 Web3{ get; }
        
        public ContractHandler ContractHandler { get; }
        
        public PublicResolverService(Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }
    
        public Task<bool> SupportsInterfaceQueryAsync(SupportsInterfaceFunction supportsInterfaceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }
        public Task<string> SetTextRequestAsync(SetTextFunction setTextFunction)
        {
             return ContractHandler.SendRequestAsync(setTextFunction);
        }
        public Task<TransactionReceipt> SetTextRequestAndWaitForReceiptAsync(SetTextFunction setTextFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setTextFunction, cancellationToken);
        }
        public Task<ABIOutputDTO> ABIQueryAsync(ABIFunction aBIFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<ABIFunction, ABIOutputDTO>(aBIFunction, blockParameter);
        }
        public Task<string> SetPubkeyRequestAsync(SetPubkeyFunction setPubkeyFunction)
        {
             return ContractHandler.SendRequestAsync(setPubkeyFunction);
        }
        public Task<TransactionReceipt> SetPubkeyRequestAndWaitForReceiptAsync(SetPubkeyFunction setPubkeyFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setPubkeyFunction, cancellationToken);
        }
        public Task<byte[]> ContentQueryAsync(ContentFunction contentFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ContentFunction, byte[]>(contentFunction, blockParameter);
        }
        public Task<string> AddrQueryAsync(AddrFunction addrFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AddrFunction, string>(addrFunction, blockParameter);
        }
        public Task<string> TextQueryAsync(TextFunction textFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TextFunction, string>(textFunction, blockParameter);
        }
        public Task<string> SetABIRequestAsync(SetABIFunction setABIFunction)
        {
             return ContractHandler.SendRequestAsync(setABIFunction);
        }
        public Task<TransactionReceipt> SetABIRequestAndWaitForReceiptAsync(SetABIFunction setABIFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setABIFunction, cancellationToken);
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
        public Task<string> SetMultihashRequestAsync(SetMultihashFunction setMultihashFunction)
        {
             return ContractHandler.SendRequestAsync(setMultihashFunction);
        }
        public Task<TransactionReceipt> SetMultihashRequestAndWaitForReceiptAsync(SetMultihashFunction setMultihashFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setMultihashFunction, cancellationToken);
        }
        public Task<string> SetContentRequestAsync(SetContentFunction setContentFunction)
        {
             return ContractHandler.SendRequestAsync(setContentFunction);
        }
        public Task<TransactionReceipt> SetContentRequestAndWaitForReceiptAsync(SetContentFunction setContentFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setContentFunction, cancellationToken);
        }
        public Task<PubkeyOutputDTO> PubkeyQueryAsync(PubkeyFunction pubkeyFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<PubkeyFunction, PubkeyOutputDTO>(pubkeyFunction, blockParameter);
        }
        public Task<string> SetAddrRequestAsync(SetAddrFunction setAddrFunction)
        {
             return ContractHandler.SendRequestAsync(setAddrFunction);
        }
        public Task<TransactionReceipt> SetAddrRequestAndWaitForReceiptAsync(SetAddrFunction setAddrFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setAddrFunction, cancellationToken);
        }
        public Task<byte[]> MultihashQueryAsync(MultihashFunction multihashFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MultihashFunction, byte[]>(multihashFunction, blockParameter);
        }
    }
}
