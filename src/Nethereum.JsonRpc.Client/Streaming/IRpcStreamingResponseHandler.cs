using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.JsonRpc.Client.Streaming
{
    public interface IRpcStreamingResponseHandler
    {
        void HandleResponse(RpcStreamingResponseMessage rpcStreamingResponse);
    }
}