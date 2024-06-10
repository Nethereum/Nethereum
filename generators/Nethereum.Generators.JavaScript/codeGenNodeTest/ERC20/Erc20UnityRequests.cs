using System;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Unity.Contracts;
using Newtonsoft.Json;
using Nethereum.Unity.Contracts.Standards.ERC20.ContractDefinition;

namespace Nethereum.Unity.Contracts.Standards.ERC20
{
    public partial class DomainSeparatorQueryRequest : ContractFunctionQueryRequest<DomainSeparatorFunction, DomainSeparatorOutputDTO>
    {

        public DomainSeparatorQueryRequest(IContractQueryUnityRequestFactory contractQueryUnityRequestFactory, string contractAddress) : base(contractQueryUnityRequestFactory, contractAddress)
        {
        }

        public DomainSeparatorQueryRequest(string url, string contractAddress, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, contractAddress, defaultAccount, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator Query(BlockParameter blockParameter = null)
        {
            var domainSeparatorFunction = new DomainSeparatorFunction();
            yield return Query(domainSeparatorFunction, blockParameter);
        }

    }

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

        public IEnumerator Query(string account, BlockParameter blockParameter = null)
        {
            var balanceOfFunction = new BalanceOfFunction();
                balanceOfFunction.Account = account;
            yield return Query(balanceOfFunction, blockParameter);
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

    public partial class Eip712DomainQueryRequest : ContractFunctionQueryRequest<Eip712DomainFunction, Eip712DomainOutputDTO>
    {

        public Eip712DomainQueryRequest(IContractQueryUnityRequestFactory contractQueryUnityRequestFactory, string contractAddress) : base(contractQueryUnityRequestFactory, contractAddress)
        {
        }

        public Eip712DomainQueryRequest(string url, string contractAddress, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, contractAddress, defaultAccount, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator Query(BlockParameter blockParameter = null)
        {
            var eip712DomainFunction = new Eip712DomainFunction();
            yield return Query(eip712DomainFunction, blockParameter);
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

    public partial class NoncesQueryRequest : ContractFunctionQueryRequest<NoncesFunction, NoncesOutputDTO>
    {

        public NoncesQueryRequest(IContractQueryUnityRequestFactory contractQueryUnityRequestFactory, string contractAddress) : base(contractQueryUnityRequestFactory, contractAddress)
        {
        }

        public NoncesQueryRequest(string url, string contractAddress, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, contractAddress, defaultAccount, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator Query(string owner, BlockParameter blockParameter = null)
        {
            var noncesFunction = new NoncesFunction();
                noncesFunction.Owner = owner;
            yield return Query(noncesFunction, blockParameter);
        }

    }

    public partial class PermitTransactionRequest : ContractFunctionTransactionRequest<PermitFunction>
    {

        public PermitTransactionRequest(IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, string contractAddress) : base(contractTransactionUnityRequestFactory, contractAddress)
        {
        }

        public PermitTransactionRequest(string url, BigInteger chainId, string privateKey, string contractAddress, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, chainId, privateKey, contractAddress, jsonSerializerSettings, requestHeaders)
        {
        }

        public IEnumerator SignAndSendTransaction(string owner, string spender, BigInteger value, BigInteger deadline, byte v, byte[] r, byte[] s, BlockParameter blockParameter = null)
        {
            var permitFunction = new PermitFunction();
                permitFunction.Owner = owner;
                permitFunction.Spender = spender;
                permitFunction.Value = value;
                permitFunction.Deadline = deadline;
                permitFunction.V = v;
                permitFunction.R = r;
                permitFunction.S = s;
            yield return SignAndSendTransaction(permitFunction);
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
