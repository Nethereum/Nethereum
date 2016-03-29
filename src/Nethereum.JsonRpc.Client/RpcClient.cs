using System;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
using Newtonsoft.Json;

namespace Nethereum.JsonRpc.Client
{
    public class RpcClient : IClient
    {
        private edjCase.JsonRpc.Client.RpcClient innerRpcClient;

        public RpcClient(Uri baseUrl, AuthenticationHeaderValue authHeaderValue = null, JsonSerializerSettings jsonSerializerSettings = null)
        {
            this.innerRpcClient = new edjCase.JsonRpc.Client.RpcClient(baseUrl, authHeaderValue, jsonSerializerSettings);
        }

        public Task<RpcResponse> SendRequestAsync(RpcRequest request, string route = null)
        {
            return innerRpcClient.SendRequestAsync(request, route);
        }

        public Task<RpcResponse> SendRequestAsync(string method, string route = null, params object[] paramList)
        {
            return innerRpcClient.SendRequestAsync(method, route, paramList);
        }
    }
}