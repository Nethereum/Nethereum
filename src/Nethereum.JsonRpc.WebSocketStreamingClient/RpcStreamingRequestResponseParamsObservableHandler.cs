using Nethereum.JsonRpc.Client;

namespace Nethereum.JsonRpc.WebSocketStreamingClient
{
    public abstract class RpcStreamingRequestResponseParamsObservableHandler<TResponse, TRpcRequestResponseHandler> : RpcStreamingRequestResponseObservableHandler<TResponse>
        where TRpcRequestResponseHandler : RpcRequestResponseHandler<TResponse>
    {
        protected TRpcRequestResponseHandler RpcRequestResponseHandler { get; }

        protected RpcStreamingRequestResponseParamsObservableHandler(IStreamingClient streamingClient, TRpcRequestResponseHandler rpcRequestResponseHandler):base(streamingClient)
        {
            RpcRequestResponseHandler = rpcRequestResponseHandler;
        }
    }
}
