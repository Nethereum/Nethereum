using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.Subscriptions;
using System;
using System.Threading.Tasks;

namespace Nethereum.JsonRpc.WebSocketStreamingClient
{
    public abstract class RpcStreamingSubscriptionHandler<TSubscriptionDataResponse>: IRpcStreamingSubscriptionHandler
    {
        protected IStreamingClient StreamingClient { get; set; }
        protected EthUnsubscribe EthUnsubscribe { get; set; }
        protected object SubscribeRequestId { get; set; }
        protected string UnsubscribeRequestId { get; set; }
        private readonly object _lockingObject = new object();

        protected RpcStreamingSubscriptionHandler(IStreamingClient streamingClient)
        {
            StreamingClient = streamingClient;
            EthUnsubscribe = new EthUnsubscribe(null);
        }

        protected async Task SubscribeAsync(RpcRequest rpcRequest)
        {
            if (SubscriptionState != SubscriptionState.Idle)
                throw new Exception("Invalid state to start subscribtion, current state: " + SubscriptionState.ToString());

            SubscribeRequestId = rpcRequest.Id;
            await StreamingClient.SendRequestAsync(rpcRequest, this);
            this.SubscriptionState = SubscriptionState.Subscribing;
        }

        public async Task UnsubscribeAsync()
        {
            if (SubscriptionState != SubscriptionState.Subscribed && SubscriptionState != SubscriptionState.Unsubscribed) //allow retrying? what happens when it returns false the unsubscription
                throw new Exception("Invalid state to unsubscribe, current state: " + SubscriptionState.ToString());

            UnsubscribeRequestId = Guid.NewGuid().ToString();
            var request = EthUnsubscribe.BuildRequest(SubscriptionId, UnsubscribeRequestId);
            await StreamingClient.SendRequestAsync(request, this);
            this.SubscriptionState = SubscriptionState.Unsubscribing;
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
