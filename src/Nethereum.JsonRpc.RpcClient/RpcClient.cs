using EdjCase.JsonRpc.Client;
using EdjCase.JsonRpc.Core;
using Newtonsoft.Json;
using System;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Nethereum.JsonRpc.Client
{
    public class RpcClient : ClientBase
    {
        private readonly EdjCase.JsonRpc.Client.RpcClient _innerRpcClient;

        public RpcClient(Uri baseUrl, AuthenticationHeaderValue authHeaderValue = null,
            JsonSerializerSettings jsonSerializerSettings = null)
        {
            if (jsonSerializerSettings == null)
            {
                jsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();
            }
            this._innerRpcClient = new EdjCase.JsonRpc.Client.RpcClient(baseUrl, (AuthenticationHeaderValue)authHeaderValue, (JsonSerializerSettings)jsonSerializerSettings, null, null);
        }

        protected override async Task<T> SendInnerRequestAync<T>(RpcRequest request, string route = null)
        {
            var response =
                await _innerRpcClient.SendRequestAsync(
                        new EdjCase.JsonRpc.Core.RpcRequest(request.Id, request.Method, (object[])request.RawParameters), route)
                    .ConfigureAwait(false);
            HandleRpcError(response);
            return response.GetResult<T>();
        }

        protected override async Task<T> SendInnerRequestAync<T>(string method, string route = null,
            params object[] paramList)
        {
            var response = await _innerRpcClient.SendRequestAsync(method, route, paramList).ConfigureAwait(false);
            HandleRpcError(response);
            return response.GetResult<T>();
        }

        private void HandleRpcError(RpcResponse response)
        {
            if (response.HasError)
                throw new RpcResponseException(new RpcError(response.Error.Code, response.Error.Message,
                    response.Error.Data));
        }

        public override async Task SendRequestAsync(RpcRequest request, string route = null)
        {
            var response =
                await _innerRpcClient.SendRequestAsync(
                        new EdjCase.JsonRpc.Core.RpcRequest(request.Id, request.Method, (object[])request.RawParameters), route)
                    .ConfigureAwait(false);
            HandleRpcError(response);
        }

        public override async Task SendRequestAsync(string method, string route = null, params object[] paramList)
        {
            var response = await _innerRpcClient.SendRequestAsync(method, route, paramList).ConfigureAwait(false);
            HandleRpcError(response);
        }
    }
}