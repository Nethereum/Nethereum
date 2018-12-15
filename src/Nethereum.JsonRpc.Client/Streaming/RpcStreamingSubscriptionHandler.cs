using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;


namespace Nethereum.JsonRpc.Client.Streaming
{
    public abstract class RpcStreamingSubscriptionHandler<TSubscriptionDataResponse>: IRpcStreamingSubscriptionHandler
    {
        protected IStreamingClient StreamingClient { get; set; }
        protected IUnsubscribeSubscriptionRpcRequestBuilder UnsubscribeSubscriptionRpcRequestBuilder { get; set; }
        protected object SubscribeRequestId { get; set; }
        protected string UnsubscribeRequestId { get; set; }
        private readonly object _lockingObject = new object();

        protected RpcStreamingSubscriptionHandler(IStreamingClient streamingClient, IUnsubscribeSubscriptionRpcRequestBuilder unsubscribeSubscriptionRpcRequestBuilder)
        {
            StreamingClient = streamingClient;
            UnsubscribeSubscriptionRpcRequestBuilder = unsubscribeSubscriptionRpcRequestBuilder;
        }

        protected Task SubscribeAsync(RpcRequest rpcRequest)
        {
            if (SubscriptionState != SubscriptionState.Idle)
                throw new Exception("Invalid state to start subscription, current state: " + SubscriptionState.ToString());

            SubscribeRequestId = rpcRequest.Id;

            this.SubscriptionState = SubscriptionState.Subscribing;

            return StreamingClient.SendRequestAsync(rpcRequest, this);
        }

        public Task UnsubscribeAsync()
        {

            if (SubscriptionState != SubscriptionState.Subscribed && SubscriptionState != SubscriptionState.Unsubscribed) //allow retrying? what happens when it returns false the unsubscription
                throw new Exception("Invalid state to unsubscribe, current state: " + SubscriptionState.ToString());

            UnsubscribeRequestId = Guid.NewGuid().ToString();
            var request = UnsubscribeSubscriptionRpcRequestBuilder.BuildRequest(SubscriptionId, UnsubscribeRequestId);

            this.SubscriptionState = SubscriptionState.Unsubscribing;

            return StreamingClient.SendRequestAsync(request, this);

        }

        protected RpcResponseException GetException(RpcStreamingResponseMessage rpcStreamingResponse)
        {
            return new RpcResponseException(new Client.RpcError(rpcStreamingResponse.Error.Code, rpcStreamingResponse.Error.Message,
                       rpcStreamingResponse.Error.Data));
        }

        public string SubscriptionId { get; protected set; }

        public SubscriptionState SubscriptionState { get; protected set; } = SubscriptionState.Idle;

        protected abstract void HandleSubscribeResponseError(RpcResponseException exception);
        protected abstract void HandleUnsubscribeResponseError(RpcResponseException exception);
        protected abstract void HandleDataResponseError(RpcResponseException exception);

        protected abstract void HandleSubscribeResponse(string subscriptionId);
        protected abstract void HandleUnsubscribeResponse(bool success);
        protected abstract void HandleDataResponse(TSubscriptionDataResponse subscriptionDataResponse);


        public void HandleResponse(RpcStreamingResponseMessage rpcStreamingResponse)
        {
            lock (_lockingObject)
            {
                if (rpcStreamingResponse.Method == null)
                {
                    if (rpcStreamingResponse.Id.ToString() == SubscribeRequestId.ToString())
                    {
                        if (rpcStreamingResponse.HasError)
                        {
                            HandleSubscribeResponseError(GetException(rpcStreamingResponse));

                        }
                        else
                        {
                            var result = rpcStreamingResponse.GetStreamingResult<string>();
                            this.SubscriptionState = SubscriptionState.Subscribed;
                            this.SubscriptionId = result;
                            StreamingClient.AddSubscription(SubscriptionId, this);
                            HandleSubscribeResponse(result);
                        }
                    }

                    if (!string.IsNullOrEmpty(UnsubscribeRequestId) && rpcStreamingResponse.Id.ToString() == UnsubscribeRequestId)
                    {
                        if (rpcStreamingResponse.HasError)
                        {
                            HandleUnsubscribeResponseError(GetException(rpcStreamingResponse));

                        }
                        else
                        {
                            var result = rpcStreamingResponse.GetStreamingResult<bool>();
                            if (result)
                            {
                                this.SubscriptionState = SubscriptionState.Unsubscribed;
                                StreamingClient.RemoveSubscription(SubscriptionId);
                            }
                            HandleUnsubscribeResponse(result);
                        }
                    }
                }
                else
                {
                    if (rpcStreamingResponse.HasError)
                    {
                        HandleDataResponseError(GetException(rpcStreamingResponse));
                    }
                    else
                    {
                        var result = rpcStreamingResponse.GetStreamingResult<TSubscriptionDataResponse>();
                        HandleDataResponse(result);
                    }
                }
            }
        }
    }

}
