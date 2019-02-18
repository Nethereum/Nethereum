using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.ENS.ENSRegistry.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.ENS
{

    public partial class ENSRegistryService
    {
    
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.Web3 web3, ENSRegistryDeployment eNSRegistryDeployment, CancellationToken token = default(CancellationToken))
        {
            return web3.Eth.GetContractDeploymentHandler<ENSRegistryDeployment>().SendRequestAndWaitForReceiptAsync(eNSRegistryDeployment, token);
        }
        public static Task<string> DeployContractAsync(Nethereum.Web3.Web3 web3, ENSRegistryDeployment eNSRegistryDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<ENSRegistryDeployment>().SendRequestAsync(eNSRegistryDeployment);
        }
        public static async Task<ENSRegistryService> DeployContractAndGetServiceAsync(Nethereum.Web3.Web3 web3, ENSRegistryDeployment eNSRegistryDeployment, CancellationToken token = default(CancellationToken))
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, eNSRegistryDeployment, token);
            return new ENSRegistryService(web3, receipt.ContractAddress);
        }
    
        protected Nethereum.Web3.Web3 Web3{ get; }
        
        public ContractHandler ContractHandler { get; }
        
        public ENSRegistryService(Nethereum.Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }
    
        public Task<string> ResolverQueryAsync(ResolverFunction resolverFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ResolverFunction, string>(resolverFunction, blockParameter);
        }

        
        public Task<string> ResolverQueryAsync(byte[] node, BlockParameter blockParameter = null)
        {
            var resolverFunction = new ResolverFunction();
                resolverFunction.Node = node;
            
            return ContractHandler.QueryAsync<ResolverFunction, string>(resolverFunction, blockParameter);
        }



        public Task<string> OwnerQueryAsync(OwnerFunction ownerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }

        
        public Task<string> OwnerQueryAsync(byte[] node, BlockParameter blockParameter = null)
        {
            var ownerFunction = new OwnerFunction();
                ownerFunction.Node = node;
            
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }



        public Task<string> SetSubnodeOwnerRequestAsync(SetSubnodeOwnerFunction setSubnodeOwnerFunction)
        {
             return ContractHandler.SendRequestAsync(setSubnodeOwnerFunction);
        }

        public Task<TransactionReceipt> SetSubnodeOwnerRequestAndWaitForReceiptAsync(SetSubnodeOwnerFunction setSubnodeOwnerFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setSubnodeOwnerFunction, token);
        }

        public Task<string> SetSubnodeOwnerRequestAsync(byte[] node, byte[] label, string owner)
        {
            var setSubnodeOwnerFunction = new SetSubnodeOwnerFunction();
                setSubnodeOwnerFunction.Node = node;
                setSubnodeOwnerFunction.Label = label;
                setSubnodeOwnerFunction.Owner = owner;
            
             return ContractHandler.SendRequestAsync(setSubnodeOwnerFunction);
        }

        public Task<TransactionReceipt> SetSubnodeOwnerRequestAndWaitForReceiptAsync(byte[] node, byte[] label, string owner, CancellationToken token = default(CancellationToken))
        {
            var setSubnodeOwnerFunction = new SetSubnodeOwnerFunction();
                setSubnodeOwnerFunction.Node = node;
                setSubnodeOwnerFunction.Label = label;
                setSubnodeOwnerFunction.Owner = owner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setSubnodeOwnerFunction, token);
        }

        public Task<string> SetTTLRequestAsync(SetTTLFunction setTTLFunction)
        {
             return ContractHandler.SendRequestAsync(setTTLFunction);
        }

        public Task<TransactionReceipt> SetTTLRequestAndWaitForReceiptAsync(SetTTLFunction setTTLFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setTTLFunction, token);
        }

        public Task<string> SetTTLRequestAsync(byte[] node, ulong ttl)
        {
            var setTTLFunction = new SetTTLFunction();
                setTTLFunction.Node = node;
                setTTLFunction.Ttl = ttl;
            
             return ContractHandler.SendRequestAsync(setTTLFunction);
        }

        public Task<TransactionReceipt> SetTTLRequestAndWaitForReceiptAsync(byte[] node, ulong ttl, CancellationToken token = default(CancellationToken))
        {
            var setTTLFunction = new SetTTLFunction();
                setTTLFunction.Node = node;
                setTTLFunction.Ttl = ttl;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setTTLFunction, token);
        }

        public Task<ulong> TtlQueryAsync(TtlFunction ttlFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TtlFunction, ulong>(ttlFunction, blockParameter);
        }

        
        public Task<ulong> TtlQueryAsync(byte[] node, BlockParameter blockParameter = null)
        {
            var ttlFunction = new TtlFunction();
                ttlFunction.Node = node;
            
            return ContractHandler.QueryAsync<TtlFunction, ulong>(ttlFunction, blockParameter);
        }



        public Task<string> SetResolverRequestAsync(SetResolverFunction setResolverFunction)
        {
             return ContractHandler.SendRequestAsync(setResolverFunction);
        }

        public Task<TransactionReceipt> SetResolverRequestAndWaitForReceiptAsync(SetResolverFunction setResolverFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setResolverFunction, token);
        }

        public Task<string> SetResolverRequestAsync(byte[] node, string resolver)
        {
            var setResolverFunction = new SetResolverFunction();
                setResolverFunction.Node = node;
                setResolverFunction.Resolver = resolver;
            
             return ContractHandler.SendRequestAsync(setResolverFunction);
        }

        public Task<TransactionReceipt> SetResolverRequestAndWaitForReceiptAsync(byte[] node, string resolver, CancellationToken token = default(CancellationToken))
        {
            var setResolverFunction = new SetResolverFunction();
                setResolverFunction.Node = node;
                setResolverFunction.Resolver = resolver;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setResolverFunction, token);
        }

        public Task<string> SetOwnerRequestAsync(SetOwnerFunction setOwnerFunction)
        {
             return ContractHandler.SendRequestAsync(setOwnerFunction);
        }

        public Task<TransactionReceipt> SetOwnerRequestAndWaitForReceiptAsync(SetOwnerFunction setOwnerFunction, CancellationToken token = default(CancellationToken))
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setOwnerFunction, token);
        }

        public Task<string> SetOwnerRequestAsync(byte[] node, string owner)
        {
            var setOwnerFunction = new SetOwnerFunction();
                setOwnerFunction.Node = node;
                setOwnerFunction.Owner = owner;
            
             return ContractHandler.SendRequestAsync(setOwnerFunction);
        }

        public Task<TransactionReceipt> SetOwnerRequestAndWaitForReceiptAsync(byte[] node, string owner, CancellationToken token = default(CancellationToken))
        {
            var setOwnerFunction = new SetOwnerFunction();
                setOwnerFunction.Node = node;
                setOwnerFunction.Owner = owner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setOwnerFunction, token);
        }
    }
}
