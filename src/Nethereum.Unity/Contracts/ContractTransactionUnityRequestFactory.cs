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

        public IUnityRpcRequestClientFactory UnityRpcClientFactory { get; }

        public ContractTransactionUnityRequestFactory(string url, BigInteger chainId, string privateKey = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null)
        {
            _chainId = chainId;
            _privateKey = privateKey;
            UnityRpcClientFactory = new UnityWebRequestRpcClientFactory(url, jsonSerializerSettings, requestHeaders);
        }

        public ContractTransactionUnityRequestFactory(IUnityRpcRequestClientFactory unityRpcClientFactory, BigInteger chainId, string privateKey)
        {
            _chainId = chainId;
            _privateKey = privateKey;
            UnityRpcClientFactory = unityRpcClientFactory;
        }

        public IContractTransactionUnityRequest CreateContractTransactionUnityRequest()
        {
            return new TransactionSignedUnityRequest(_privateKey, _chainId, UnityRpcClientFactory);
        }
    }
}
