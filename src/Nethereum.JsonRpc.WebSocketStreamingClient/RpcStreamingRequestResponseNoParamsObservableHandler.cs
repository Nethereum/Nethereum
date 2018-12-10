using Nethereum.JsonRpc.Client;
using System;
using System.Threading.Tasks;

namespace Nethereum.JsonRpc.WebSocketStreamingClient
{
    public abstract class RpcStreamingRequestResponseNoParamsObservableHandler<TResponse, TRpcRequestResponseHandler> : RpcStreamingRequestResponseObservableHandler<TResponse>
       where TRpcRequestResponseHandler : RpcRequestResponseHandlerNoParam<TResponse>
    {
        protected TRpcRequestResponseHandler RpcRequestResponseHandler { get; }

        protected RpcStreamingRequestResponseNoParamsObservableHandler(IStreamingClient streamingClient, TRpcRequestResponseHandler rpcRequestResponseHandler) : base(streamingClient)
        {
            RpcRequestResponseHandler = rpcRequestResponseHandler;
        }

        public Task SendRequestAsync(object id = null)
        {
            if (id == null) id = Guid.NewGuid().ToString();
            var request = RpcRequestResponseHandler.BuildRequest(id);
            return base.SendRequestAsync(request);
        }
    }
}
