using System.Collections.Generic;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Unity.Rpc;
using Newtonsoft.Json;

namespace Nethereum.Unity.Contracts
{
    public class ContractQueryUnityRequestFactory : IContractQueryUnityRequestFactory
    {
        private string _defaultAccount;
        public IUnityRpcRequestClientFactory UnityRpcClientFactory { get; }

        public ContractQueryUnityRequestFactory(string url, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null)
        {
            _defaultAccount = defaultAccount;
            UnityRpcClientFactory = new UnityWebRequestRpcClientFactory(url, jsonSerializerSettings, requestHeaders);
        }

        public ContractQueryUnityRequestFactory(IUnityRpcRequestClientFactory unityRpcClientFactory, string defaultAccount)
        {
            _defaultAccount = defaultAccount;
            UnityRpcClientFactory = unityRpcClientFactory;
        }

        public IContractQueryUnityRequest<TFunctionMessage, TResponse> CreateQueryUnityRequest<TFunctionMessage, TResponse>() where TFunctionMessage : FunctionMessage, new() where TResponse : IFunctionOutputDTO, new()
        {
            return new QueryUnityRequest<TFunctionMessage, TResponse>(UnityRpcClientFactory, _defaultAccount);
        }
    }
}
