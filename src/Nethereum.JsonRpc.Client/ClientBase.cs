#if !DOTNET35
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.JsonRpc.Client
{
    public abstract class ClientBase : IClient
    {
        public static TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(20.0);

        public RequestInterceptor OverridingRequestInterceptor { get; set; }

        public async Task<T> SendRequestAsync<T>(RpcRequest request, string route = null)
        {
            if (OverridingRequestInterceptor != null)
                return
                    (T)
                    await OverridingRequestInterceptor.InterceptSendRequestAsync(SendInnerRequestAsync<T>, request, route)
                        .ConfigureAwait(false);
            return await SendInnerRequestAsync<T>(request, route).ConfigureAwait(false);
        }

        public virtual async Task<RpcRequestResponseBatch> SendBatchRequestAsync(RpcRequestResponseBatch rpcRequestResponseBatch)
        {
            var responses = await SendAsync(rpcRequestResponseBatch.GetRpcRequests()).ConfigureAwait(false);
            rpcRequestResponseBatch.UpdateBatchItemResponses(responses);
            return rpcRequestResponseBatch;
        }

        public async Task<T> SendRequestAsync<T>(string method, string route = null, params object[] paramList)
        {
            if (OverridingRequestInterceptor != null)
                return
                    (T)
                    await OverridingRequestInterceptor.InterceptSendRequestAsync(SendInnerRequestAsync<T>, method, route,
                        paramList).ConfigureAwait(false);
            return await SendInnerRequestAsync<T>(method, route, paramList).ConfigureAwait(false);
        }

        protected void HandleRpcError(RpcResponseMessage response, string reqMsg)
        {
            if (response.HasError)
                throw new RpcResponseException(new RpcError(response.Error.Code, response.Error.Message + ": " + reqMsg,
                    response.Error.Data));
        }

        protected virtual async Task<T> SendInnerRequestAsync<T>(RpcRequestMessage reqMsg,
                                                       string route = null)
        {
            var response = await SendAsync(reqMsg, route).ConfigureAwait(false);
            HandleRpcError(response, reqMsg.Method);
            try
            {
                return DecodeResult<T>(response);
            }
            catch (FormatException formatException)
            {
                throw new RpcResponseFormatException("Invalid format found in RPC response", formatException);
            }
        }

        public virtual T DecodeResult<T>(RpcResponseMessage rpcResponseMessage)
        {
            return rpcResponseMessage.GetResult<T>();
        }

        protected virtual Task<T> SendInnerRequestAsync<T>(RpcRequest request, string route = null)
        {
            var reqMsg = new RpcRequestMessage(request.Id,
                                               request.Method,
                                               request.RawParameters);
            return SendInnerRequestAsync<T>(reqMsg, route);
        }

        protected virtual Task<T> SendInnerRequestAsync<T>(string method, string route = null,
            params object[] paramList)
        {
            var request = new RpcRequestMessage(Guid.NewGuid().ToString(), method, paramList);
            return SendInnerRequestAsync<T>(request, route);
        }

        public virtual async Task SendRequestAsync(RpcRequest request, string route = null)
        {
            var response =
                await SendAsync(
                        new RpcRequestMessage(request.Id, request.Method, request.RawParameters), route)
                    .ConfigureAwait(false);
            HandleRpcError(response, request.Method);
        }

        public abstract Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string route = null);
        protected abstract Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests);
        public virtual async Task SendRequestAsync(string method, string route = null, params object[] paramList)
        {
            var request = new RpcRequestMessage(Guid.NewGuid().ToString(), method, paramList);
            var response = await SendAsync(request, route).ConfigureAwait(false);
            HandleRpcError(response, method);
        }
    }
}
#endif