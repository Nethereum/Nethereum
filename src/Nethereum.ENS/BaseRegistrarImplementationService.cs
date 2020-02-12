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
using Nethereum.ENS.BaseRegistrarImplementation.ContractDefinition;

namespace Nethereum.ENS
{
    public partial class BaseRegistrarImplementationService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.Web3 web3, BaseRegistrarImplementationDeployment baseRegistrarImplementationDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<BaseRegistrarImplementationDeployment>().SendRequestAndWaitForReceiptAsync(baseRegistrarImplementationDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.Web3 web3, BaseRegistrarImplementationDeployment baseRegistrarImplementationDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<BaseRegistrarImplementationDeployment>().SendRequestAsync(baseRegistrarImplementationDeployment);
        }

        public static async Task<BaseRegistrarImplementationService> DeployContractAndGetServiceAsync(Nethereum.Web3.Web3 web3, BaseRegistrarImplementationDeployment baseRegistrarImplementationDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, baseRegistrarImplementationDeployment, cancellationTokenSource);
            return new BaseRegistrarImplementationService(web3, receipt.ContractAddress);
        }

        protected Nethereum.Web3.Web3 Web3 { get; }

        public ContractHandler ContractHandler { get; }

        public BaseRegistrarImplementationService(Nethereum.Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<BigInteger> GRACE_PERIODQueryAsync(GRACE_PERIODFunction gRACE_PERIODFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GRACE_PERIODFunction, BigInteger>(gRACE_PERIODFunction, blockParameter);
        }


        public Task<BigInteger> GRACE_PERIODQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GRACE_PERIODFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> AddControllerRequestAsync(AddControllerFunction addControllerFunction)
        {
            return ContractHandler.SendRequestAsync(addControllerFunction);
        }

        public Task<TransactionReceipt> AddControllerRequestAndWaitForReceiptAsync(AddControllerFunction addControllerFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(addControllerFunction, cancellationToken);
        }

        public Task<string> AddControllerRequestAsync(string controller)
        {
            var addControllerFunction = new AddControllerFunction();
            addControllerFunction.Controller = controller;

            return ContractHandler.SendRequestAsync(addControllerFunction);
        }

        public Task<TransactionReceipt> AddControllerRequestAndWaitForReceiptAsync(string controller, CancellationTokenSource cancellationToken = null)
        {
            var addControllerFunction = new AddControllerFunction();
            addControllerFunction.Controller = controller;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(addControllerFunction, cancellationToken);
        }

        public Task<string> ApproveRequestAsync(ApproveFunction approveFunction)
        {
            return ContractHandler.SendRequestAsync(approveFunction);
        }

        public Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(ApproveFunction approveFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(approveFunction, cancellationToken);
        }

        public Task<string> ApproveRequestAsync(string to, BigInteger tokenId)
        {
            var approveFunction = new ApproveFunction();
            approveFunction.To = to;
            approveFunction.TokenId = tokenId;

            return ContractHandler.SendRequestAsync(approveFunction);
        }

        public Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(string to, BigInteger tokenId, CancellationTokenSource cancellationToken = null)
        {
            var approveFunction = new ApproveFunction();
            approveFunction.To = to;
            approveFunction.TokenId = tokenId;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(approveFunction, cancellationToken);
        }

        public Task<bool> AvailableQueryAsync(AvailableFunction availableFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AvailableFunction, bool>(availableFunction, blockParameter);
        }


        public Task<bool> AvailableQueryAsync(BigInteger id, BlockParameter blockParameter = null)
        {
            var availableFunction = new AvailableFunction();
            availableFunction.Id = id;

            return ContractHandler.QueryAsync<AvailableFunction, bool>(availableFunction, blockParameter);
        }

        public Task<BigInteger> BalanceOfQueryAsync(BalanceOfFunction balanceOfFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }


        public Task<BigInteger> BalanceOfQueryAsync(string owner, BlockParameter blockParameter = null)
        {
            var balanceOfFunction = new BalanceOfFunction();
            balanceOfFunction.Owner = owner;

            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }

        public Task<byte[]> BaseNodeQueryAsync(BaseNodeFunction baseNodeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BaseNodeFunction, byte[]>(baseNodeFunction, blockParameter);
        }


        public Task<byte[]> BaseNodeQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BaseNodeFunction, byte[]>(null, blockParameter);
        }

        public Task<bool> ControllersQueryAsync(ControllersFunction controllersFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ControllersFunction, bool>(controllersFunction, blockParameter);
        }


        public Task<bool> ControllersQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var controllersFunction = new ControllersFunction();
            controllersFunction.ReturnValue1 = returnValue1;

            return ContractHandler.QueryAsync<ControllersFunction, bool>(controllersFunction, blockParameter);
        }

        public Task<string> EnsQueryAsync(EnsFunction ensFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EnsFunction, string>(ensFunction, blockParameter);
        }


        public Task<string> EnsQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EnsFunction, string>(null, blockParameter);
        }

        public Task<string> GetApprovedQueryAsync(GetApprovedFunction getApprovedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetApprovedFunction, string>(getApprovedFunction, blockParameter);
        }


        public Task<string> GetApprovedQueryAsync(BigInteger tokenId, BlockParameter blockParameter = null)
        {
            var getApprovedFunction = new GetApprovedFunction();
            getApprovedFunction.TokenId = tokenId;

            return ContractHandler.QueryAsync<GetApprovedFunction, string>(getApprovedFunction, blockParameter);
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

        public Task<bool> IsOwnerQueryAsync(IsOwnerFunction isOwnerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsOwnerFunction, bool>(isOwnerFunction, blockParameter);
        }


        public Task<bool> IsOwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsOwnerFunction, bool>(null, blockParameter);
        }

        public Task<BigInteger> NameExpiresQueryAsync(NameExpiresFunction nameExpiresFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameExpiresFunction, BigInteger>(nameExpiresFunction, blockParameter);
        }


        public Task<BigInteger> NameExpiresQueryAsync(BigInteger id, BlockParameter blockParameter = null)
        {
            var nameExpiresFunction = new NameExpiresFunction();
            nameExpiresFunction.Id = id;

            return ContractHandler.QueryAsync<NameExpiresFunction, BigInteger>(nameExpiresFunction, blockParameter);
        }

        public Task<string> OwnerQueryAsync(OwnerFunction ownerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }


        public Task<string> OwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(null, blockParameter);
        }

        public Task<string> OwnerOfQueryAsync(OwnerOfFunction ownerOfFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerOfFunction, string>(ownerOfFunction, blockParameter);
        }


        public Task<string> OwnerOfQueryAsync(BigInteger tokenId, BlockParameter blockParameter = null)
        {
            var ownerOfFunction = new OwnerOfFunction();
            ownerOfFunction.TokenId = tokenId;

            return ContractHandler.QueryAsync<OwnerOfFunction, string>(ownerOfFunction, blockParameter);
        }

        public Task<string> ReclaimRequestAsync(ReclaimFunction reclaimFunction)
        {
            return ContractHandler.SendRequestAsync(reclaimFunction);
        }

        public Task<TransactionReceipt> ReclaimRequestAndWaitForReceiptAsync(ReclaimFunction reclaimFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(reclaimFunction, cancellationToken);
        }

        public Task<string> ReclaimRequestAsync(BigInteger id, string owner)
        {
            var reclaimFunction = new ReclaimFunction();
            reclaimFunction.Id = id;
            reclaimFunction.Owner = owner;

            return ContractHandler.SendRequestAsync(reclaimFunction);
        }

        public Task<TransactionReceipt> ReclaimRequestAndWaitForReceiptAsync(BigInteger id, string owner, CancellationTokenSource cancellationToken = null)
        {
            var reclaimFunction = new ReclaimFunction();
            reclaimFunction.Id = id;
            reclaimFunction.Owner = owner;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(reclaimFunction, cancellationToken);
        }

        public Task<string> RegisterRequestAsync(RegisterFunction registerFunction)
        {
            return ContractHandler.SendRequestAsync(registerFunction);
        }

        public Task<TransactionReceipt> RegisterRequestAndWaitForReceiptAsync(RegisterFunction registerFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(registerFunction, cancellationToken);
        }

        public Task<string> RegisterRequestAsync(BigInteger id, string owner, BigInteger duration)
        {
            var registerFunction = new RegisterFunction();
            registerFunction.Id = id;
            registerFunction.Owner = owner;
            registerFunction.Duration = duration;

            return ContractHandler.SendRequestAsync(registerFunction);
        }

        public Task<TransactionReceipt> RegisterRequestAndWaitForReceiptAsync(BigInteger id, string owner, BigInteger duration, CancellationTokenSource cancellationToken = null)
        {
            var registerFunction = new RegisterFunction();
            registerFunction.Id = id;
            registerFunction.Owner = owner;
            registerFunction.Duration = duration;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(registerFunction, cancellationToken);
        }

        public Task<string> RegisterOnlyRequestAsync(RegisterOnlyFunction registerOnlyFunction)
        {
            return ContractHandler.SendRequestAsync(registerOnlyFunction);
        }

        public Task<TransactionReceipt> RegisterOnlyRequestAndWaitForReceiptAsync(RegisterOnlyFunction registerOnlyFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(registerOnlyFunction, cancellationToken);
        }

        public Task<string> RegisterOnlyRequestAsync(BigInteger id, string owner, BigInteger duration)
        {
            var registerOnlyFunction = new RegisterOnlyFunction();
            registerOnlyFunction.Id = id;
            registerOnlyFunction.Owner = owner;
            registerOnlyFunction.Duration = duration;

            return ContractHandler.SendRequestAsync(registerOnlyFunction);
        }

        public Task<TransactionReceipt> RegisterOnlyRequestAndWaitForReceiptAsync(BigInteger id, string owner, BigInteger duration, CancellationTokenSource cancellationToken = null)
        {
            var registerOnlyFunction = new RegisterOnlyFunction();
            registerOnlyFunction.Id = id;
            registerOnlyFunction.Owner = owner;
            registerOnlyFunction.Duration = duration;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(registerOnlyFunction, cancellationToken);
        }

        public Task<string> RemoveControllerRequestAsync(RemoveControllerFunction removeControllerFunction)
        {
            return ContractHandler.SendRequestAsync(removeControllerFunction);
        }

        public Task<TransactionReceipt> RemoveControllerRequestAndWaitForReceiptAsync(RemoveControllerFunction removeControllerFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(removeControllerFunction, cancellationToken);
        }

        public Task<string> RemoveControllerRequestAsync(string controller)
        {
            var removeControllerFunction = new RemoveControllerFunction();
            removeControllerFunction.Controller = controller;

            return ContractHandler.SendRequestAsync(removeControllerFunction);
        }

        public Task<TransactionReceipt> RemoveControllerRequestAndWaitForReceiptAsync(string controller, CancellationTokenSource cancellationToken = null)
        {
            var removeControllerFunction = new RemoveControllerFunction();
            removeControllerFunction.Controller = controller;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(removeControllerFunction, cancellationToken);
        }

        public Task<string> RenewRequestAsync(RenewFunction renewFunction)
        {
            return ContractHandler.SendRequestAsync(renewFunction);
        }

        public Task<TransactionReceipt> RenewRequestAndWaitForReceiptAsync(RenewFunction renewFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(renewFunction, cancellationToken);
        }

        public Task<string> RenewRequestAsync(BigInteger id, BigInteger duration)
        {
            var renewFunction = new RenewFunction();
            renewFunction.Id = id;
            renewFunction.Duration = duration;

            return ContractHandler.SendRequestAsync(renewFunction);
        }

        public Task<TransactionReceipt> RenewRequestAndWaitForReceiptAsync(BigInteger id, BigInteger duration, CancellationTokenSource cancellationToken = null)
        {
            var renewFunction = new RenewFunction();
            renewFunction.Id = id;
            renewFunction.Duration = duration;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(renewFunction, cancellationToken);
        }

        public Task<string> RenounceOwnershipRequestAsync(RenounceOwnershipFunction renounceOwnershipFunction)
        {
            return ContractHandler.SendRequestAsync(renounceOwnershipFunction);
        }

        public Task<string> RenounceOwnershipRequestAsync()
        {
            return ContractHandler.SendRequestAsync<RenounceOwnershipFunction>();
        }

        public Task<TransactionReceipt> RenounceOwnershipRequestAndWaitForReceiptAsync(RenounceOwnershipFunction renounceOwnershipFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(renounceOwnershipFunction, cancellationToken);
        }

        public Task<TransactionReceipt> RenounceOwnershipRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync<RenounceOwnershipFunction>(null, cancellationToken);
        }

        public Task<string> SafeTransferFromRequestAsync(SafeTransferFromFunction safeTransferFromFunction)
        {
            return ContractHandler.SendRequestAsync(safeTransferFromFunction);
        }

        public Task<TransactionReceipt> SafeTransferFromRequestAndWaitForReceiptAsync(SafeTransferFromFunction safeTransferFromFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(safeTransferFromFunction, cancellationToken);
        }

        public Task<string> SafeTransferFromRequestAsync(string from, string to, BigInteger tokenId)
        {
            var safeTransferFromFunction = new SafeTransferFromFunction();
            safeTransferFromFunction.From = from;
            safeTransferFromFunction.To = to;
            safeTransferFromFunction.TokenId = tokenId;

            return ContractHandler.SendRequestAsync(safeTransferFromFunction);
        }

        public Task<TransactionReceipt> SafeTransferFromRequestAndWaitForReceiptAsync(string from, string to, BigInteger tokenId, CancellationTokenSource cancellationToken = null)
        {
            var safeTransferFromFunction = new SafeTransferFromFunction();
            safeTransferFromFunction.From = from;
            safeTransferFromFunction.To = to;
            safeTransferFromFunction.TokenId = tokenId;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(safeTransferFromFunction, cancellationToken);
        }

        public Task<string> SafeTransferFromRequestAsync(SafeTransferFromFunction2 safeTransferFromFunction)
        {
            return ContractHandler.SendRequestAsync(safeTransferFromFunction);
        }

        public Task<TransactionReceipt> SafeTransferFromRequestAndWaitForReceiptAsync(SafeTransferFromFunction2 safeTransferFromFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(safeTransferFromFunction, cancellationToken);
        }

        public Task<string> SafeTransferFromRequestAsync(string from, string to, BigInteger tokenId, byte[] data)
        {
            var safeTransferFromFunction = new SafeTransferFromFunction2();
            safeTransferFromFunction.From = from;
            safeTransferFromFunction.To = to;
            safeTransferFromFunction.TokenId = tokenId;
            safeTransferFromFunction.Data = data;

            return ContractHandler.SendRequestAsync(safeTransferFromFunction);
        }

        public Task<TransactionReceipt> SafeTransferFromRequestAndWaitForReceiptAsync(string from, string to, BigInteger tokenId, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var safeTransferFromFunction = new SafeTransferFromFunction2();
            safeTransferFromFunction.From = from;
            safeTransferFromFunction.To = to;
            safeTransferFromFunction.TokenId = tokenId;
            safeTransferFromFunction.Data = data;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(safeTransferFromFunction, cancellationToken);
        }

        public Task<string> SetApprovalForAllRequestAsync(SetApprovalForAllFunction setApprovalForAllFunction)
        {
            return ContractHandler.SendRequestAsync(setApprovalForAllFunction);
        }

        public Task<TransactionReceipt> SetApprovalForAllRequestAndWaitForReceiptAsync(SetApprovalForAllFunction setApprovalForAllFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(setApprovalForAllFunction, cancellationToken);
        }

        public Task<string> SetApprovalForAllRequestAsync(string to, bool approved)
        {
            var setApprovalForAllFunction = new SetApprovalForAllFunction();
            setApprovalForAllFunction.To = to;
            setApprovalForAllFunction.Approved = approved;

            return ContractHandler.SendRequestAsync(setApprovalForAllFunction);
        }

        public Task<TransactionReceipt> SetApprovalForAllRequestAndWaitForReceiptAsync(string to, bool approved, CancellationTokenSource cancellationToken = null)
        {
            var setApprovalForAllFunction = new SetApprovalForAllFunction();
            setApprovalForAllFunction.To = to;
            setApprovalForAllFunction.Approved = approved;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(setApprovalForAllFunction, cancellationToken);
        }

        public Task<string> SetResolverRequestAsync(SetResolverFunction setResolverFunction)
        {
            return ContractHandler.SendRequestAsync(setResolverFunction);
        }

        public Task<TransactionReceipt> SetResolverRequestAndWaitForReceiptAsync(SetResolverFunction setResolverFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(setResolverFunction, cancellationToken);
        }

        public Task<string> SetResolverRequestAsync(string resolver)
        {
            var setResolverFunction = new SetResolverFunction();
            setResolverFunction.Resolver = resolver;

            return ContractHandler.SendRequestAsync(setResolverFunction);
        }

        public Task<TransactionReceipt> SetResolverRequestAndWaitForReceiptAsync(string resolver, CancellationTokenSource cancellationToken = null)
        {
            var setResolverFunction = new SetResolverFunction();
            setResolverFunction.Resolver = resolver;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(setResolverFunction, cancellationToken);
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

        public Task<string> TransferFromRequestAsync(TransferFromFunction transferFromFunction)
        {
            return ContractHandler.SendRequestAsync(transferFromFunction);
        }

        public Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(TransferFromFunction transferFromFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFromFunction, cancellationToken);
        }

        public Task<string> TransferFromRequestAsync(string from, string to, BigInteger tokenId)
        {
            var transferFromFunction = new TransferFromFunction();
            transferFromFunction.From = from;
            transferFromFunction.To = to;
            transferFromFunction.TokenId = tokenId;

            return ContractHandler.SendRequestAsync(transferFromFunction);
        }

        public Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(string from, string to, BigInteger tokenId, CancellationTokenSource cancellationToken = null)
        {
            var transferFromFunction = new TransferFromFunction();
            transferFromFunction.From = from;
            transferFromFunction.To = to;
            transferFromFunction.TokenId = tokenId;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFromFunction, cancellationToken);
        }

        public Task<string> TransferOwnershipRequestAsync(TransferOwnershipFunction transferOwnershipFunction)
        {
            return ContractHandler.SendRequestAsync(transferOwnershipFunction);
        }

        public Task<TransactionReceipt> TransferOwnershipRequestAndWaitForReceiptAsync(TransferOwnershipFunction transferOwnershipFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(transferOwnershipFunction, cancellationToken);
        }

        public Task<string> TransferOwnershipRequestAsync(string newOwner)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
            transferOwnershipFunction.NewOwner = newOwner;

            return ContractHandler.SendRequestAsync(transferOwnershipFunction);
        }

        public Task<TransactionReceipt> TransferOwnershipRequestAndWaitForReceiptAsync(string newOwner, CancellationTokenSource cancellationToken = null)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
            transferOwnershipFunction.NewOwner = newOwner;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(transferOwnershipFunction, cancellationToken);
        }
    }
}
