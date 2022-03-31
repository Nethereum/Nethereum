using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.Services;
using Nethereum.Contracts.Standards.ENS.ENSRegistry.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.Standards.ENS
{
    public partial class ENSRegistryService
    {
        public string ContractAddress { get; }

        public ContractHandler ContractHandler { get; }

        public ENSRegistryService(IEthApiContractService ethApiContractService, string contractAddress)
        {
            ContractAddress = contractAddress;
#if !DOTNET35
            ContractHandler = ethApiContractService.GetContractHandler(contractAddress);
#endif
        }
#if !DOTNET35
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

        public Task<TransactionReceipt> SetSubnodeOwnerRequestAndWaitForReceiptAsync(SetSubnodeOwnerFunction setSubnodeOwnerFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setSubnodeOwnerFunction, cancellationToken);
        }

        public Task<string> SetSubnodeOwnerRequestAsync(byte[] node, byte[] label, string owner)
        {
            var setSubnodeOwnerFunction = new SetSubnodeOwnerFunction();
                setSubnodeOwnerFunction.Node = node;
                setSubnodeOwnerFunction.Label = label;
                setSubnodeOwnerFunction.Owner = owner;
            
             return ContractHandler.SendRequestAsync(setSubnodeOwnerFunction);
        }

        public Task<TransactionReceipt> SetSubnodeOwnerRequestAndWaitForReceiptAsync(byte[] node, byte[] label, string owner, CancellationTokenSource cancellationToken = null)
        {
            var setSubnodeOwnerFunction = new SetSubnodeOwnerFunction();
                setSubnodeOwnerFunction.Node = node;
                setSubnodeOwnerFunction.Label = label;
                setSubnodeOwnerFunction.Owner = owner;
            
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

        public Task<string> SetTTLRequestAsync(byte[] node, ulong ttl)
        {
            var setTTLFunction = new SetTTLFunction();
                setTTLFunction.Node = node;
                setTTLFunction.Ttl = ttl;
            
             return ContractHandler.SendRequestAsync(setTTLFunction);
        }

        public Task<TransactionReceipt> SetTTLRequestAndWaitForReceiptAsync(byte[] node, ulong ttl, CancellationTokenSource cancellationToken = null)
        {
            var setTTLFunction = new SetTTLFunction();
                setTTLFunction.Node = node;
                setTTLFunction.Ttl = ttl;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setTTLFunction, cancellationToken);
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

        public Task<TransactionReceipt> SetResolverRequestAndWaitForReceiptAsync(SetResolverFunction setResolverFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setResolverFunction, cancellationToken);
        }

        public Task<string> SetResolverRequestAsync(byte[] node, string resolver)
        {
            var setResolverFunction = new SetResolverFunction();
                setResolverFunction.Node = node;
                setResolverFunction.Resolver = resolver;
            
             return ContractHandler.SendRequestAsync(setResolverFunction);
        }

        public Task<TransactionReceipt> SetResolverRequestAndWaitForReceiptAsync(byte[] node, string resolver, CancellationTokenSource cancellationToken = null)
        {
            var setResolverFunction = new SetResolverFunction();
                setResolverFunction.Node = node;
                setResolverFunction.Resolver = resolver;
            
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

        public Task<string> SetOwnerRequestAsync(byte[] node, string owner)
        {
            var setOwnerFunction = new SetOwnerFunction();
                setOwnerFunction.Node = node;
                setOwnerFunction.Owner = owner;
            
             return ContractHandler.SendRequestAsync(setOwnerFunction);
        }

        public Task<TransactionReceipt> SetOwnerRequestAndWaitForReceiptAsync(byte[] node, string owner, CancellationTokenSource cancellationToken = null)
        {
            var setOwnerFunction = new SetOwnerFunction();
                setOwnerFunction.Node = node;
                setOwnerFunction.Owner = owner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setOwnerFunction, cancellationToken);
        }
#endif
    }
}
