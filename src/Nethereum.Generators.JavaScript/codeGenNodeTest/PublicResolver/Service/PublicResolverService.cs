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
namespace Nethereum.ENS.PublicResolver.Service
{

    public partial class PublicResolverService
    {
    
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3 web3, PublicResolverDeployment publicResolverDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<PublicResolverDeployment>().SendRequestAndWaitForReceiptAsync(publicResolverDeployment, cancellationTokenSource);
        }
        public static Task<string> DeployContractAsync(Web3 web3, PublicResolverDeployment publicResolverDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<PublicResolverDeployment>().SendRequestAsync(publicResolverDeployment);
        }
        public static async Task<PublicResolverService> DeployContractAndGetServiceAsync(Web3 web3, PublicResolverDeployment publicResolverDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, publicResolverDeployment, cancellationTokenSource);
            return new PublicResolverService(web3, receipt.ContractAddress);
        }
    
        protected Web3 Web3{ get; }
        
        public ContractHandler ContractHandler { get; }
        
        public PublicResolverService(Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }
    
        public Task<bool> SupportsInterfaceQueryAsync(SupportsInterfaceFunction supportsInterfaceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        
        public Task<bool> SupportsInterfaceQueryAsync(byte[] interfaceID, BlockParameter blockParameter = null)
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
                supportsInterfaceFunction.InterfaceID = interfaceID;
            
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

        public Task<string> SetTextRequestAsync(byte[] node,string key,string value)
        {
            var setTextFunction = new SetTextFunction();
                setTextFunction.Node = node;
                setTextFunction.Key = key;
                setTextFunction.Value = value;
            
             return ContractHandler.SendRequestAsync(setTextFunction);
        }

        public Task<TransactionReceipt> SetTextRequestAndWaitForReceiptAsync(byte[] node,string key,string value, CancellationTokenSource cancellationToken = null)
        {
            var setTextFunction = new SetTextFunction();
                setTextFunction.Node = node;
                setTextFunction.Key = key;
                setTextFunction.Value = value;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setTextFunction, cancellationToken);
        }

        public Task<ABIOutputDTO> ABIQueryAsync(ABIFunction aBIFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<ABIFunction, ABIOutputDTO>(aBIFunction, blockParameter);
        }

        
        public Task<ABIOutputDTO> ABIQueryAsync(byte[] node,BigInteger contentTypes, BlockParameter blockParameter = null)
        {
            var aBIFunction = new ABIFunction();
                aBIFunction.Node = node;
                aBIFunction.ContentTypes = contentTypes;
            
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

        public Task<string> SetPubkeyRequestAsync(byte[] node,byte[] x,byte[] y)
        {
            var setPubkeyFunction = new SetPubkeyFunction();
                setPubkeyFunction.Node = node;
                setPubkeyFunction.X = x;
                setPubkeyFunction.Y = y;
            
             return ContractHandler.SendRequestAsync(setPubkeyFunction);
        }

        public Task<TransactionReceipt> SetPubkeyRequestAndWaitForReceiptAsync(byte[] node,byte[] x,byte[] y, CancellationTokenSource cancellationToken = null)
        {
            var setPubkeyFunction = new SetPubkeyFunction();
                setPubkeyFunction.Node = node;
                setPubkeyFunction.X = x;
                setPubkeyFunction.Y = y;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setPubkeyFunction, cancellationToken);
        }

        public Task<byte[]> ContentQueryAsync(ContentFunction contentFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ContentFunction, byte[]>(contentFunction, blockParameter);
        }

        
        public Task<byte[]> ContentQueryAsync(byte[] node, BlockParameter blockParameter = null)
        {
            var contentFunction = new ContentFunction();
                contentFunction.Node = node;
            
            return ContractHandler.QueryAsync<ContentFunction, byte[]>(contentFunction, blockParameter);
        }



        public Task<string> AddrQueryAsync(AddrFunction addrFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AddrFunction, string>(addrFunction, blockParameter);
        }

        
        public Task<string> AddrQueryAsync(byte[] node, BlockParameter blockParameter = null)
        {
            var addrFunction = new AddrFunction();
                addrFunction.Node = node;
            
            return ContractHandler.QueryAsync<AddrFunction, string>(addrFunction, blockParameter);
        }



        public Task<string> TextQueryAsync(TextFunction textFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TextFunction, string>(textFunction, blockParameter);
        }

        
        public Task<string> TextQueryAsync(byte[] node,string key, BlockParameter blockParameter = null)
        {
            var textFunction = new TextFunction();
                textFunction.Node = node;
                textFunction.Key = key;
            
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

        public Task<string> SetABIRequestAsync(byte[] node,BigInteger contentType,byte[] data)
        {
            var setABIFunction = new SetABIFunction();
                setABIFunction.Node = node;
                setABIFunction.ContentType = contentType;
                setABIFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(setABIFunction);
        }

        public Task<TransactionReceipt> SetABIRequestAndWaitForReceiptAsync(byte[] node,BigInteger contentType,byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var setABIFunction = new SetABIFunction();
                setABIFunction.Node = node;
                setABIFunction.ContentType = contentType;
                setABIFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setABIFunction, cancellationToken);
        }

        public Task<string> NameQueryAsync(NameFunction nameFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(nameFunction, blockParameter);
        }

        
        public Task<string> NameQueryAsync(byte[] node, BlockParameter blockParameter = null)
        {
            var nameFunction = new NameFunction();
                nameFunction.Node = node;
            
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

        public Task<string> SetNameRequestAsync(byte[] node,string name)
        {
            var setNameFunction = new SetNameFunction();
                setNameFunction.Node = node;
                setNameFunction.Name = name;
            
             return ContractHandler.SendRequestAsync(setNameFunction);
        }

        public Task<TransactionReceipt> SetNameRequestAndWaitForReceiptAsync(byte[] node,string name, CancellationTokenSource cancellationToken = null)
        {
            var setNameFunction = new SetNameFunction();
                setNameFunction.Node = node;
                setNameFunction.Name = name;
            
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

        public Task<string> SetMultihashRequestAsync(byte[] node,byte[] hash)
        {
            var setMultihashFunction = new SetMultihashFunction();
                setMultihashFunction.Node = node;
                setMultihashFunction.Hash = hash;
            
             return ContractHandler.SendRequestAsync(setMultihashFunction);
        }

        public Task<TransactionReceipt> SetMultihashRequestAndWaitForReceiptAsync(byte[] node,byte[] hash, CancellationTokenSource cancellationToken = null)
        {
            var setMultihashFunction = new SetMultihashFunction();
                setMultihashFunction.Node = node;
                setMultihashFunction.Hash = hash;
            
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

        public Task<string> SetContentRequestAsync(byte[] node,byte[] hash)
        {
            var setContentFunction = new SetContentFunction();
                setContentFunction.Node = node;
                setContentFunction.Hash = hash;
            
             return ContractHandler.SendRequestAsync(setContentFunction);
        }

        public Task<TransactionReceipt> SetContentRequestAndWaitForReceiptAsync(byte[] node,byte[] hash, CancellationTokenSource cancellationToken = null)
        {
            var setContentFunction = new SetContentFunction();
                setContentFunction.Node = node;
                setContentFunction.Hash = hash;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setContentFunction, cancellationToken);
        }

        public Task<PubkeyOutputDTO> PubkeyQueryAsync(PubkeyFunction pubkeyFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<PubkeyFunction, PubkeyOutputDTO>(pubkeyFunction, blockParameter);
        }

        
        public Task<PubkeyOutputDTO> PubkeyQueryAsync(byte[] node, BlockParameter blockParameter = null)
        {
            var pubkeyFunction = new PubkeyFunction();
                pubkeyFunction.Node = node;
            
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

        public Task<string> SetAddrRequestAsync(byte[] node,string addr)
        {
            var setAddrFunction = new SetAddrFunction();
                setAddrFunction.Node = node;
                setAddrFunction.Addr = addr;
            
             return ContractHandler.SendRequestAsync(setAddrFunction);
        }

        public Task<TransactionReceipt> SetAddrRequestAndWaitForReceiptAsync(byte[] node,string addr, CancellationTokenSource cancellationToken = null)
        {
            var setAddrFunction = new SetAddrFunction();
                setAddrFunction.Node = node;
                setAddrFunction.Addr = addr;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setAddrFunction, cancellationToken);
        }

        public Task<byte[]> MultihashQueryAsync(MultihashFunction multihashFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MultihashFunction, byte[]>(multihashFunction, blockParameter);
        }

        
        public Task<byte[]> MultihashQueryAsync(byte[] node, BlockParameter blockParameter = null)
        {
            var multihashFunction = new MultihashFunction();
                multihashFunction.Node = node;
            
            return ContractHandler.QueryAsync<MultihashFunction, byte[]>(multihashFunction, blockParameter);
        }


    }
}
