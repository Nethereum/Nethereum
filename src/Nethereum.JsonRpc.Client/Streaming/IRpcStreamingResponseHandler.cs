using System;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.JsonRpc.Client.Streaming
{
    public interface IRpcStreamingResponseHandler
    {
        void HandleResponse(RpcStreamingResponseMessage rpcStreamingResponse);
        void HandleClientError(Exception ex);
        void HandleClientDisconnection();
    }
}