using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.JsonRpc.Client
{
    public class RpcRequestResponseBatchItem<TRequestHandler, TResponse> : IRpcRequestResponseBatchItem where TRequestHandler : IRpcRequestHandler<TResponse>
    {
        public RpcRequestResponseBatchItem(TRequestHandler requestHandler, RpcRequest rpcRequest)
        {
            RequestHandler = requestHandler;
            RpcRequestMessage = new RpcRequestMessage(rpcRequest.Id,
                                             rpcRequest.Method,
                                             rpcRequest.RawParameters);
        }

        public TRequestHandler RequestHandler { get; private set; }
        public RpcRequestMessage RpcRequestMessage { get; private set; }
        public TResponse Response { get; private set; }
        public object RawResponse { get; private set; }
        public bool HasError { get; private set; }
        public RpcError RpcError { get; private set; }
        public void DecodeResponse(RpcResponseMessage rpcResponse)
        {
            if (rpcResponse.HasError)
            {
                this.HasError = true;
                this.RpcError = new RpcError(rpcResponse.Error.Code, rpcResponse.Error.Message,
                    rpcResponse.Error.Data);
            }
            try
            {
                Response = RequestHandler.DecodeResponse(rpcResponse);
                RawResponse = Response;
            }
            catch
            {
                this.HasError = true;
                this.RpcError = new RpcError(-1, "Invalid format exception");
            }
        }
    }
}