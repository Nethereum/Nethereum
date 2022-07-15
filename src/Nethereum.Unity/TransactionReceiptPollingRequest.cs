using System.Collections;
using System.Collections.Generic;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;
using UnityEngine;

namespace Nethereum.JsonRpc.UnityClient
{
    public class TransactionReceiptPollingRequest : UnityRequest<TransactionReceipt>
    {
        private readonly EthGetTransactionReceiptUnityRequest _ethGetTransactionReceipt;
        public bool CancelPolling { get; set; } = false;

        public TransactionReceiptPollingRequest(string url, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null)
        {
            _ethGetTransactionReceipt = new EthGetTransactionReceiptUnityRequest(url, jsonSerializerSettings, requestHeaders);
            
        }

        public TransactionReceiptPollingRequest(IUnityRpcRequestClientFactory unityRpcClientFactory)
        {
            _ethGetTransactionReceipt = new EthGetTransactionReceiptUnityRequest(unityRpcClientFactory);
        }

        public IEnumerator PollForReceipt(string transactionHash, float secondsToWait)
        {
            TransactionReceipt receipt = null;
            Result = null;
            while (receipt == null)
            {
                if (!CancelPolling)
                {
                    yield return _ethGetTransactionReceipt.SendRequest(transactionHash);

                    if (_ethGetTransactionReceipt.Exception == null)
                    {
                        receipt = _ethGetTransactionReceipt.Result;
                    }
                    else
                    {
                        this.Exception = _ethGetTransactionReceipt.Exception;
                        yield break;
                    }
                }
                else
                {
                    yield break;
                }

                yield return new WaitForSeconds(secondsToWait);
            }

            Result = receipt;
        }
    }
}