using System.Collections.Generic;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.JsonRpc.UnityClient;
using Newtonsoft.Json;

namespace Nethereum.Unity.Contracts
{
    public class ContractQueryUnityRequestFactory : IContractQueryUnityRequestFactory
    {
        private readonly IUnityRpcRequestClientFactory _unityRpcClientFactory;
        private string _defaultAccount;

        public ContractQueryUnityRequestFactory(string url, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null)
        {
            _defaultAccount = defaultAccount;
            _unityRpcClientFactory = new UnityWebRequestRpcClientFactory(url, jsonSerializerSettings, requestHeaders);
        }

        public ContractQueryUnityRequestFactory(IUnityRpcRequestClientFactory unityRpcClientFactory, string defaultAccount)
        {
            _defaultAccount = defaultAccount;
            _unityRpcClientFactory = unityRpcClientFactory;
        }

        public IContractQueryUnityRequest<TFunctionMessage, TResponse> CreateQueryUnityRequest<TFunctionMessage, TResponse>() where TFunctionMessage : FunctionMessage, new() where TResponse : IFunctionOutputDTO, new()
        {
            return new QueryUnityRequest<TFunctionMessage, TResponse>(_unityRpcClientFactory, _defaultAccount);
        }
    }
}
