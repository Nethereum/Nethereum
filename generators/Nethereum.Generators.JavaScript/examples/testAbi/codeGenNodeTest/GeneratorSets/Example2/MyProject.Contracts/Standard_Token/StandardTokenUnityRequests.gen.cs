using System;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Unity.Contracts;
using Newtonsoft.Json;
using MyProject.Contracts.Standard_Token.ContractDefinition;

namespace MyProject.Contracts.Standard_Token
{
    public partial class AllowanceQueryRequest : ContractFunctionQueryRequest<AllowanceFunction, AllowanceOutputDTO>
    {

        public AllowanceQueryRequest(IContractQueryUnityRequestFactory contractQueryUnityRequestFactory, string contractAddress) : base(contractQueryUnityRequestFactory, contractAddress)
        {
        }

        public AllowanceQueryRequest(string url, string contractAddress, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, contractAddress, defaultAccount, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator Query(string owner, string spender, BlockParameter blockParameter = null)
        {
            var allowanceFunction = new AllowanceFunction();
                allowanceFunction.Owner = owner;
                allowanceFunction.Spender = spender;
            yield return Query(allowanceFunction, blockParameter);
        }

    }

    public partial class AllowedQueryRequest : ContractFunctionQueryRequest<AllowedFunction, AllowedOutputDTO>
    {

        public AllowedQueryRequest(IContractQueryUnityRequestFactory contractQueryUnityRequestFactory, string contractAddress) : base(contractQueryUnityRequestFactory, contractAddress)
        {
        }

        public AllowedQueryRequest(string url, string contractAddress, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, contractAddress, defaultAccount, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator Query(string returnValue1, string returnValue2, BlockParameter blockParameter = null)
        {
            var allowedFunction = new AllowedFunction();
                allowedFunction.ReturnValue1 = returnValue1;
                allowedFunction.ReturnValue2 = returnValue2;
            yield return Query(allowedFunction, blockParameter);
        }

    }

    public partial class ApproveTransactionRequest : ContractFunctionTransactionRequest<ApproveFunction>
    {

        public ApproveTransactionRequest(IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, string contractAddress) : base(contractTransactionUnityRequestFactory, contractAddress)
        {
        }

        public ApproveTransactionRequest(string url, BigInteger chainId, string privateKey, string contractAddress, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, chainId, privateKey, contractAddress, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator SignAndSendTransaction(string spender, BigInteger value, BlockParameter blockParameter = null)
        {
            var approveFunction = new ApproveFunction();
                approveFunction.Spender = spender;
                approveFunction.Value = value;
            yield return SignAndSendTransaction(approveFunction);
        }

    }

    public partial class BalanceOfQueryRequest : ContractFunctionQueryRequest<BalanceOfFunction, BalanceOfOutputDTO>
    {

        public BalanceOfQueryRequest(IContractQueryUnityRequestFactory contractQueryUnityRequestFactory, string contractAddress) : base(contractQueryUnityRequestFactory, contractAddress)
        {
        }

        public BalanceOfQueryRequest(string url, string contractAddress, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, contractAddress, defaultAccount, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator Query(string owner, BlockParameter blockParameter = null)
        {
            var balanceOfFunction = new BalanceOfFunction();
                balanceOfFunction.Owner = owner;
            yield return Query(balanceOfFunction, blockParameter);
        }

    }

    public partial class BalancesQueryRequest : ContractFunctionQueryRequest<BalancesFunction, BalancesOutputDTO>
    {

        public BalancesQueryRequest(IContractQueryUnityRequestFactory contractQueryUnityRequestFactory, string contractAddress) : base(contractQueryUnityRequestFactory, contractAddress)
        {
        }

        public BalancesQueryRequest(string url, string contractAddress, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, contractAddress, defaultAccount, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator Query(string returnValue1, BlockParameter blockParameter = null)
        {
            var balancesFunction = new BalancesFunction();
                balancesFunction.ReturnValue1 = returnValue1;
            yield return Query(balancesFunction, blockParameter);
        }

    }

    public partial class DecimalsQueryRequest : ContractFunctionQueryRequest<DecimalsFunction, DecimalsOutputDTO>
    {

        public DecimalsQueryRequest(IContractQueryUnityRequestFactory contractQueryUnityRequestFactory, string contractAddress) : base(contractQueryUnityRequestFactory, contractAddress)
        {
        }

        public DecimalsQueryRequest(string url, string contractAddress, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, contractAddress, defaultAccount, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator Query(BlockParameter blockParameter = null)
        {
            var decimalsFunction = new DecimalsFunction();
            yield return Query(decimalsFunction, blockParameter);
        }

    }

    public partial class NameQueryRequest : ContractFunctionQueryRequest<NameFunction, NameOutputDTO>
    {

        public NameQueryRequest(IContractQueryUnityRequestFactory contractQueryUnityRequestFactory, string contractAddress) : base(contractQueryUnityRequestFactory, contractAddress)
        {
        }

        public NameQueryRequest(string url, string contractAddress, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, contractAddress, defaultAccount, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator Query(BlockParameter blockParameter = null)
        {
            var nameFunction = new NameFunction();
            yield return Query(nameFunction, blockParameter);
        }

    }

    public partial class SymbolQueryRequest : ContractFunctionQueryRequest<SymbolFunction, SymbolOutputDTO>
    {

        public SymbolQueryRequest(IContractQueryUnityRequestFactory contractQueryUnityRequestFactory, string contractAddress) : base(contractQueryUnityRequestFactory, contractAddress)
        {
        }

        public SymbolQueryRequest(string url, string contractAddress, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, contractAddress, defaultAccount, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator Query(BlockParameter blockParameter = null)
        {
            var symbolFunction = new SymbolFunction();
            yield return Query(symbolFunction, blockParameter);
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

        public IEnumerator Query(BlockParameter blockParameter = null)
        {
            var totalSupplyFunction = new TotalSupplyFunction();
            yield return Query(totalSupplyFunction, blockParameter);
        }

    }

    public partial class TransferTransactionRequest : ContractFunctionTransactionRequest<TransferFunction>
    {

        public TransferTransactionRequest(IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, string contractAddress) : base(contractTransactionUnityRequestFactory, contractAddress)
        {
        }

        public TransferTransactionRequest(string url, BigInteger chainId, string privateKey, string contractAddress, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, chainId, privateKey, contractAddress, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator SignAndSendTransaction(string to, BigInteger value, BlockParameter blockParameter = null)
        {
            var transferFunction = new TransferFunction();
                transferFunction.To = to;
                transferFunction.Value = value;
            yield return SignAndSendTransaction(transferFunction);
        }

    }

    public partial class TransferFromTransactionRequest : ContractFunctionTransactionRequest<TransferFromFunction>
    {

        public TransferFromTransactionRequest(IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, string contractAddress) : base(contractTransactionUnityRequestFactory, contractAddress)
        {
        }

        public TransferFromTransactionRequest(string url, BigInteger chainId, string privateKey, string contractAddress, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, chainId, privateKey, contractAddress, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator SignAndSendTransaction(string from, string to, BigInteger value, BlockParameter blockParameter = null)
        {
            var transferFromFunction = new TransferFromFunction();
                transferFromFunction.From = from;
                transferFromFunction.To = to;
                transferFromFunction.Value = value;
            yield return SignAndSendTransaction(transferFromFunction);
        }

    }
}
