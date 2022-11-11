using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts.Constants;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.QueryHandlers.MultiCall;
using Nethereum.Contracts.Services;
using Nethereum.Contracts.Standards.ERC721.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.Standards.ERC721
{


    public partial class ERC721ContractService
    {
        private readonly IEthApiContractService _ethApiContractService;
        public string ContractAddress { get; }

        public ContractHandler ContractHandler { get; }

        public ERC721ContractService(IEthApiContractService ethApiContractService, string contractAddress)
        {
            _ethApiContractService = ethApiContractService;
            ContractAddress = contractAddress;
#if !DOTNET35
            ContractHandler = ethApiContractService.GetContractHandler(contractAddress);
#endif
        }

#if !DOTNET35
        public Task<byte[]> DomainSeparatorQueryAsync(DomainSeparatorFunction domainSeparatorFunction,
            BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DomainSeparatorFunction, byte[]>(domainSeparatorFunction, blockParameter);
        }


        public Task<byte[]> DomainSeparatorQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DomainSeparatorFunction, byte[]>(null, blockParameter);
        }

        public Task<string> ApproveRequestAsync(ApproveFunction approveFunction)
        {
            return ContractHandler.SendRequestAsync(approveFunction);
        }

        public Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(ApproveFunction approveFunction,
            CancellationToken cancellationToken = default)
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

        public Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(string to, BigInteger tokenId,
            CancellationToken cancellationToken = default)
        {
            var approveFunction = new ApproveFunction();
            approveFunction.To = to;
            approveFunction.TokenId = tokenId;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(approveFunction, cancellationToken);
        }

        public Task<BigInteger> BalanceOfQueryAsync(BalanceOfFunction balanceOfFunction,
            BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }


        public Task<BigInteger> BalanceOfQueryAsync(string owner, BlockParameter blockParameter = null)
        {
            var balanceOfFunction = new BalanceOfFunction();
            balanceOfFunction.Owner = owner;

            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }

        public Task<string> BurnRequestAsync(BurnFunction burnFunction)
        {
            return ContractHandler.SendRequestAsync(burnFunction);
        }

        public Task<TransactionReceipt> BurnRequestAndWaitForReceiptAsync(BurnFunction burnFunction,
            CancellationToken cancellationToken = default)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(burnFunction, cancellationToken);
        }

        public Task<string> BurnRequestAsync(BigInteger tokenId)
        {
            var burnFunction = new BurnFunction();
            burnFunction.TokenId = tokenId;

            return ContractHandler.SendRequestAsync(burnFunction);
        }

        public Task<TransactionReceipt> BurnRequestAndWaitForReceiptAsync(BigInteger tokenId,
            CancellationToken cancellationToken = default)
        {
            var burnFunction = new BurnFunction();
            burnFunction.TokenId = tokenId;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(burnFunction, cancellationToken);
        }

        public Task<string> DelegateRequestAsync(DelegateFunction @delegateFunction)
        {
            return ContractHandler.SendRequestAsync(@delegateFunction);
        }

        public Task<TransactionReceipt> DelegateRequestAndWaitForReceiptAsync(DelegateFunction @delegateFunction,
            CancellationToken cancellationToken = default)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(@delegateFunction, cancellationToken);
        }

        public Task<string> DelegateRequestAsync(string delegatee)
        {
            var @delegateFunction = new DelegateFunction();
            @delegateFunction.Delegatee = delegatee;

            return ContractHandler.SendRequestAsync(@delegateFunction);
        }

        public Task<TransactionReceipt> DelegateRequestAndWaitForReceiptAsync(string delegatee,
            CancellationToken cancellationToken = default)
        {
            var @delegateFunction = new DelegateFunction();
            @delegateFunction.Delegatee = delegatee;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(@delegateFunction, cancellationToken);
        }

        public Task<string> DelegateBySigRequestAsync(DelegateBySigFunction delegateBySigFunction)
        {
            return ContractHandler.SendRequestAsync(delegateBySigFunction);
        }

        public Task<TransactionReceipt> DelegateBySigRequestAndWaitForReceiptAsync(
            DelegateBySigFunction delegateBySigFunction, CancellationToken cancellationToken = default)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(delegateBySigFunction, cancellationToken);
        }

        public Task<string> DelegateBySigRequestAsync(string delegatee, BigInteger nonce, BigInteger expiry, byte v,
            byte[] r, byte[] s)
        {
            var delegateBySigFunction = new DelegateBySigFunction();
            delegateBySigFunction.Delegatee = delegatee;
            delegateBySigFunction.Nonce = nonce;
            delegateBySigFunction.Expiry = expiry;
            delegateBySigFunction.V = v;
            delegateBySigFunction.R = r;
            delegateBySigFunction.S = s;

            return ContractHandler.SendRequestAsync(delegateBySigFunction);
        }

        public Task<TransactionReceipt> DelegateBySigRequestAndWaitForReceiptAsync(string delegatee, BigInteger nonce,
            BigInteger expiry, byte v, byte[] r, byte[] s, CancellationToken cancellationToken = default)
        {
            var delegateBySigFunction = new DelegateBySigFunction();
            delegateBySigFunction.Delegatee = delegatee;
            delegateBySigFunction.Nonce = nonce;
            delegateBySigFunction.Expiry = expiry;
            delegateBySigFunction.V = v;
            delegateBySigFunction.R = r;
            delegateBySigFunction.S = s;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(delegateBySigFunction, cancellationToken);
        }

        public Task<string> DelegatesQueryAsync(DelegatesFunction delegatesFunction,
            BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DelegatesFunction, string>(delegatesFunction, blockParameter);
        }


        public Task<string> DelegatesQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var delegatesFunction = new DelegatesFunction();
            delegatesFunction.Account = account;

            return ContractHandler.QueryAsync<DelegatesFunction, string>(delegatesFunction, blockParameter);
        }

        public Task<string> GetApprovedQueryAsync(GetApprovedFunction getApprovedFunction,
            BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetApprovedFunction, string>(getApprovedFunction, blockParameter);
        }


        public Task<string> GetApprovedQueryAsync(BigInteger tokenId, BlockParameter blockParameter = null)
        {
            var getApprovedFunction = new GetApprovedFunction();
            getApprovedFunction.TokenId = tokenId;

            return ContractHandler.QueryAsync<GetApprovedFunction, string>(getApprovedFunction, blockParameter);
        }

        public Task<BigInteger> GetPastTotalSupplyQueryAsync(GetPastTotalSupplyFunction getPastTotalSupplyFunction,
            BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetPastTotalSupplyFunction, BigInteger>(getPastTotalSupplyFunction,
                blockParameter);
        }


        public Task<BigInteger> GetPastTotalSupplyQueryAsync(BigInteger blockNumber,
            BlockParameter blockParameter = null)
        {
            var getPastTotalSupplyFunction = new GetPastTotalSupplyFunction();
            getPastTotalSupplyFunction.BlockNumber = blockNumber;

            return ContractHandler.QueryAsync<GetPastTotalSupplyFunction, BigInteger>(getPastTotalSupplyFunction,
                blockParameter);
        }

        public Task<BigInteger> GetPastVotesQueryAsync(GetPastVotesFunction getPastVotesFunction,
            BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetPastVotesFunction, BigInteger>(getPastVotesFunction, blockParameter);
        }


        public Task<BigInteger> GetPastVotesQueryAsync(string account, BigInteger blockNumber,
            BlockParameter blockParameter = null)
        {
            var getPastVotesFunction = new GetPastVotesFunction();
            getPastVotesFunction.Account = account;
            getPastVotesFunction.BlockNumber = blockNumber;

            return ContractHandler.QueryAsync<GetPastVotesFunction, BigInteger>(getPastVotesFunction, blockParameter);
        }

        public Task<BigInteger> GetVotesQueryAsync(GetVotesFunction getVotesFunction,
            BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetVotesFunction, BigInteger>(getVotesFunction, blockParameter);
        }


        public Task<BigInteger> GetVotesQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var getVotesFunction = new GetVotesFunction();
            getVotesFunction.Account = account;

            return ContractHandler.QueryAsync<GetVotesFunction, BigInteger>(getVotesFunction, blockParameter);
        }

        public Task<bool> IsApprovedForAllQueryAsync(IsApprovedForAllFunction isApprovedForAllFunction,
            BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsApprovedForAllFunction, bool>(isApprovedForAllFunction, blockParameter);
        }


        public Task<bool> IsApprovedForAllQueryAsync(string owner, string @operator,
            BlockParameter blockParameter = null)
        {
            var isApprovedForAllFunction = new IsApprovedForAllFunction();
            isApprovedForAllFunction.Owner = owner;
            isApprovedForAllFunction.Operator = @operator;

            return ContractHandler.QueryAsync<IsApprovedForAllFunction, bool>(isApprovedForAllFunction, blockParameter);
        }

        public Task<string> NameQueryAsync(NameFunction nameFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(nameFunction, blockParameter);
        }


        public Task<string> NameQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> NoncesQueryAsync(NoncesFunction noncesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NoncesFunction, BigInteger>(noncesFunction, blockParameter);
        }


        public Task<BigInteger> NoncesQueryAsync(string owner, BlockParameter blockParameter = null)
        {
            var noncesFunction = new NoncesFunction();
            noncesFunction.Owner = owner;

            return ContractHandler.QueryAsync<NoncesFunction, BigInteger>(noncesFunction, blockParameter);
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

        public Task<string> PauseRequestAsync(PauseFunction pauseFunction)
        {
            return ContractHandler.SendRequestAsync(pauseFunction);
        }

        public Task<string> PauseRequestAsync()
        {
            return ContractHandler.SendRequestAsync<PauseFunction>();
        }

        public Task<TransactionReceipt> PauseRequestAndWaitForReceiptAsync(PauseFunction pauseFunction,
            CancellationToken cancellationToken = default)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(pauseFunction, cancellationToken);
        }

        public Task<TransactionReceipt> PauseRequestAndWaitForReceiptAsync(
            CancellationToken cancellationToken = default)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync<PauseFunction>(null, cancellationToken);
        }

        public Task<bool> PausedQueryAsync(PausedFunction pausedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PausedFunction, bool>(pausedFunction, blockParameter);
        }


        public Task<bool> PausedQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PausedFunction, bool>(null, blockParameter);
        }

        public Task<string> RenounceOwnershipRequestAsync(RenounceOwnershipFunction renounceOwnershipFunction)
        {
            return ContractHandler.SendRequestAsync(renounceOwnershipFunction);
        }

        public Task<string> RenounceOwnershipRequestAsync()
        {
            return ContractHandler.SendRequestAsync<RenounceOwnershipFunction>();
        }

        public Task<TransactionReceipt> RenounceOwnershipRequestAndWaitForReceiptAsync(
            RenounceOwnershipFunction renounceOwnershipFunction, CancellationToken cancellationToken = default)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(renounceOwnershipFunction, cancellationToken);
        }

        public Task<TransactionReceipt> RenounceOwnershipRequestAndWaitForReceiptAsync(
            CancellationToken cancellationToken = default)
        {
            return ContractHandler
                .SendRequestAndWaitForReceiptAsync<RenounceOwnershipFunction>(null, cancellationToken);
        }

        public Task<string> SafeMintRequestAsync(SafeMintFunction safeMintFunction)
        {
            return ContractHandler.SendRequestAsync(safeMintFunction);
        }

        public Task<TransactionReceipt> SafeMintRequestAndWaitForReceiptAsync(SafeMintFunction safeMintFunction,
            CancellationToken cancellationToken = default)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(safeMintFunction, cancellationToken);
        }

        public Task<string> SafeMintRequestAsync(string to, string uri)
        {
            var safeMintFunction = new SafeMintFunction();
            safeMintFunction.To = to;
            safeMintFunction.Uri = uri;

            return ContractHandler.SendRequestAsync(safeMintFunction);
        }

        public Task<TransactionReceipt> SafeMintRequestAndWaitForReceiptAsync(string to, string uri,
            CancellationToken cancellationToken = default)
        {
            var safeMintFunction = new SafeMintFunction();
            safeMintFunction.To = to;
            safeMintFunction.Uri = uri;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(safeMintFunction, cancellationToken);
        }

        public Task<string> SafeTransferFromRequestAsync(SafeTransferFromFunction safeTransferFromFunction)
        {
            return ContractHandler.SendRequestAsync(safeTransferFromFunction);
        }

        public Task<TransactionReceipt> SafeTransferFromRequestAndWaitForReceiptAsync(
            SafeTransferFromFunction safeTransferFromFunction, CancellationToken cancellationToken = default)
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

        public Task<TransactionReceipt> SafeTransferFromRequestAndWaitForReceiptAsync(string from, string to,
            BigInteger tokenId, CancellationToken cancellationToken = default)
        {
            var safeTransferFromFunction = new SafeTransferFromFunction();
            safeTransferFromFunction.From = from;
            safeTransferFromFunction.To = to;
            safeTransferFromFunction.TokenId = tokenId;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(safeTransferFromFunction, cancellationToken);
        }

        public Task<string> SafeTransferFromRequestAsync(SafeTransferFrom1Function safeTransferFrom1Function)
        {
            return ContractHandler.SendRequestAsync(safeTransferFrom1Function);
        }

        public Task<TransactionReceipt> SafeTransferFromRequestAndWaitForReceiptAsync(
            SafeTransferFrom1Function safeTransferFrom1Function, CancellationToken cancellationToken = default)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(safeTransferFrom1Function, cancellationToken);
        }

        public Task<string> SafeTransferFromRequestAsync(string from, string to, BigInteger tokenId, byte[] data)
        {
            var safeTransferFrom1Function = new SafeTransferFrom1Function();
            safeTransferFrom1Function.From = from;
            safeTransferFrom1Function.To = to;
            safeTransferFrom1Function.TokenId = tokenId;
            safeTransferFrom1Function.Data = data;

            return ContractHandler.SendRequestAsync(safeTransferFrom1Function);
        }

        public Task<TransactionReceipt> SafeTransferFromRequestAndWaitForReceiptAsync(string from, string to,
            BigInteger tokenId, byte[] data, CancellationToken cancellationToken = default)
        {
            var safeTransferFrom1Function = new SafeTransferFrom1Function();
            safeTransferFrom1Function.From = from;
            safeTransferFrom1Function.To = to;
            safeTransferFrom1Function.TokenId = tokenId;
            safeTransferFrom1Function.Data = data;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(safeTransferFrom1Function, cancellationToken);
        }

        public Task<string> SetApprovalForAllRequestAsync(SetApprovalForAllFunction setApprovalForAllFunction)
        {
            return ContractHandler.SendRequestAsync(setApprovalForAllFunction);
        }

        public Task<TransactionReceipt> SetApprovalForAllRequestAndWaitForReceiptAsync(
            SetApprovalForAllFunction setApprovalForAllFunction, CancellationToken cancellationToken = default)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(setApprovalForAllFunction, cancellationToken);
        }

        public Task<string> SetApprovalForAllRequestAsync(string @operator, bool approved)
        {
            var setApprovalForAllFunction = new SetApprovalForAllFunction();
            setApprovalForAllFunction.Operator = @operator;
            setApprovalForAllFunction.Approved = approved;

            return ContractHandler.SendRequestAsync(setApprovalForAllFunction);
        }

        public Task<TransactionReceipt> SetApprovalForAllRequestAndWaitForReceiptAsync(string @operator, bool approved,
            CancellationToken cancellationToken = default)
        {
            var setApprovalForAllFunction = new SetApprovalForAllFunction();
            setApprovalForAllFunction.Operator = @operator;
            setApprovalForAllFunction.Approved = approved;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(setApprovalForAllFunction, cancellationToken);
        }

        public Task<bool> SupportsInterfaceQueryAsync(SupportsInterfaceFunction supportsInterfaceFunction,
            BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction,
                blockParameter);
        }


        public Task<bool> SupportsInterfaceQueryAsync(byte[] interfaceId, BlockParameter blockParameter = null)
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
            supportsInterfaceFunction.InterfaceId = interfaceId;

            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction,
                blockParameter);
        }

        public Task<string> SymbolQueryAsync(SymbolFunction symbolFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SymbolFunction, string>(symbolFunction, blockParameter);
        }


        public Task<string> SymbolQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SymbolFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> TokenByIndexQueryAsync(TokenByIndexFunction tokenByIndexFunction,
            BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TokenByIndexFunction, BigInteger>(tokenByIndexFunction, blockParameter);
        }


        public Task<BigInteger> TokenByIndexQueryAsync(BigInteger index, BlockParameter blockParameter = null)
        {
            var tokenByIndexFunction = new TokenByIndexFunction();
            tokenByIndexFunction.Index = index;

            return ContractHandler.QueryAsync<TokenByIndexFunction, BigInteger>(tokenByIndexFunction, blockParameter);
        }

        public Task<BigInteger> TokenOfOwnerByIndexQueryAsync(TokenOfOwnerByIndexFunction tokenOfOwnerByIndexFunction,
            BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TokenOfOwnerByIndexFunction, BigInteger>(tokenOfOwnerByIndexFunction,
                blockParameter);
        }


        public Task<BigInteger> TokenOfOwnerByIndexQueryAsync(string owner, BigInteger index,
            BlockParameter blockParameter = null)
        {
            var tokenOfOwnerByIndexFunction = new TokenOfOwnerByIndexFunction();
            tokenOfOwnerByIndexFunction.Owner = owner;
            tokenOfOwnerByIndexFunction.Index = index;

            return ContractHandler.QueryAsync<TokenOfOwnerByIndexFunction, BigInteger>(tokenOfOwnerByIndexFunction,
                blockParameter);
        }

        public Task<string> TokenURIQueryAsync(TokenURIFunction tokenURIFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TokenURIFunction, string>(tokenURIFunction, blockParameter);
        }


        public Task<string> TokenURIQueryAsync(BigInteger tokenId, BlockParameter blockParameter = null)
        {
            var tokenURIFunction = new TokenURIFunction();
            tokenURIFunction.TokenId = tokenId;

            return ContractHandler.QueryAsync<TokenURIFunction, string>(tokenURIFunction, blockParameter);
        }

        public Task<BigInteger> TotalSupplyQueryAsync(TotalSupplyFunction totalSupplyFunction,
            BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>(totalSupplyFunction, blockParameter);
        }


        public Task<BigInteger> TotalSupplyQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> TransferFromRequestAsync(TransferFromFunction transferFromFunction)
        {
            return ContractHandler.SendRequestAsync(transferFromFunction);
        }

        public Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(
            TransferFromFunction transferFromFunction, CancellationToken cancellationToken = default)
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

        public Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(string from, string to,
            BigInteger tokenId, CancellationToken cancellationToken = default)
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

        public Task<TransactionReceipt> TransferOwnershipRequestAndWaitForReceiptAsync(
            TransferOwnershipFunction transferOwnershipFunction, CancellationToken cancellationToken = default)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(transferOwnershipFunction, cancellationToken);
        }

        public Task<string> TransferOwnershipRequestAsync(string newOwner)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
            transferOwnershipFunction.NewOwner = newOwner;

            return ContractHandler.SendRequestAsync(transferOwnershipFunction);
        }

        public Task<TransactionReceipt> TransferOwnershipRequestAndWaitForReceiptAsync(string newOwner,
            CancellationToken cancellationToken = default)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
            transferOwnershipFunction.NewOwner = newOwner;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(transferOwnershipFunction, cancellationToken);
        }

        public Task<string> UnpauseRequestAsync(UnpauseFunction unpauseFunction)
        {
            return ContractHandler.SendRequestAsync(unpauseFunction);
        }

        public Task<string> UnpauseRequestAsync()
        {
            return ContractHandler.SendRequestAsync<UnpauseFunction>();
        }

        public Task<TransactionReceipt> UnpauseRequestAndWaitForReceiptAsync(UnpauseFunction unpauseFunction,
            CancellationToken cancellationToken = default)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(unpauseFunction, cancellationToken);
        }

        public Task<TransactionReceipt> UnpauseRequestAndWaitForReceiptAsync(
            CancellationToken cancellationToken = default)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync<UnpauseFunction>(null, cancellationToken);
        }
#endif
    }
}