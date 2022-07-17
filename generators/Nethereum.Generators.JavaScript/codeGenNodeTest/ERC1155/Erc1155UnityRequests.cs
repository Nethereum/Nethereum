using System;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Unity.Contracts;
using Newtonsoft.Json;
using Nethereum.Unity.Contracts.Standards.ERC1155.ERC1155.ContractDefinition;

namespace Nethereum.Unity.Contracts.Standards.ERC1155.ERC1155
{
    public partial class BalanceOfQueryRequest : ContractFunctionQueryRequest<BalanceOfFunction, BalanceOfOutputDTO>
    {

        public BalanceOfQueryRequest(IContractQueryUnityRequestFactory contractQueryUnityRequestFactory, string contractAddress) : base(contractQueryUnityRequestFactory, contractAddress)
        {
        }

        public BalanceOfQueryRequest(string url, string contractAddress, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, contractAddress, defaultAccount, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator Query(string account, BigInteger id, BlockParameter blockParameter = null)
        {
            var balanceOfFunction = new BalanceOfFunction();
                balanceOfFunction.Account = account;
                balanceOfFunction.Id = id;
            yield return Query(balanceOfFunction, blockParameter);
        }

    }

    public partial class BalanceOfBatchQueryRequest : ContractFunctionQueryRequest<BalanceOfBatchFunction, BalanceOfBatchOutputDTO>
    {

        public BalanceOfBatchQueryRequest(IContractQueryUnityRequestFactory contractQueryUnityRequestFactory, string contractAddress) : base(contractQueryUnityRequestFactory, contractAddress)
        {
        }

        public BalanceOfBatchQueryRequest(string url, string contractAddress, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, contractAddress, defaultAccount, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator Query(List<string> accounts, List<BigInteger> ids, BlockParameter blockParameter = null)
        {
            var balanceOfBatchFunction = new BalanceOfBatchFunction();
                balanceOfBatchFunction.Accounts = accounts;
                balanceOfBatchFunction.Ids = ids;
            yield return Query(balanceOfBatchFunction, blockParameter);
        }

    }

    public partial class BurnTransactionRequest : ContractFunctionTransactionRequest<BurnFunction>
    {

        public BurnTransactionRequest(IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, string contractAddress) : base(contractTransactionUnityRequestFactory, contractAddress)
        {
        }

        public BurnTransactionRequest(string url, BigInteger chainId, string privateKey, string contractAddress, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, chainId, privateKey, contractAddress, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator SignAndSendTransaction(string account, BigInteger id, BigInteger value, BlockParameter blockParameter = null)
        {
            var burnFunction = new BurnFunction();
                burnFunction.Account = account;
                burnFunction.Id = id;
                burnFunction.Value = value;
            yield return SignAndSendTransaction(burnFunction);
        }

    }

    public partial class BurnBatchTransactionRequest : ContractFunctionTransactionRequest<BurnBatchFunction>
    {

        public BurnBatchTransactionRequest(IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, string contractAddress) : base(contractTransactionUnityRequestFactory, contractAddress)
        {
        }

        public BurnBatchTransactionRequest(string url, BigInteger chainId, string privateKey, string contractAddress, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, chainId, privateKey, contractAddress, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator SignAndSendTransaction(string account, List<BigInteger> ids, List<BigInteger> values, BlockParameter blockParameter = null)
        {
            var burnBatchFunction = new BurnBatchFunction();
                burnBatchFunction.Account = account;
                burnBatchFunction.Ids = ids;
                burnBatchFunction.Values = values;
            yield return SignAndSendTransaction(burnBatchFunction);
        }

    }

    public partial class ExistsQueryRequest : ContractFunctionQueryRequest<ExistsFunction, ExistsOutputDTO>
    {

        public ExistsQueryRequest(IContractQueryUnityRequestFactory contractQueryUnityRequestFactory, string contractAddress) : base(contractQueryUnityRequestFactory, contractAddress)
        {
        }

        public ExistsQueryRequest(string url, string contractAddress, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, contractAddress, defaultAccount, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator Query(BigInteger id, BlockParameter blockParameter = null)
        {
            var existsFunction = new ExistsFunction();
                existsFunction.Id = id;
            yield return Query(existsFunction, blockParameter);
        }

    }

    public partial class IsApprovedForAllQueryRequest : ContractFunctionQueryRequest<IsApprovedForAllFunction, IsApprovedForAllOutputDTO>
    {

        public IsApprovedForAllQueryRequest(IContractQueryUnityRequestFactory contractQueryUnityRequestFactory, string contractAddress) : base(contractQueryUnityRequestFactory, contractAddress)
        {
        }

        public IsApprovedForAllQueryRequest(string url, string contractAddress, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, contractAddress, defaultAccount, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator Query(string account, string @operator, BlockParameter blockParameter = null)
        {
            var isApprovedForAllFunction = new IsApprovedForAllFunction();
                isApprovedForAllFunction.Account = account;
                isApprovedForAllFunction.Operator = @operator;
            yield return Query(isApprovedForAllFunction, blockParameter);
        }

    }

    public partial class MintTransactionRequest : ContractFunctionTransactionRequest<MintFunction>
    {

        public MintTransactionRequest(IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, string contractAddress) : base(contractTransactionUnityRequestFactory, contractAddress)
        {
        }

        public MintTransactionRequest(string url, BigInteger chainId, string privateKey, string contractAddress, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, chainId, privateKey, contractAddress, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator SignAndSendTransaction(string account, BigInteger id, BigInteger amount, byte[] data, BlockParameter blockParameter = null)
        {
            var mintFunction = new MintFunction();
                mintFunction.Account = account;
                mintFunction.Id = id;
                mintFunction.Amount = amount;
                mintFunction.Data = data;
            yield return SignAndSendTransaction(mintFunction);
        }

    }

    public partial class MintBatchTransactionRequest : ContractFunctionTransactionRequest<MintBatchFunction>
    {

        public MintBatchTransactionRequest(IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, string contractAddress) : base(contractTransactionUnityRequestFactory, contractAddress)
        {
        }

        public MintBatchTransactionRequest(string url, BigInteger chainId, string privateKey, string contractAddress, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, chainId, privateKey, contractAddress, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator SignAndSendTransaction(string to, List<BigInteger> ids, List<BigInteger> amounts, byte[] data, BlockParameter blockParameter = null)
        {
            var mintBatchFunction = new MintBatchFunction();
                mintBatchFunction.To = to;
                mintBatchFunction.Ids = ids;
                mintBatchFunction.Amounts = amounts;
                mintBatchFunction.Data = data;
            yield return SignAndSendTransaction(mintBatchFunction);
        }

    }

    public partial class OwnerQueryRequest : ContractFunctionQueryRequest<OwnerFunction, OwnerOutputDTO>
    {

        public OwnerQueryRequest(IContractQueryUnityRequestFactory contractQueryUnityRequestFactory, string contractAddress) : base(contractQueryUnityRequestFactory, contractAddress)
        {
        }

        public OwnerQueryRequest(string url, string contractAddress, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, contractAddress, defaultAccount, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator Query(BlockParameter blockParameter = null)
        {
            var ownerFunction = new OwnerFunction();
            yield return Query(ownerFunction, blockParameter);
        }

    }

    public partial class PauseTransactionRequest : ContractFunctionTransactionRequest<PauseFunction>
    {

        public PauseTransactionRequest(IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, string contractAddress) : base(contractTransactionUnityRequestFactory, contractAddress)
        {
        }

        public PauseTransactionRequest(string url, BigInteger chainId, string privateKey, string contractAddress, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, chainId, privateKey, contractAddress, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator SignAndSendTransaction(BlockParameter blockParameter = null)
        {
            var pauseFunction = new PauseFunction();
            yield return SignAndSendTransaction(pauseFunction);
        }

    }

    public partial class PausedQueryRequest : ContractFunctionQueryRequest<PausedFunction, PausedOutputDTO>
    {

        public PausedQueryRequest(IContractQueryUnityRequestFactory contractQueryUnityRequestFactory, string contractAddress) : base(contractQueryUnityRequestFactory, contractAddress)
        {
        }

        public PausedQueryRequest(string url, string contractAddress, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, contractAddress, defaultAccount, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator Query(BlockParameter blockParameter = null)
        {
            var pausedFunction = new PausedFunction();
            yield return Query(pausedFunction, blockParameter);
        }

    }

    public partial class RenounceOwnershipTransactionRequest : ContractFunctionTransactionRequest<RenounceOwnershipFunction>
    {

        public RenounceOwnershipTransactionRequest(IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, string contractAddress) : base(contractTransactionUnityRequestFactory, contractAddress)
        {
        }

        public RenounceOwnershipTransactionRequest(string url, BigInteger chainId, string privateKey, string contractAddress, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, chainId, privateKey, contractAddress, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator SignAndSendTransaction(BlockParameter blockParameter = null)
        {
            var renounceOwnershipFunction = new RenounceOwnershipFunction();
            yield return SignAndSendTransaction(renounceOwnershipFunction);
        }

    }

    public partial class SafeBatchTransferFromTransactionRequest : ContractFunctionTransactionRequest<SafeBatchTransferFromFunction>
    {

        public SafeBatchTransferFromTransactionRequest(IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, string contractAddress) : base(contractTransactionUnityRequestFactory, contractAddress)
        {
        }

        public SafeBatchTransferFromTransactionRequest(string url, BigInteger chainId, string privateKey, string contractAddress, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, chainId, privateKey, contractAddress, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator SignAndSendTransaction(string from, string to, List<BigInteger> ids, List<BigInteger> amounts, byte[] data, BlockParameter blockParameter = null)
        {
            var safeBatchTransferFromFunction = new SafeBatchTransferFromFunction();
                safeBatchTransferFromFunction.From = from;
                safeBatchTransferFromFunction.To = to;
                safeBatchTransferFromFunction.Ids = ids;
                safeBatchTransferFromFunction.Amounts = amounts;
                safeBatchTransferFromFunction.Data = data;
            yield return SignAndSendTransaction(safeBatchTransferFromFunction);
        }

    }

    public partial class SafeTransferFromTransactionRequest : ContractFunctionTransactionRequest<SafeTransferFromFunction>
    {

        public SafeTransferFromTransactionRequest(IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, string contractAddress) : base(contractTransactionUnityRequestFactory, contractAddress)
        {
        }

        public SafeTransferFromTransactionRequest(string url, BigInteger chainId, string privateKey, string contractAddress, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, chainId, privateKey, contractAddress, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator SignAndSendTransaction(string from, string to, BigInteger id, BigInteger amount, byte[] data, BlockParameter blockParameter = null)
        {
            var safeTransferFromFunction = new SafeTransferFromFunction();
                safeTransferFromFunction.From = from;
                safeTransferFromFunction.To = to;
                safeTransferFromFunction.Id = id;
                safeTransferFromFunction.Amount = amount;
                safeTransferFromFunction.Data = data;
            yield return SignAndSendTransaction(safeTransferFromFunction);
        }

    }

    public partial class SetApprovalForAllTransactionRequest : ContractFunctionTransactionRequest<SetApprovalForAllFunction>
    {

        public SetApprovalForAllTransactionRequest(IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, string contractAddress) : base(contractTransactionUnityRequestFactory, contractAddress)
        {
        }

        public SetApprovalForAllTransactionRequest(string url, BigInteger chainId, string privateKey, string contractAddress, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, chainId, privateKey, contractAddress, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator SignAndSendTransaction(string @operator, bool approved, BlockParameter blockParameter = null)
        {
            var setApprovalForAllFunction = new SetApprovalForAllFunction();
                setApprovalForAllFunction.Operator = @operator;
                setApprovalForAllFunction.Approved = approved;
            yield return SignAndSendTransaction(setApprovalForAllFunction);
        }

    }

    public partial class SetTokenUriTransactionRequest : ContractFunctionTransactionRequest<SetTokenUriFunction>
    {

        public SetTokenUriTransactionRequest(IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, string contractAddress) : base(contractTransactionUnityRequestFactory, contractAddress)
        {
        }

        public SetTokenUriTransactionRequest(string url, BigInteger chainId, string privateKey, string contractAddress, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, chainId, privateKey, contractAddress, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator SignAndSendTransaction(BigInteger tokenId, string tokenURI, BlockParameter blockParameter = null)
        {
            var setTokenUriFunction = new SetTokenUriFunction();
                setTokenUriFunction.TokenId = tokenId;
                setTokenUriFunction.TokenURI = tokenURI;
            yield return SignAndSendTransaction(setTokenUriFunction);
        }

    }

    public partial class SetURITransactionRequest : ContractFunctionTransactionRequest<SetURIFunction>
    {

        public SetURITransactionRequest(IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, string contractAddress) : base(contractTransactionUnityRequestFactory, contractAddress)
        {
        }

        public SetURITransactionRequest(string url, BigInteger chainId, string privateKey, string contractAddress, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, chainId, privateKey, contractAddress, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator SignAndSendTransaction(string newuri, BlockParameter blockParameter = null)
        {
            var setURIFunction = new SetURIFunction();
                setURIFunction.Newuri = newuri;
            yield return SignAndSendTransaction(setURIFunction);
        }

    }

    public partial class SupportsInterfaceQueryRequest : ContractFunctionQueryRequest<SupportsInterfaceFunction, SupportsInterfaceOutputDTO>
    {

        public SupportsInterfaceQueryRequest(IContractQueryUnityRequestFactory contractQueryUnityRequestFactory, string contractAddress) : base(contractQueryUnityRequestFactory, contractAddress)
        {
        }

        public SupportsInterfaceQueryRequest(string url, string contractAddress, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, contractAddress, defaultAccount, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator Query(byte[] interfaceId, BlockParameter blockParameter = null)
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
                supportsInterfaceFunction.InterfaceId = interfaceId;
            yield return Query(supportsInterfaceFunction, blockParameter);
        }

    }

    public partial class TotalSupplyQueryRequest : ContractFunctionQueryRequest<TotalSupplyFunction, TotalSupplyOutputDTO>
    {

        public TotalSupplyQueryRequest(IContractQueryUnityRequestFactory contractQueryUnityRequestFactory, string contractAddress) : base(contractQueryUnityRequestFactory, contractAddress)
        {
        }

        public TotalSupplyQueryRequest(string url, string contractAddress, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, contractAddress, defaultAccount, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator Query(BigInteger id, BlockParameter blockParameter = null)
        {
            var totalSupplyFunction = new TotalSupplyFunction();
                totalSupplyFunction.Id = id;
            yield return Query(totalSupplyFunction, blockParameter);
        }

    }

    public partial class TransferOwnershipTransactionRequest : ContractFunctionTransactionRequest<TransferOwnershipFunction>
    {

        public TransferOwnershipTransactionRequest(IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, string contractAddress) : base(contractTransactionUnityRequestFactory, contractAddress)
        {
        }

        public TransferOwnershipTransactionRequest(string url, BigInteger chainId, string privateKey, string contractAddress, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, chainId, privateKey, contractAddress, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator SignAndSendTransaction(string newOwner, BlockParameter blockParameter = null)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
                transferOwnershipFunction.NewOwner = newOwner;
            yield return SignAndSendTransaction(transferOwnershipFunction);
        }

    }

    public partial class UnpauseTransactionRequest : ContractFunctionTransactionRequest<UnpauseFunction>
    {

        public UnpauseTransactionRequest(IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, string contractAddress) : base(contractTransactionUnityRequestFactory, contractAddress)
        {
        }

        public UnpauseTransactionRequest(string url, BigInteger chainId, string privateKey, string contractAddress, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, chainId, privateKey, contractAddress, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator SignAndSendTransaction(BlockParameter blockParameter = null)
        {
            var unpauseFunction = new UnpauseFunction();
            yield return SignAndSendTransaction(unpauseFunction);
        }

    }

    public partial class UriQueryRequest : ContractFunctionQueryRequest<UriFunction, UriOutputDTO>
    {

        public UriQueryRequest(IContractQueryUnityRequestFactory contractQueryUnityRequestFactory, string contractAddress) : base(contractQueryUnityRequestFactory, contractAddress)
        {
        }

        public UriQueryRequest(string url, string contractAddress, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, contractAddress, defaultAccount, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator Query(BigInteger tokenId, BlockParameter blockParameter = null)
        {
            var uriFunction = new UriFunction();
                uriFunction.TokenId = tokenId;
            yield return Query(uriFunction, blockParameter);
        }

    }
}
