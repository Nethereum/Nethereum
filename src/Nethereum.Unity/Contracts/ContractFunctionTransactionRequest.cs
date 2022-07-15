using System.Collections;
using System.Collections.Generic;
using Nethereum.Contracts;
using System.Numerics;
using Newtonsoft.Json;
using Nethereum.JsonRpc.UnityClient;

namespace Nethereum.Unity.Contracts
{
    public class ContractFunctionTransactionRequest<TFunctionMessage> : UnityRequest<string>, IUnityRequest<string>
         where TFunctionMessage : FunctionMessage, new()
    {
        protected IContractTransactionUnityRequest ContractTransactionUnityRequest { get; set; }
        public string ContractAddress { get; protected set; }
        protected IContractTransactionUnityRequestFactory ContractTransactionUnityRequestFactory { get; }


        public ContractFunctionTransactionRequest(string url, BigInteger chainId, string privateKey, string contractAddress, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null)
        {
            ContractTransactionUnityRequestFactory = new ContractTransactionUnityRequestFactory(url, chainId, privateKey, jsonSerializerSettings, requestHeaders);
            ContractTransactionUnityRequest = ContractTransactionUnityRequestFactory.CreateContractTransactionUnityRequest();
            ContractAddress = contractAddress;
        }

        public ContractFunctionTransactionRequest(IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, string contractAddress)
        {
            ContractTransactionUnityRequest = contractTransactionUnityRequestFactory.CreateContractTransactionUnityRequest();
            ContractAddress = contractAddress;
            ContractTransactionUnityRequestFactory = contractTransactionUnityRequestFactory;
        }

        public IEnumerator SignAndSendTransaction(TFunctionMessage function)
        {
            ContractTransactionUnityRequest.Result = null;
            ContractTransactionUnityRequest.Exception = null;
            yield return ContractTransactionUnityRequest.SignAndSendTransaction(function, ContractAddress);
            if (ContractTransactionUnityRequest.Exception == null)
            {
                Result = ContractTransactionUnityRequest.Result;
            }
            else
            {
                Exception = ContractTransactionUnityRequest.Exception;
                yield break;
            }
        }
    }
}
