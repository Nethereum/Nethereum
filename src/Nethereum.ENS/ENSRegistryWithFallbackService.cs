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
using Nethereum.ENS.ENSRegistryWithFallback.ContractDefinition;

namespace Nethereum.ENS
{
    public partial class ENSRegistryWithFallbackService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.Web3 web3, ENSRegistryWithFallbackDeployment eNSRegistryWithFallbackDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<ENSRegistryWithFallbackDeployment>().SendRequestAndWaitForReceiptAsync(eNSRegistryWithFallbackDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.Web3 web3, ENSRegistryWithFallbackDeployment eNSRegistryWithFallbackDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<ENSRegistryWithFallbackDeployment>().SendRequestAsync(eNSRegistryWithFallbackDeployment);
        }

        public static async Task<ENSRegistryWithFallbackService> DeployContractAndGetServiceAsync(Nethereum.Web3.Web3 web3, ENSRegistryWithFallbackDeployment eNSRegistryWithFallbackDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, eNSRegistryWithFallbackDeployment, cancellationTokenSource).ConfigureAwait(false);
            return new ENSRegistryWithFallbackService(web3, receipt.ContractAddress);
        }

        protected Nethereum.Web3.Web3 Web3 { get; }

        public ContractHandler ContractHandler { get; }

        public ENSRegistryWithFallbackService(Nethereum.Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<bool> IsApprovedForAllQueryAsync(IsApprovedForAllFunction isApprovedForAllFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsApprovedForAllFunction, bool>(isApprovedForAllFunction, blockParameter);
        }


        public Task<bool> IsApprovedForAllQueryAsync(string owner, string operatorx, BlockParameter blockParameter = null)
        {
            var isApprovedForAllFunction = new IsApprovedForAllFunction();
            isApprovedForAllFunction.Owner = owner;
            isApprovedForAllFunction.Operator = operatorx;

            return ContractHandler.QueryAsync<IsApprovedForAllFunction, bool>(isApprovedForAllFunction, blockParameter);
        }

        public Task<string> OldQueryAsync(OldFunction oldFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OldFunction, string>(oldFunction, blockParameter);
        }


        public Task<string> OldQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OldFunction, string>(null, blockParameter);
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

        public Task<bool> RecordExistsQueryAsync(RecordExistsFunction recordExistsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RecordExistsFunction, bool>(recordExistsFunction, blockParameter);
        }


        public Task<bool> RecordExistsQueryAsync(byte[] node, BlockParameter blockParameter = null)
        {
            var recordExistsFunction = new RecordExistsFunction();
            recordExistsFunction.Node = node;

            return ContractHandler.QueryAsync<RecordExistsFunction, bool>(recordExistsFunction, blockParameter);
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

        public Task<string> SetApprovalForAllRequestAsync(SetApprovalForAllFunction setApprovalForAllFunction)
        {
            return ContractHandler.SendRequestAsync(setApprovalForAllFunction);
        }

        public Task<TransactionReceipt> SetApprovalForAllRequestAndWaitForReceiptAsync(SetApprovalForAllFunction setApprovalForAllFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(setApprovalForAllFunction, cancellationToken);
        }

        public Task<string> SetApprovalForAllRequestAsync(string operatorx, bool approved)
        {
            var setApprovalForAllFunction = new SetApprovalForAllFunction();
            setApprovalForAllFunction.Operator = operatorx;
            setApprovalForAllFunction.Approved = approved;

            return ContractHandler.SendRequestAsync(setApprovalForAllFunction);
        }

        public Task<TransactionReceipt> SetApprovalForAllRequestAndWaitForReceiptAsync(string operatorx, bool approved, CancellationTokenSource cancellationToken = null)
        {
            var setApprovalForAllFunction = new SetApprovalForAllFunction();
            setApprovalForAllFunction.Operator = operatorx;
            setApprovalForAllFunction.Approved = approved;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(setApprovalForAllFunction, cancellationToken);
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

        public Task<string> SetRecordRequestAsync(SetRecordFunction setRecordFunction)
        {
            return ContractHandler.SendRequestAsync(setRecordFunction);
        }

        public Task<TransactionReceipt> SetRecordRequestAndWaitForReceiptAsync(SetRecordFunction setRecordFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(setRecordFunction, cancellationToken);
        }

        public Task<string> SetRecordRequestAsync(byte[] node, string owner, string resolver, ulong ttl)
        {
            var setRecordFunction = new SetRecordFunction();
            setRecordFunction.Node = node;
            setRecordFunction.Owner = owner;
            setRecordFunction.Resolver = resolver;
            setRecordFunction.Ttl = ttl;

            return ContractHandler.SendRequestAsync(setRecordFunction);
        }

        public Task<TransactionReceipt> SetRecordRequestAndWaitForReceiptAsync(byte[] node, string owner, string resolver, ulong ttl, CancellationTokenSource cancellationToken = null)
        {
            var setRecordFunction = new SetRecordFunction();
            setRecordFunction.Node = node;
            setRecordFunction.Owner = owner;
            setRecordFunction.Resolver = resolver;
            setRecordFunction.Ttl = ttl;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(setRecordFunction, cancellationToken);
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

        public Task<string> SetSubnodeRecordRequestAsync(SetSubnodeRecordFunction setSubnodeRecordFunction)
        {
            return ContractHandler.SendRequestAsync(setSubnodeRecordFunction);
        }

        public Task<TransactionReceipt> SetSubnodeRecordRequestAndWaitForReceiptAsync(SetSubnodeRecordFunction setSubnodeRecordFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(setSubnodeRecordFunction, cancellationToken);
        }

        public Task<string> SetSubnodeRecordRequestAsync(byte[] node, byte[] label, string owner, string resolver, ulong ttl)
        {
            var setSubnodeRecordFunction = new SetSubnodeRecordFunction();
            setSubnodeRecordFunction.Node = node;
            setSubnodeRecordFunction.Label = label;
            setSubnodeRecordFunction.Owner = owner;
            setSubnodeRecordFunction.Resolver = resolver;
            setSubnodeRecordFunction.Ttl = ttl;

            return ContractHandler.SendRequestAsync(setSubnodeRecordFunction);
        }

        public Task<TransactionReceipt> SetSubnodeRecordRequestAndWaitForReceiptAsync(byte[] node, byte[] label, string owner, string resolver, ulong ttl, CancellationTokenSource cancellationToken = null)
        {
            var setSubnodeRecordFunction = new SetSubnodeRecordFunction();
            setSubnodeRecordFunction.Node = node;
            setSubnodeRecordFunction.Label = label;
            setSubnodeRecordFunction.Owner = owner;
            setSubnodeRecordFunction.Resolver = resolver;
            setSubnodeRecordFunction.Ttl = ttl;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(setSubnodeRecordFunction, cancellationToken);
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
    }
}
