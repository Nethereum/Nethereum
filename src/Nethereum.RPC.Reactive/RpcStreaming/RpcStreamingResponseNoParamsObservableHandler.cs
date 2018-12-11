using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.Streaming;

namespace Nethereum.RPC.Reactive.RpcStreaming
{
    public abstract class RpcStreamingResponseNoParamsObservableHandler<TResponse, TRpcRequestResponseHandler> : RpcStreamingResponseObservableHandler<TResponse>
       where TRpcRequestResponseHandler : RpcRequestResponseHandlerNoParam<TResponse>
    {
        protected TRpcRequestResponseHandler RpcRequestResponseHandler { get; }

        protected RpcStreamingResponseNoParamsObservableHandler(IStreamingClient streamingClient, TRpcRequestResponseHandler rpcRequestResponseHandler) : base(streamingClient)
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
