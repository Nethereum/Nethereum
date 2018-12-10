using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using RpcError = Nethereum.JsonRpc.Client.RpcError;

namespace Nethereum.JsonRpc.WebSocketStreamingClient
{
    public abstract class RpcStreamingRequestResponseObservableHandler<TResponse> : IRpcStreamingResponseHandler 
    {
        protected IStreamingClient StreamingClient { get; }

        protected Subject<TResponse> ResponseSubject { get; set; }

        protected RpcStreamingRequestResponseObservableHandler(IStreamingClient streamingClient)
        { 
            ResponseSubject = new Subject<TResponse>();
            StreamingClient = streamingClient;
        }

        public IObservable<TResponse> GetResponseAsObservable()
        {
            return ResponseSubject.AsObservable();
        }

        protected Task SendRequestAsync(RpcRequest request)
        {
            if (request.Id == null) request.Id = Guid.NewGuid().ToString();
            return StreamingClient.SendRequestAsync(request, this);
        }
       
        public void HandleResponse(RpcStreamingResponseMessage rpcStreamingResponse)
        {
            if (rpcStreamingResponse.HasError)
            {
                ResponseSubject.OnError(
                    new RpcResponseException(new RpcError(rpcStreamingResponse.Error.Code, rpcStreamingResponse.Error.Message,
                        rpcStreamingResponse.Error.Data)));
            }
            else
            {
                var result = rpcStreamingResponse.GetStreamingResult<TResponse>();
                ResponseSubject.OnNext(result);
            }

            ResponseSubject.OnCompleted();
        }
    }
}
