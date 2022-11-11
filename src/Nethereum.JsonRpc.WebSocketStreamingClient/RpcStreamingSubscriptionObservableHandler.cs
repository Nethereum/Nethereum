using Nethereum.JsonRpc.Client;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using Nethereum.JsonRpc.Client.Streaming;

namespace Nethereum.JsonRpc.WebSocketStreamingClient
{

    public class RpcStreamingSubscriptionObservableHandler<TSubscriptionDataResponse> : RpcStreamingSubscriptionHandler<TSubscriptionDataResponse>
    {
        protected Subject<string> SubscribeResponseSubject { get; set; }
        protected Subject<bool> UnsubscribeResponseSubject { get; set; }
        protected Subject<TSubscriptionDataResponse> SubscriptionDataResponseSubject { get; set; }
        
        protected RpcStreamingSubscriptionObservableHandler(IStreamingClient streamingClient, IUnsubscribeSubscriptionRpcRequestBuilder unsubscribeSubscriptionRpcRequestBuilder):base(streamingClient, unsubscribeSubscriptionRpcRequestBuilder)
        {
            SubscribeResponseSubject = new Subject<string>();
            UnsubscribeResponseSubject = new Subject<bool>();
            SubscriptionDataResponseSubject = new Subject<TSubscriptionDataResponse>();
        }

        public IObservable<string> GetSubscribeResponseAsObservable()
        {
            return SubscribeResponseSubject.AsObservable();
        }

        public IObservable<TSubscriptionDataResponse> GetSubscriptionDataResponsesAsObservable()
        {
            return SubscriptionDataResponseSubject.AsObservable();
        }

        public IObservable<bool> GetUnsubscribeResponseAsObservable()
        {
            return UnsubscribeResponseSubject.AsObservable();
        }

        protected override void HandleSubscribeResponseError(RpcResponseException exception)
        {
            SubscribeResponseSubject.OnError(exception);
        }

        protected override void HandleUnsubscribeResponseError(RpcResponseException exception)
        {
            UnsubscribeResponseSubject.OnError(exception);
        }

        protected override void HandleDataResponseError(RpcResponseException exception)
        {
            SubscriptionDataResponseSubject.OnError(exception);
        }

        protected override void HandleSubscribeResponse(string subscriptionId)
        {
            SubscribeResponseSubject.OnNext(subscriptionId);
            SubscribeResponseSubject.OnCompleted();
        }

        protected override void HandleUnsubscribeResponse(bool success)
        {
            UnsubscribeResponseSubject.OnNext(success);
            UnsubscribeResponseSubject.OnCompleted();
            SubscriptionDataResponseSubject.OnCompleted();
        }

        protected override void HandleDataResponse(TSubscriptionDataResponse subscriptionDataResponse)
        {
            SubscriptionDataResponseSubject.OnNext(subscriptionDataResponse);
        }
    }
}
