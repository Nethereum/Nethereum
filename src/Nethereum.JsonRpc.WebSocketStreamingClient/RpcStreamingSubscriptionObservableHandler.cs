using Nethereum.JsonRpc.Client;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;

namespace Nethereum.JsonRpc.WebSocketStreamingClient
{

    public class StreamingEventArgs<TEntity> : EventArgs
    {
        public TEntity Response { get; private set; }
        public RpcResponseException Exception { get; private set; }

        public StreamingEventArgs(TEntity entity, RpcResponseException exception = null)
        {
            Response = entity;
        }

        public StreamingEventArgs(RpcResponseException exception)
        {
            Exception = exception;
        }

    }

    public class RpcStreamingSubscriptionEventResponseHandler<TSubscriptionDataResponse> : RpcStreamingSubscriptionHandler<TSubscriptionDataResponse>
    {
        public event EventHandler<StreamingEventArgs<string>> SubscribeResponse;
        public event EventHandler<StreamingEventArgs<bool>> UnsubscribeResponse;
        public event EventHandler<StreamingEventArgs<TSubscriptionDataResponse>> SubscriptionDataResponse;

        protected RpcStreamingSubscriptionEventResponseHandler(IStreamingClient streamingClient) : base(streamingClient)
        {
     
        }

        protected override void HandleDataResponse(TSubscriptionDataResponse subscriptionDataResponse)
        {
            SubscriptionDataResponse?.Invoke(this, new StreamingEventArgs<TSubscriptionDataResponse>(subscriptionDataResponse));
        }

        protected override void HandleDataResponseError(RpcResponseException exception)
        {
            SubscriptionDataResponse?.Invoke(this, new StreamingEventArgs<TSubscriptionDataResponse>(exception));
        }

        protected override void HandleSubscribeResponse(string subscriptionId)
        {
            SubscribeResponse?.Invoke(this, new StreamingEventArgs<string>(subscriptionId));
        }

        protected override void HandleSubscribeResponseError(RpcResponseException exception)
        {
            SubscribeResponse?.Invoke(this, new StreamingEventArgs<string>(exception));
        }

        protected override void HandleUnsubscribeResponse(bool success)
        {
            UnsubscribeResponse?.Invoke(this, new StreamingEventArgs<bool>(success));
        }

        protected override void HandleUnsubscribeResponseError(RpcResponseException exception)
        {
            UnsubscribeResponse?.Invoke(this, new StreamingEventArgs<bool>(exception));
        }
    }


    public class RpcStreamingSubscriptionObservableHandler<TSubscriptionDataResponse> : RpcStreamingSubscriptionHandler<TSubscriptionDataResponse>
    {
        protected Subject<string> SubscribeResponseSubject { get; set; }
        protected Subject<bool> UnsubscribeResponseSubject { get; set; }
        protected Subject<TSubscriptionDataResponse> SubscriptionDataResponseSubject { get; set; }
        
        protected RpcStreamingSubscriptionObservableHandler(IStreamingClient streamingClient):base(streamingClient)
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
            SubscribeResponseSubject.OnCompleted();
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
