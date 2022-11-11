using System.Collections.Generic;
using System.Numerics;
using Nethereum.Unity.Rpc;
using Newtonsoft.Json;

namespace Nethereum.Unity.Contracts
{
    public class ContractTransactionUnityRequestFactory : IContractTransactionUnityRequestFactory
    {
        private readonly BigInteger _chainId;
        private readonly string _privateKey;
        private readonly IUnityRpcRequestClientFactory _unityRpcClientFactory;

        public ContractTransactionUnityRequestFactory(string url, BigInteger chainId, string privateKey = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null)
        {
            _chainId = chainId;
            _privateKey = privateKey;
            _unityRpcClientFactory = new UnityWebRequestRpcClientFactory(url, jsonSerializerSettings, requestHeaders);
        }

        public ContractTransactionUnityRequestFactory(IUnityRpcRequestClientFactory unityRpcClientFactory, BigInteger chainId, string privateKey)
        {
            _chainId = chainId;
            _privateKey = privateKey;
            _unityRpcClientFactory = unityRpcClientFactory;
        }

        public IContractTransactionUnityRequest CreateContractTransactionUnityRequest()
        {
            return new TransactionSignedUnityRequest(_privateKey, _chainId, _unityRpcClientFactory);
        }
    }
}
