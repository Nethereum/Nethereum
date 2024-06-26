using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Contracts;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.TransactionManagers;
using Nethereum.Unity.Rpc;
using Newtonsoft.Json;

namespace Nethereum.Unity.Contracts
{

    public class EstimateContractTransactionUnityRequest : UnityRequest<HexBigInteger>
    {
        private readonly EthEstimateGasUnityRequest _ethEstimateGasUnityRequest;

        public EstimateContractTransactionUnityRequest(string url, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null)
        {
            _ethEstimateGasUnityRequest = new EthEstimateGasUnityRequest(url, jsonSerializerSettings, requestHeaders);
        }

        public EstimateContractTransactionUnityRequest(IUnityRpcRequestClientFactory unityRpcClientFactory)
        {
            _ethEstimateGasUnityRequest = new EthEstimateGasUnityRequest(unityRpcClientFactory);
        }

        public IEnumerator EstimateContractFunction<TContractFunction>(TContractFunction function, string contractAdress) where TContractFunction : FunctionMessage, new()
        {
            var callInput = function.CreateCallInput(contractAdress);
            yield return _ethEstimateGasUnityRequest.SendRequest(callInput);
        }

        public IEnumerator EstimateContractDeployment<TDeploymentMessage>(TDeploymentMessage deploymentMessage) where TDeploymentMessage : ContractDeploymentMessage, new()
        {
            var callInput = deploymentMessage.CreateCallInput();
            yield return _ethEstimateGasUnityRequest.SendRequest(callInput);
        }
    }
}