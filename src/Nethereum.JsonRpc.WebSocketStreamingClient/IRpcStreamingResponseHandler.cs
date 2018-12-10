using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.JsonRpc.WebSocketStreamingClient
{
    public interface IRpcStreamingResponseHandler
    {
        void HandleResponse(RpcStreamingResponseMessage rpcStreamingResponse);
    }
}