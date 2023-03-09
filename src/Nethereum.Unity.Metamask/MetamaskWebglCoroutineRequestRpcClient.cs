using AOT;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System;
using UnityEngine;
using System.Collections;
using Nethereum.Unity.Rpc;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;

namespace Nethereum.Unity.Metamask
{
    public class MetamaskWebglCoroutineRequestRpcClient : UnityRequest<RpcResponseMessage>, IUnityRpcRequestClient
    {

        public static ConcurrentDictionary<string, RpcResponseMessage> RequestResponses = new ConcurrentDictionary<string, RpcResponseMessage>();

        [MonoPInvokeCallback(typeof(Action<string>))]
        public static void RequestCallBack(string responseMessage)
        {
          
            var response = JsonConvert.DeserializeObject<RpcResponseMessage>(responseMessage, DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings());
            RequestResponses.TryAdd((string)response.Id, response);
          
        }

        public JsonSerializerSettings JsonSerializerSettings { get; set; }
        public int TimeOutMilliseconds { get; }

        private string _account;

        public MetamaskWebglCoroutineRequestRpcClient(string account, JsonSerializerSettings jsonSerializerSettings = null, int timeOutMilliseconds = WaitUntilRequestResponse.DefaultTimeOutMilliSeconds)
        {
            if (jsonSerializerSettings == null)
                jsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();

            JsonSerializerSettings = jsonSerializerSettings;
            TimeOutMilliseconds = timeOutMilliseconds;
            _account = account;
        }

        public IEnumerator SendRequest(RpcRequest request)
        {
            this.Exception = null;
            this.Result = null;
            var newUniqueRequestId = Guid.NewGuid().ToString();
            try
            {
                if (request.Method == ApiMethods.eth_sendTransaction.ToString())
                {
                    var transaction = (TransactionInput)request.RawParameters[0];
                    transaction.From = _account;
                    request.RawParameters[0] = transaction;
                }
                else if (request.Method == ApiMethods.eth_estimateGas.ToString() || request.Method == ApiMethods.eth_call.ToString())
                {

                    var callinput = (CallInput)request.RawParameters[0];
                    if (callinput.From == null)
                    {
                        callinput.From ??= _account;
                        request.RawParameters[0] = callinput;
                    }
                }
                else if (request.Method == ApiMethods.eth_signTypedData_v4.ToString() || request.Method == ApiMethods.personal_sign.ToString())
                {
                    var parameters = new object[] { _account, request.RawParameters[0] };
                    request = new RpcRequest(newUniqueRequestId, request.Method, parameters);
                }

                var metamaskRpcRequest = new MetamaskRpcRequestMessage(newUniqueRequestId, request.Method, _account,
                request.RawParameters);

                MetamaskWebglInterop.RequestRpcClientCallback(RequestCallBack, JsonConvert.SerializeObject(metamaskRpcRequest, JsonSerializerSettings));

            }
            catch (Exception ex)
            {
                Exception = ex;
                yield break;

            }


            var waitUntilRequestResponse = new WaitUntilRequestResponse(newUniqueRequestId, RequestResponses, TimeOutMilliseconds);
            yield return new WaitUntil(waitUntilRequestResponse.HasCompletedResponse);
            RpcResponseMessage responseMessage = null;

            if (MetamaskWebglCoroutineRequestRpcClient.RequestResponses.TryRemove(newUniqueRequestId, out responseMessage))
            {
                Result = responseMessage;
            }
            else
            {
                if (waitUntilRequestResponse.HasTimedOut)
                {
                    Exception = new Exception($"Metamask Response has timeout after : {TimeOutMilliseconds}");
                    yield break;
                }
                Exception = new Exception("Unexpected error retrieving message");
            }

            
        }
    }
}




