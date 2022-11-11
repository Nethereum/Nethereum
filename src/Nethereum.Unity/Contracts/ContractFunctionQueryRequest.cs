using Nethereum.RPC.Eth.DTOs;
using System.Collections;
using System.Collections.Generic;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Newtonsoft.Json;
using Nethereum.Unity.Rpc;

namespace Nethereum.Unity.Contracts
{
    public class ContractFunctionQueryRequest<TFunctionMessage, TResponse> : UnityRequest<TResponse>, IUnityRequest<TResponse>
         where TFunctionMessage : FunctionMessage, new()
         where TResponse : IFunctionOutputDTO, new()
    {
        protected IContractQueryUnityRequest<TFunctionMessage, TResponse> QueryUnityRequest { get; set; }
        public string ContractAddress { get; protected set; }
        protected IContractQueryUnityRequestFactory ContractQueryUnityRequestFactory { get; }

        public ContractFunctionQueryRequest(string url, string contractAddress, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null)
        {
            ContractQueryUnityRequestFactory = new ContractQueryUnityRequestFactory(url, defaultAccount, jsonSerializerSettings, requestHeaders);
            QueryUnityRequest = ContractQueryUnityRequestFactory.CreateQueryUnityRequest<TFunctionMessage, TResponse>();
            ContractAddress = contractAddress;
        }

        public ContractFunctionQueryRequest(IContractQueryUnityRequestFactory contractQueryUnityRequestFactory, string contractAddress)
        {
            QueryUnityRequest = contractQueryUnityRequestFactory.CreateQueryUnityRequest<TFunctionMessage, TResponse>();
            ContractAddress = contractAddress;
            ContractQueryUnityRequestFactory = contractQueryUnityRequestFactory;
        }

        public IEnumerator Query(TFunctionMessage functionMessage,
         BlockParameter blockParameter = null)
        {
            QueryUnityRequest.Result = default;
            QueryUnityRequest.Exception = null;
            yield return QueryUnityRequest.Query(functionMessage, ContractAddress, blockParameter);

            if (QueryUnityRequest.Exception == null)
            {
                Result = QueryUnityRequest.Result;
            }
            else
            {
                Exception = QueryUnityRequest.Exception;
                yield break;
            }
        }

    }
}
