#if !DOTNET35
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.JsonRpc.Client
{
    public abstract class StreamingClientBase : IStreamingClient
    {

        public static TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(20.0);

        public RequestInterceptor OverridingRequestInterceptor { get; set; }

        public event EventHandler<RpcStreamingResponseMessageEventArgs> StreamingMessageReceived;

        public event EventHandler<RpcResponseMessageEventArgs> MessageReceived;

        protected virtual void OnMessageRecieved(object sender, RpcResponseMessageEventArgs args)
        {
            var handler = MessageReceived;
            if (handler != null)
            {
                handler(this, args);
            }
        }

        protected virtual void OnStreamingMessageRecieved(object sender, RpcStreamingResponseMessageEventArgs args)
        {
            var handler = StreamingMessageReceived;
            if (handler != null)
            {
                handler(this, args);
            }
        }

        protected void HandleRpcError(RpcResponseMessage response)
        {
            if (response.HasError)
                throw new RpcResponseException(new RpcError(response.Error.Code, response.Error.Message,
                    response.Error.Data));
        }

        private async Task SendInnerRequestAsync(RpcRequestMessage reqMsg,
                                                       string route = null)
        {
            await SendAsync(reqMsg, route).ConfigureAwait(false);
            //HandleRpcError(response);
            //try
            //{
            //    return response.GetResult<T>();
            //}
            //catch (FormatException formatException)
            //{
            //    throw new RpcResponseFormatException("Invalid format found in RPC response", formatException);
            //}
        }

        protected virtual async Task SendInnerRequestAsync(RpcRequest request, string route = null)
        {
            var reqMsg = new RpcRequestMessage(request.Id,
                                               request.Method,
                                               request.RawParameters);
            await SendInnerRequestAsync(reqMsg, route).ConfigureAwait(false);
        }

        protected virtual async Task SendInnerRequestAsync(string method, string route = null,
            params object[] paramList)
        {
            var request = new RpcRequestMessage(Guid.NewGuid().ToString(), method, paramList);
            await SendInnerRequestAsync(request, route);
        }

        public virtual async Task SendRequestAsync(RpcRequest request, string route = null)
        {
            await SendAsync(
                    new RpcRequestMessage(request.Id, request.Method, request.RawParameters), route)
                .ConfigureAwait(false);
            //HandleRpcError(response);
        }

        protected abstract Task SendAsync(RpcRequestMessage rpcRequestMessage, string route = null);

        public virtual async Task SendRequestAsync(string method, string route = null, params object[] paramList)
        {
            var request = new RpcRequestMessage(Guid.NewGuid().ToString(), method, paramList);
            await SendAsync(request, route).ConfigureAwait(false);
            //HandleRpcError(response);
        }
    }
}
#endif