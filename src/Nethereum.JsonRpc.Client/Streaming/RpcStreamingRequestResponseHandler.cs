using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.JsonRpc.Client.Streaming;
using RpcError = Nethereum.JsonRpc.Client.RpcError;

namespace Nethereum.JsonRpc.WebSocketStreamingClient
{
    public abstract class RpcStreamingRequestResponseHandler<TResponse> : IRpcStreamingResponseHandler
    {
        protected IStreamingClient StreamingClient { get; }
        protected RpcStreamingRequestResponseHandler(IStreamingClient streamingClient)
        {
            StreamingClient = streamingClient;
        }

        protected abstract void HandleResponse(TResponse subscriptionDataResponse);
        protected abstract void HandleResponseError(RpcResponseException exception);

        protected Task SendRequestAsync(RpcRequest request)
        {
            if (request.Id == null) request.Id = Guid.NewGuid().ToString();
            return StreamingClient.SendRequestAsync(request, this);
        }

        public void HandleResponse(RpcStreamingResponseMessage rpcStreamingResponse)
        {
            if (rpcStreamingResponse.HasError)
            {
                HandleResponseError(
                    new RpcResponseException(new RpcError(rpcStreamingResponse.Error.Code, rpcStreamingResponse.Error.Message,
                        rpcStreamingResponse.Error.Data)));
            }
            else
            {
                var result = rpcStreamingResponse.GetStreamingResult<TResponse>();
                HandleResponse(result);
            }
        }

        public void HandleClientError(Exception ex)
        {
            HandleResponseError(new RpcResponseException(new RpcError(-1, "Client connection error")));
        }

        public void HandleClientDisconnection()
        {
            HandleResponseError(new RpcResponseException(new RpcError(-1, "Client disconnected")));
        }
    }
}