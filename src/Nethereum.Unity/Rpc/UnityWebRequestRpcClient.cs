using System;
using System.Text;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using RpcRequest = Nethereum.JsonRpc.Client.RpcRequest;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.Unity.Rpc
{
    public class UnityWebRequestRpcClient : UnityRequest<RpcResponseMessage>, IClientRequestHeaderSupport, IUnityRpcRequestClient
    {
        private readonly string _url;

        public JsonSerializerSettings JsonSerializerSettings { get; set; }

        public Dictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>();

        public UnityWebRequestRpcClient(string url, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null)
        {
            if (jsonSerializerSettings == null)
                jsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();
            _url = url;

            if (requestHeaders != null)
            {
                RequestHeaders = requestHeaders;
            }
            this.SetBasicAuthenticationHeaderFromUri(new Uri(url));

            JsonSerializerSettings = jsonSerializerSettings;
        }

        public IEnumerator SendRequest(RpcRequest request)
        {
            var requestFormatted = new RpcModel.RpcRequest(request.Id, request.Method, request.RawParameters);

            var rpcRequestJson = JsonConvert.SerializeObject(requestFormatted, JsonSerializerSettings);
            var requestBytes = Encoding.UTF8.GetBytes(rpcRequestJson);
            using (var unityRequest = new UnityWebRequest(_url, "POST"))
            {
                var uploadHandler = new UploadHandlerRaw(requestBytes);
                unityRequest.SetRequestHeader("Content-Type", "application/json");
                uploadHandler.contentType = "application/json";
                unityRequest.uploadHandler = uploadHandler;

                unityRequest.downloadHandler = new DownloadHandlerBuffer();

                if (RequestHeaders != null)
                {
                    foreach (var requestHeader in RequestHeaders)
                    {
                        unityRequest.SetRequestHeader(requestHeader.Key, requestHeader.Value);
                    }
                }

                yield return unityRequest.SendWebRequest();

                if (unityRequest.error != null)
                {
                    Exception = new Exception(unityRequest.error);
#if DEBUG
                    Debug.Log(unityRequest.error);
#endif
                }
                else
                {
                    try
                    {
                        byte[] results = unityRequest.downloadHandler.data;
                        var responseJson = Encoding.UTF8.GetString(results);
#if DEBUG
                        Debug.Log(responseJson);
#endif
                        Result = JsonConvert.DeserializeObject<RpcResponseMessage>(responseJson, JsonSerializerSettings);
                    }
                    catch (Exception ex)
                    {
                        Exception = new Exception(ex.Message);
#if DEBUG
                        Debug.Log(ex.Message);
#endif
                    }
                }
            }
        }
    }
}


