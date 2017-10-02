using System;
using System.Threading.Tasks;
using EdjCase.JsonRpc.Client;
using EdjCase.JsonRpc.Core;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json;
using RpcError = Nethereum.JsonRpc.Client.RpcError;
using RpcRequest = Nethereum.JsonRpc.Client.RpcRequest;

namespace Nethereum.JsonRpc.IpcClient
{
    public abstract class IpcClientBase : ClientBase, IDisposable
    {
        protected readonly string IpcPath;

        public IpcClientBase(string ipcPath, JsonSerializerSettings jsonSerializerSettings = null)
        {
            if (jsonSerializerSettings == null)
                jsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();
            this.IpcPath = ipcPath;
            JsonSerializerSettings = jsonSerializerSettings;
        }

        public JsonSerializerSettings JsonSerializerSettings { get; set; }

        protected override async Task<T> SendInnerRequestAync<T>(RpcRequest request, string route = null)
        {
            var response =
                await SendAsync<EdjCase.JsonRpc.Core.RpcRequest, RpcResponse>(
                        new EdjCase.JsonRpc.Core.RpcRequest(request.Id, request.Method, request.RawParameters))
                    .ConfigureAwait(false);
            HandleRpcError(response);
            return response.GetResult<T>();
        }

        protected override async Task<T> SendInnerRequestAync<T>(string method, string route = null,
            params object[] paramList)
        {
            var response =
                await SendAsync<EdjCase.JsonRpc.Core.RpcRequest, RpcResponse>(
                        new EdjCase.JsonRpc.Core.RpcRequest(Configuration.DefaultRequestId, method, paramList))
                    .ConfigureAwait(false);
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
                await SendAsync<EdjCase.JsonRpc.Core.RpcRequest, RpcResponse>(
                        new EdjCase.JsonRpc.Core.RpcRequest(request.Id, request.Method, request.RawParameters))
                    .ConfigureAwait(false);
            HandleRpcError(response);
        }

        public override async Task SendRequestAsync(string method, string route = null, params object[] paramList)
        {
            var response =
                await SendAsync<EdjCase.JsonRpc.Core.RpcRequest, RpcResponse>(
                        new EdjCase.JsonRpc.Core.RpcRequest(Configuration.DefaultRequestId, method, paramList))
                    .ConfigureAwait(false);
            HandleRpcError(response);
        }



        protected abstract Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request);

        #region IDisposable Support

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}
