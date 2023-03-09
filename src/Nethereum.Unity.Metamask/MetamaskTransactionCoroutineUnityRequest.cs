using Nethereum.Contracts;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Unity.Contracts;
using Nethereum.Unity.Rpc;
using Nethereum.Unity.RpcModel;
using Nethereum.Util;
using Newtonsoft.Json;
using System;
using System.Collections;

namespace Nethereum.Unity.Metamask
{
    public class MetamaskTransactionCoroutineUnityRequest : UnityRequest<string>, IContractTransactionUnityRequest
    {
        private readonly EthEstimateGasUnityRequest _ethEstimateGasUnityRequest;
        private readonly EthSendTransactionUnityRequest _ethSendTransactionUnityRequest;
        private readonly string _account;
        private readonly IUnityRpcRequestClientFactory unityRpcRequestClientFactory;
        public bool EstimateGas { get; set; } = true;
        public bool UseLegacyAsDefault { get; set; }

        public MetamaskTransactionCoroutineUnityRequest(string account, IUnityRpcRequestClientFactory unityRpcRequestClientFactory)
        {

            _ethEstimateGasUnityRequest = new EthEstimateGasUnityRequest(unityRpcRequestClientFactory);
            _ethSendTransactionUnityRequest = new EthSendTransactionUnityRequest(unityRpcRequestClientFactory);
            _account = account;
            this.unityRpcRequestClientFactory = unityRpcRequestClientFactory;
        }

        public IEnumerator SignAndSendTransaction(TransactionInput transactionInput)
        {
            if (transactionInput == null) throw new ArgumentNullException("transactionInput");

            if (string.IsNullOrEmpty(transactionInput.From)) transactionInput.From = _account;

            if (!transactionInput.From.IsTheSameAddress(_account))
            {
                throw new Exception("Transaction Input From address does not match account");
            }

            if (transactionInput.Gas == null)
            {
                if (EstimateGas)
                {
                    yield return _ethEstimateGasUnityRequest.SendRequest(transactionInput);

                    if (_ethEstimateGasUnityRequest.Exception == null)
                    {
                        var gas = _ethEstimateGasUnityRequest.Result;
                        transactionInput.Gas = gas;
                    }
                    else
                    {
                        this.Exception = _ethEstimateGasUnityRequest.Exception;
                        yield break;
                    }
                }
            }

            yield return _ethSendTransactionUnityRequest.SendRequest(transactionInput);

            if (_ethSendTransactionUnityRequest.Exception == null)
            {
                this.Result = _ethSendTransactionUnityRequest.Result;
            }
            else
            {
                this.Exception = _ethSendTransactionUnityRequest.Exception;
                yield break;
            }
        }

        public IEnumerator SignAndSendDeploymentContractTransaction<TDeploymentMessage>() where TDeploymentMessage : ContractDeploymentMessage, new()
        {
            var deploymentMessage = new TDeploymentMessage();
            yield return SignAndSendDeploymentContractTransaction(deploymentMessage);
        }

        public IEnumerator SignAndSendDeploymentContractTransaction<TDeploymentMessage>(TDeploymentMessage deploymentMessage) where TDeploymentMessage : ContractDeploymentMessage
        {
            var transactionInput = deploymentMessage.CreateTransactionInput();
            yield return SignAndSendTransaction(transactionInput);
        }

        public IEnumerator SignAndSendTransaction<TContractFunction>(TContractFunction function, string contractAdress) where TContractFunction : FunctionMessage
        {
            var transactionInput = function.CreateTransactionInput(contractAdress);
            yield return SignAndSendTransaction(transactionInput);
        }
    }
}




