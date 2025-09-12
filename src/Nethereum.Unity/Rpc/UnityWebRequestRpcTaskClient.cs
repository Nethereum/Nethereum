using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Unity.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using static Nethereum.JsonRpc.Client.UserAuthentication;

namespace Nethereum.Unity.Rpc
{
#if !DOTNET35
    public class UnityWebRequestRpcTaskClient : ClientBase, IClientRequestHeaderSupport
    {
        private readonly Uri _baseUrl;
        private readonly JsonSerializerSettings _jsonSerializerSettings;
        private readonly Microsoft.Extensions.Logging.ILogger _log;
        public Dictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>();

        public UnityWebRequestRpcTaskClient(Uri baseUrl,
            JsonSerializerSettings jsonSerializerSettings = null, Microsoft.Extensions.Logging.ILogger log = null)
        {
            _baseUrl = baseUrl;
            if (jsonSerializerSettings == null)
                jsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();

            _jsonSerializerSettings = jsonSerializerSettings;
            _log = log;
            this.SetBasicAuthenticationHeaderFromUri(baseUrl);
        }

        protected override async Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests)
        {
            var logger = new RpcLogger(_log);
            string uri = _baseUrl.AbsoluteUri;
            var rpcRequestJson = JsonConvert.SerializeObject(requests, _jsonSerializerSettings);
            var requestBytes = Encoding.UTF8.GetBytes(rpcRequestJson);
            var responseJson = await SendAsyncInternally(rpcRequestJson, uri, string.Empty, logger);
            try
            {
#if DEBUG
                Debug.Log(responseJson);
#endif
                var message = JsonConvert.DeserializeObject<RpcResponseMessage[]>(responseJson, _jsonSerializerSettings);
                return message;
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.Log(ex.Message);
#endif
                throw new RpcClientUnknownException("Error occurred when trying to send rpc request(s): ", ex);

            }
        }

        public override async Task<RpcResponseMessage> SendAsync(RpcRequestMessage request, string route = null)
        {
            var logger = new RpcLogger(_log);
            string uri = new Uri(_baseUrl, route).AbsoluteUri;
            var rpcRequestJson = JsonConvert.SerializeObject(request, _jsonSerializerSettings);

            var responseJson = await SendAsyncInternally(rpcRequestJson, uri, request.Method, logger);
            try
            {
#if DEBUG
                Debug.Log(responseJson);
#endif
                var message = JsonConvert.DeserializeObject<RpcResponseMessage>(responseJson, _jsonSerializerSettings);
                logger.LogResponse(message);
                return message;
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.Log(ex.Message);
#endif
                throw new RpcClientUnknownException("Error occurred when trying to send rpc request(s): " + request.Method, ex);

            }
        }

        private async Task<string> SendAsyncInternally(string rpcRequestJson, string uri, string rpcRequestMethod, RpcLogger logger)
        {
            if (rpcRequestMethod == null) rpcRequestMethod = string.Empty;
            var requestBytes = Encoding.UTF8.GetBytes(rpcRequestJson);
            using (var unityRequest = new UnityWebRequest(uri, "POST"))
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

                logger.LogRequest(rpcRequestJson);

                await unityRequest.SendWebRequest();


                if (unityRequest.error != null)
                {
#if DEBUG
                    Debug.Log(unityRequest.error);
#endif
                    throw new RpcClientUnknownException("Error occurred when trying to send rpc request(s): " + rpcRequestMethod, new Exception(unityRequest.error));
                }
                else
                {
                      
                    byte[] results = unityRequest.downloadHandler.data;
                    return Encoding.UTF8.GetString(results);

                }

            }
        }
    }
#endif
}
