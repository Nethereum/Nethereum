using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.Streaming;

namespace Nethereum.JsonRpc.WebSocketStreamingClient
{
    public abstract class RpcStreamingResponseParamsObservableHandler<TResponse, TRpcRequestResponseHandler> : RpcStreamingResponseObservableHandler<TResponse>
        where TRpcRequestResponseHandler : RpcRequestResponseHandler<TResponse>
    {
        protected TRpcRequestResponseHandler RpcRequestResponseHandler { get; }

        protected RpcStreamingResponseParamsObservableHandler(IStreamingClient streamingClient, TRpcRequestResponseHandler rpcRequestResponseHandler):base(streamingClient)
        {
            RpcRequestResponseHandler = rpcRequestResponseHandler;
        }
    }
}
