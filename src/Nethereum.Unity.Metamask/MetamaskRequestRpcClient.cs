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

namespace Nethereum.Unity.Metamask
{
    public class MetamaskRequestRpcClient : UnityRequest<RpcResponseMessage>, IUnityRpcRequestClient
    {

        public static ConcurrentDictionary<string, RpcResponseMessage> RequestResponses = new ConcurrentDictionary<string, RpcResponseMessage>();

        [MonoPInvokeCallback(typeof(Action<string>))]
        public static void RequestCallBack(string responseMessage)
        {
            var response = JsonConvert.DeserializeObject<RpcResponseMessage>(responseMessage, DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings());
            RequestResponses.TryAdd((string)response.Id, response);
        }

        public JsonSerializerSettings JsonSerializerSettings { get; set; }
        public int TimeOuMiliseconds { get; }

        private string _account;

        public MetamaskRequestRpcClient(string account, JsonSerializerSettings jsonSerializerSettings = null, int timeOuMiliseconds = WaitUntilRequestResponse.DefaultTimeOutMiliSeconds)
        {
            if (jsonSerializerSettings == null)
                jsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();

            JsonSerializerSettings = jsonSerializerSettings;
            TimeOuMiliseconds = timeOuMiliseconds;
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

                MetamaskInterop.RequestRpcClientCallback(RequestCallBack, JsonConvert.SerializeObject(metamaskRpcRequest, JsonSerializerSettings));

            }
            catch (Exception ex)
            {
                Exception = ex;
                yield break;

            }


            var waitUntilRequestResponse = new WaitUntilRequestResponse(newUniqueRequestId, TimeOuMiliseconds);
            yield return new WaitUntil(waitUntilRequestResponse.HasCompletedResponse);
            RpcResponseMessage responseMessage = null;

            if (RequestResponses.TryRemove(newUniqueRequestId, out responseMessage))
            {
                Result = responseMessage;
            }
            else
            {
                if (waitUntilRequestResponse.HasTimedOut)
                {
                    Exception = new Exception($"Metamask Response has timeout after : {TimeOuMiliseconds}");
                    yield break;
                }
                Exception = new Exception("Unexpected error retrieving message");
            }

            
        }
    }
}




