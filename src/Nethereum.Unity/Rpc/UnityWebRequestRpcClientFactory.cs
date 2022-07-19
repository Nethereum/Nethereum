using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nethereum.Unity.Rpc
{
    public class UnityWebRequestRpcClientFactory : IUnityRpcRequestClientFactory
    {
        public UnityWebRequestRpcClientFactory(string url, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null)
        {
            Url = url;
            JsonSerializerSettings = jsonSerializerSettings;
            RequestHeaders = requestHeaders;
        }

        public string Url { get; }
        public JsonSerializerSettings JsonSerializerSettings { get; }
        public Dictionary<string, string> RequestHeaders { get; }

        public IUnityRpcRequestClient CreateUnityRpcClient()
        {
            return new UnityWebRequestRpcClient(Url, JsonSerializerSettings, RequestHeaders);
        }
    }
}


