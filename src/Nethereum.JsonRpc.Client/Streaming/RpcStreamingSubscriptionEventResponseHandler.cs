using System;

namespace Nethereum.JsonRpc.Client.Streaming
{
    public class RpcStreamingSubscriptionEventResponseHandler<TSubscriptionDataResponse> : RpcStreamingSubscriptionHandler<TSubscriptionDataResponse>
    {
        public event EventHandler<StreamingEventArgs<string>> SubscribeResponse;
        public event EventHandler<StreamingEventArgs<bool>> UnsubscribeResponse;
        public event EventHandler<StreamingEventArgs<TSubscriptionDataResponse>> SubscriptionDataResponse;

        protected RpcStreamingSubscriptionEventResponseHandler(IStreamingClient streamingClient, IUnsubscribeSubscriptionRpcRequestBuilder unsubscribeSubscriptionRpcRequestBuilder) : base(streamingClient, unsubscribeSubscriptionRpcRequestBuilder)
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
}
