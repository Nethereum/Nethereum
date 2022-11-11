using System;
using System.Text;
using Nethereum.Unity.RpcModel;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using RpcError = Nethereum.JsonRpc.Client.RpcError;
using RpcRequest = Nethereum.JsonRpc.Client.RpcRequest;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.JsonRpc.Client.RpcMessages;
using System.Data;

namespace Nethereum.Unity.Rpc
{


    public class UnityRpcRequest<TResult> : UnityRequest<TResult>
    {
        private IUnityRpcRequestClient _unityRpcClient;
        public UnityRpcRequest(string url, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null)
        {
            _unityRpcClient = new UnityWebRequestRpcClient(url, jsonSerializerSettings, requestHeaders);
        }

        public UnityRpcRequest(IUnityRpcRequestClientFactory unityRpcClientFactory)
        {
            _unityRpcClient = unityRpcClientFactory.CreateUnityRpcClient();
        }

        private RpcResponseException HandleRpcError(RpcResponseMessage response)
        {
            if (response.HasError)
                return new RpcResponseException(new RpcError(response.Error.Code, response.Error.Message,
                    response.Error.Data));
            return null;
        }

        public IEnumerator SendRequest(RpcRequest request)
        {
            yield return _unityRpcClient.SendRequest(request);
            try
            {

                if (_unityRpcClient.Exception == null)
                {
                    Result = _unityRpcClient.Result.GetResult<TResult>(true, _unityRpcClient.JsonSerializerSettings);
                    Exception = HandleRpcError(_unityRpcClient.Result);
                }
                else
                {
                    Result = default;
                    Exception = _unityRpcClient.Exception;
                    yield break;
                }

            }
            catch (Exception ex)
            {
                Result = default;
                Exception = new Exception(ex.Message);
#if DEBUG
                Debug.Log(ex.Message);
#endif
            }
        }
    }
}


