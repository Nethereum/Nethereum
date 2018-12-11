using Nethereum.JsonRpc.Client;
using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Nethereum.JsonRpc.Client.Streaming;

namespace Nethereum.JsonRpc.WebSocketStreamingClient
{
    public class RpcStreamingResponseObservableHandler<TResponse> : RpcStreamingRequestResponseHandler<TResponse>
    {
        protected Subject<TResponse> ResponseSubject { get; set; }

        protected RpcStreamingResponseObservableHandler(IStreamingClient streamingClient):base(streamingClient)
        { 
            ResponseSubject = new Subject<TResponse>();
        }

        public IObservable<TResponse> GetResponseAsObservable()
        {
            return ResponseSubject.AsObservable();
        }

        protected override void HandleResponse(TResponse subscriptionDataResponse)
        {
            ResponseSubject.OnNext(subscriptionDataResponse);
            ResponseSubject.OnCompleted();
        }

        protected override void HandleResponseError(RpcResponseException exception)
        {
            ResponseSubject.OnError(exception);
        }
    }
}
