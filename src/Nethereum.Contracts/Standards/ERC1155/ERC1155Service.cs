using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.Services;
using Nethereum.Contracts.Standards.ERC1155.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.Standards.ERC1155
{
    public class ERC1155Service
    {
        public string ContractAddress { get; }
        public ContractHandler ContractHandler { get; }

        public ERC1155Service(IEthApiContractService ethApiContractService, string contractAddress)
        {
            ContractAddress = contractAddress;
#if !DOTNET35
            ContractHandler = ethApiContractService.GetContractHandler(contractAddress);
#endif
        }

#if !DOTNET35
        public Task<BigInteger> BalanceOfQueryAsync(BalanceOfFunction balanceOfFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }


        public Task<BigInteger> BalanceOfQueryAsync(string account, BigInteger id, BlockParameter blockParameter = null)
        {
            var balanceOfFunction = new BalanceOfFunction();
            balanceOfFunction.Account = account;
            balanceOfFunction.Id = id;

            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }

        public Task<List<BigInteger>> BalanceOfBatchQueryAsync(BalanceOfBatchFunction balanceOfBatchFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BalanceOfBatchFunction, List<BigInteger>>(balanceOfBatchFunction, blockParameter);
        }


        public Task<List<BigInteger>> BalanceOfBatchQueryAsync(List<string> accounts, List<BigInteger> ids, BlockParameter blockParameter = null)
        {
            var balanceOfBatchFunction = new BalanceOfBatchFunction();
            balanceOfBatchFunction.Accounts = accounts;
            balanceOfBatchFunction.Ids = ids;

            return ContractHandler.QueryAsync<BalanceOfBatchFunction, List<BigInteger>>(balanceOfBatchFunction, blockParameter);
        }

        public Task<string> BurnRequestAsync(BurnFunction burnFunction)
        {
            return ContractHandler.SendRequestAsync(burnFunction);
        }

        public Task<TransactionReceipt> BurnRequestAndWaitForReceiptAsync(BurnFunction burnFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(burnFunction, cancellationToken);
        }

        public Task<string> BurnRequestAsync(string account, BigInteger id, BigInteger value)
        {
            var burnFunction = new BurnFunction();
            burnFunction.Account = account;
            burnFunction.Id = id;
            burnFunction.Value = value;

            return ContractHandler.SendRequestAsync(burnFunction);
        }

        public Task<TransactionReceipt> BurnRequestAndWaitForReceiptAsync(string account, BigInteger id, BigInteger value, CancellationTokenSource cancellationToken = null)
        {
            var burnFunction = new BurnFunction();
            burnFunction.Account = account;
            burnFunction.Id = id;
            burnFunction.Value = value;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(burnFunction, cancellationToken);
        }

        public Task<string> BurnBatchRequestAsync(BurnBatchFunction burnBatchFunction)
        {
            return ContractHandler.SendRequestAsync(burnBatchFunction);
        }

        public Task<TransactionReceipt> BurnBatchRequestAndWaitForReceiptAsync(BurnBatchFunction burnBatchFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(burnBatchFunction, cancellationToken);
        }

        public Task<string> BurnBatchRequestAsync(string account, List<BigInteger> ids, List<BigInteger> values)
        {
            var burnBatchFunction = new BurnBatchFunction();
            burnBatchFunction.Account = account;
            burnBatchFunction.Ids = ids;
            burnBatchFunction.Values = values;

            return ContractHandler.SendRequestAsync(burnBatchFunction);
        }

        public Task<TransactionReceipt> BurnBatchRequestAndWaitForReceiptAsync(string account, List<BigInteger> ids, List<BigInteger> values, CancellationTokenSource cancellationToken = null)
        {
            var burnBatchFunction = new BurnBatchFunction();
            burnBatchFunction.Account = account;
            burnBatchFunction.Ids = ids;
            burnBatchFunction.Values = values;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(burnBatchFunction, cancellationToken);
        }

        public Task<bool> ExistsQueryAsync(ExistsFunction existsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ExistsFunction, bool>(existsFunction, blockParameter);
        }


        public Task<bool> ExistsQueryAsync(BigInteger id, BlockParameter blockParameter = null)
        {
            var existsFunction = new ExistsFunction();
            existsFunction.Id = id;

            return ContractHandler.QueryAsync<ExistsFunction, bool>(existsFunction, blockParameter);
        }

        public Task<bool> IsApprovedForAllQueryAsync(IsApprovedForAllFunction isApprovedForAllFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsApprovedForAllFunction, bool>(isApprovedForAllFunction, blockParameter);
        }


        public Task<bool> IsApprovedForAllQueryAsync(string account, string @operator, BlockParameter blockParameter = null)
        {
            var isApprovedForAllFunction = new IsApprovedForAllFunction();
            isApprovedForAllFunction.Account = account;
            isApprovedForAllFunction.Operator = @operator;

            return ContractHandler.QueryAsync<IsApprovedForAllFunction, bool>(isApprovedForAllFunction, blockParameter);
        }

        public Task<string> MintRequestAsync(MintFunction mintFunction)
        {
            return ContractHandler.SendRequestAsync(mintFunction);
        }

        public Task<TransactionReceipt> MintRequestAndWaitForReceiptAsync(MintFunction mintFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(mintFunction, cancellationToken);
        }

        public Task<string> MintRequestAsync(string account, BigInteger id, BigInteger amount, byte[] data)
        {
            var mintFunction = new MintFunction();
            mintFunction.Account = account;
            mintFunction.Id = id;
            mintFunction.Amount = amount;
            mintFunction.Data = data;

            return ContractHandler.SendRequestAsync(mintFunction);
        }

        public Task<TransactionReceipt> MintRequestAndWaitForReceiptAsync(string account, BigInteger id, BigInteger amount, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var mintFunction = new MintFunction();
            mintFunction.Account = account;
            mintFunction.Id = id;
            mintFunction.Amount = amount;
            mintFunction.Data = data;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(mintFunction, cancellationToken);
        }
        
        public Task<string> MintBatchRequestAsync(MintBatchFunction mintBatchFunction)
        {
            return ContractHandler.SendRequestAsync(mintBatchFunction);
        }

        public Task<TransactionReceipt> MintBatchRequestAndWaitForReceiptAsync(MintBatchFunction mintBatchFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(mintBatchFunction, cancellationToken);
        }

        public Task<string> MintBatchRequestAsync(string to, List<BigInteger> ids, List<BigInteger> amounts, byte[] data)
        {
            var mintBatchFunction = new MintBatchFunction();
            mintBatchFunction.To = to;
            mintBatchFunction.Ids = ids;
            mintBatchFunction.Amounts = amounts;
            mintBatchFunction.Data = data;

            return ContractHandler.SendRequestAsync(mintBatchFunction);
        }

        public Task<TransactionReceipt> MintBatchRequestAndWaitForReceiptAsync(string to, List<BigInteger> ids, List<BigInteger> amounts, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var mintBatchFunction = new MintBatchFunction();
            mintBatchFunction.To = to;
            mintBatchFunction.Ids = ids;
            mintBatchFunction.Amounts = amounts;
            mintBatchFunction.Data = data;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(mintBatchFunction, cancellationToken);
        }

        public Task<string> OwnerQueryAsync(OwnerFunction ownerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }


        public Task<string> OwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(null, blockParameter);
        }

        public Task<string> PauseRequestAsync(PauseFunction pauseFunction)
        {
            return ContractHandler.SendRequestAsync(pauseFunction);
        }

        public Task<string> PauseRequestAsync()
        {
            return ContractHandler.SendRequestAsync<PauseFunction>();
        }

        public Task<TransactionReceipt> PauseRequestAndWaitForReceiptAsync(PauseFunction pauseFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(pauseFunction, cancellationToken);
        }

        public Task<TransactionReceipt> PauseRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
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

        public Task<TransactionReceipt> RenounceOwnershipRequestAndWaitForReceiptAsync(RenounceOwnershipFunction renounceOwnershipFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(renounceOwnershipFunction, cancellationToken);
        }

        public Task<TransactionReceipt> RenounceOwnershipRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync<RenounceOwnershipFunction>(null, cancellationToken);
        }

        public Task<string> SafeBatchTransferFromRequestAsync(SafeBatchTransferFromFunction safeBatchTransferFromFunction)
        {
            return ContractHandler.SendRequestAsync(safeBatchTransferFromFunction);
        }

        public Task<TransactionReceipt> SafeBatchTransferFromRequestAndWaitForReceiptAsync(SafeBatchTransferFromFunction safeBatchTransferFromFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(safeBatchTransferFromFunction, cancellationToken);
        }

        public Task<string> SafeBatchTransferFromRequestAsync(string from, string to, List<BigInteger> ids, List<BigInteger> amounts, byte[] data)
        {
            var safeBatchTransferFromFunction = new SafeBatchTransferFromFunction();
            safeBatchTransferFromFunction.From = from;
            safeBatchTransferFromFunction.To = to;
            safeBatchTransferFromFunction.Ids = ids;
            safeBatchTransferFromFunction.Amounts = amounts;
            safeBatchTransferFromFunction.Data = data;

            return ContractHandler.SendRequestAsync(safeBatchTransferFromFunction);
        }

        public Task<TransactionReceipt> SafeBatchTransferFromRequestAndWaitForReceiptAsync(string from, string to, List<BigInteger> ids, List<BigInteger> amounts, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var safeBatchTransferFromFunction = new SafeBatchTransferFromFunction();
            safeBatchTransferFromFunction.From = from;
            safeBatchTransferFromFunction.To = to;
            safeBatchTransferFromFunction.Ids = ids;
            safeBatchTransferFromFunction.Amounts = amounts;
            safeBatchTransferFromFunction.Data = data;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(safeBatchTransferFromFunction, cancellationToken);
        }

        public Task<string> SafeTransferFromRequestAsync(SafeTransferFromFunction safeTransferFromFunction)
        {
            return ContractHandler.SendRequestAsync(safeTransferFromFunction);
        }

        public Task<TransactionReceipt> SafeTransferFromRequestAndWaitForReceiptAsync(SafeTransferFromFunction safeTransferFromFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(safeTransferFromFunction, cancellationToken);
        }

        public Task<string> SafeTransferFromRequestAsync(string from, string to, BigInteger id, BigInteger amount, byte[] data)
        {
            var safeTransferFromFunction = new SafeTransferFromFunction();
            safeTransferFromFunction.From = from;
            safeTransferFromFunction.To = to;
            safeTransferFromFunction.Id = id;
            safeTransferFromFunction.Amount = amount;
            safeTransferFromFunction.Data = data;

            return ContractHandler.SendRequestAsync(safeTransferFromFunction);
        }

        public Task<TransactionReceipt> SafeTransferFromRequestAndWaitForReceiptAsync(string from, string to, BigInteger id, BigInteger amount, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var safeTransferFromFunction = new SafeTransferFromFunction();
            safeTransferFromFunction.From = from;
            safeTransferFromFunction.To = to;
            safeTransferFromFunction.Id = id;
            safeTransferFromFunction.Amount = amount;
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

        public Task<string> SetApprovalForAllRequestAsync(string @operator, bool approved)
        {
            var setApprovalForAllFunction = new SetApprovalForAllFunction();
            setApprovalForAllFunction.Operator = @operator;
            setApprovalForAllFunction.Approved = approved;

            return ContractHandler.SendRequestAsync(setApprovalForAllFunction);
        }

        public Task<TransactionReceipt> SetApprovalForAllRequestAndWaitForReceiptAsync(string @operator, bool approved, CancellationTokenSource cancellationToken = null)
        {
            var setApprovalForAllFunction = new SetApprovalForAllFunction();
            setApprovalForAllFunction.Operator = @operator;
            setApprovalForAllFunction.Approved = approved;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(setApprovalForAllFunction, cancellationToken);
        }

        public Task<string> SetTokenUriRequestAsync(SetTokenUriFunction setTokenUriFunction)
        {
            return ContractHandler.SendRequestAsync(setTokenUriFunction);
        }

        public Task<TransactionReceipt> SetTokenUriRequestAndWaitForReceiptAsync(SetTokenUriFunction setTokenUriFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(setTokenUriFunction, cancellationToken);
        }

        public Task<string> SetTokenUriRequestAsync(BigInteger tokenId, string tokenURI)
        {
            var setTokenUriFunction = new SetTokenUriFunction();
            setTokenUriFunction.TokenId = tokenId;
            setTokenUriFunction.TokenURI = tokenURI;

            return ContractHandler.SendRequestAsync(setTokenUriFunction);
        }

        public Task<TransactionReceipt> SetTokenUriRequestAndWaitForReceiptAsync(BigInteger tokenId, string tokenURI, CancellationTokenSource cancellationToken = null)
        {
            var setTokenUriFunction = new SetTokenUriFunction();
            setTokenUriFunction.TokenId = tokenId;
            setTokenUriFunction.TokenURI = tokenURI;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(setTokenUriFunction, cancellationToken);
        }

        public Task<string> SetURIRequestAsync(SetURIFunction setURIFunction)
        {
            return ContractHandler.SendRequestAsync(setURIFunction);
        }

        public Task<TransactionReceipt> SetURIRequestAndWaitForReceiptAsync(SetURIFunction setURIFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(setURIFunction, cancellationToken);
        }

        public Task<string> SetURIRequestAsync(string newuri)
        {
            var setURIFunction = new SetURIFunction();
            setURIFunction.Newuri = newuri;

            return ContractHandler.SendRequestAsync(setURIFunction);
        }

        public Task<TransactionReceipt> SetURIRequestAndWaitForReceiptAsync(string newuri, CancellationTokenSource cancellationToken = null)
        {
            var setURIFunction = new SetURIFunction();
            setURIFunction.Newuri = newuri;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(setURIFunction, cancellationToken);
        }

        public Task<bool> SupportsInterfaceQueryAsync(SupportsInterfaceFunction supportsInterfaceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }


        public Task<bool> SupportsInterfaceQueryAsync(byte[] interfaceId, BlockParameter blockParameter = null)
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
            supportsInterfaceFunction.InterfaceId = interfaceId;

            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        public Task<BigInteger> TotalSupplyQueryAsync(TotalSupplyFunction totalSupplyFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>(totalSupplyFunction, blockParameter);
        }


        public Task<BigInteger> TotalSupplyQueryAsync(BigInteger id, BlockParameter blockParameter = null)
        {
            var totalSupplyFunction = new TotalSupplyFunction();
            totalSupplyFunction.Id = id;

            return ContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>(totalSupplyFunction, blockParameter);
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

        public Task<string> UnpauseRequestAsync(UnpauseFunction unpauseFunction)
        {
            return ContractHandler.SendRequestAsync(unpauseFunction);
        }

        public Task<string> UnpauseRequestAsync()
        {
            return ContractHandler.SendRequestAsync<UnpauseFunction>();
        }

        public Task<TransactionReceipt> UnpauseRequestAndWaitForReceiptAsync(UnpauseFunction unpauseFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(unpauseFunction, cancellationToken);
        }

        public Task<TransactionReceipt> UnpauseRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync<UnpauseFunction>(null, cancellationToken);
        }

        public Task<string> UriQueryAsync(UriFunction uriFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<UriFunction, string>(uriFunction, blockParameter);
        }


        public Task<string> UriQueryAsync(BigInteger tokenId, BlockParameter blockParameter = null)
        {
            var uriFunction = new UriFunction();
            uriFunction.TokenId = tokenId;

            return ContractHandler.QueryAsync<UriFunction, string>(uriFunction, blockParameter);
        }

#endif
    }
}
