using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.Subscriptions;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Nethereum.JsonRpc.WebSocketStreamingClient
{

    public class RpcStreamingSubscriptionObservableHandler<TSubscriptionDataResponse> : IRpcStreamingSubscriptionHandler
    {
        protected IStreamingClient StreamingClient { get; set; }

        protected Subject<string> SubscribeResponseSubject { get; set; }
        protected Subject<bool> UnsubscribeResponseSubject { get; set; }
        protected Subject<TSubscriptionDataResponse> SubscriptionDataResponseSubject { get; set; }
        protected EthUnsubscribe EthUnsubscribe { get; set; }

        protected object SubscribeRequestId { get; set; }
        protected string UnsubscribeRequestId { get; set; }

        protected RpcStreamingSubscriptionObservableHandler(IStreamingClient streamingClient)
        {
            StreamingClient = streamingClient;
            SubscribeResponseSubject = new Subject<string>();
            UnsubscribeResponseSubject = new Subject<bool>();
            EthUnsubscribe = new EthUnsubscribe(null);
            SubscriptionDataResponseSubject = new Subject<TSubscriptionDataResponse>();
        }

        protected async Task SubscribeAsync(RpcRequest rpcRequest)
        {
            if (SubscriptionState != SubscriptionState.Idle)
                throw new Exception("Invalid state to start subscribtion, current state: " + SubscriptionState.ToString());

            SubscribeRequestId = rpcRequest.Id;
            await StreamingClient.SendRequestAsync(rpcRequest, this);
            this.SubscriptionState = SubscriptionState.Subscribing;
        }

        public IObservable<string> GetSubscribeResponseAsObservable()
        {
            return SubscribeResponseSubject.AsObservable();
        }

        public IObservable<TSubscriptionDataResponse> GetSubscribionDataResponsesAsObservable()
        {
            return SubscriptionDataResponseSubject.AsObservable();
        }

        public IObservable<bool> GetUnsubscribeResponsesAsObservable()
        {
            return UnsubscribeResponseSubject.AsObservable();
        }

        public async Task UnsubscribeAsync()
        {
            if (SubscriptionState != SubscriptionState.Subscribed && SubscriptionState != SubscriptionState.Unsubscribed ) //allow retrying? what happens when it returns false the unsubscription
                    throw new Exception("Invalid state to unsubscribe, current state: " + SubscriptionState.ToString());

            UnsubscribeRequestId = Guid.NewGuid().ToString();
            var request = EthUnsubscribe.BuildRequest(SubscriptionId, UnsubscribeRequestId);
            await StreamingClient.SendRequestAsync(request, this);
            this.SubscriptionState = SubscriptionState.Unsubscribing;
        }

        public void HandleResponse(RpcStreamingResponseMessage rpcStreamingResponse)
        {
            if (rpcStreamingResponse.Method == null)
            {
                if (rpcStreamingResponse.Id.ToString() == SubscribeRequestId.ToString())
                {
                    if (rpcStreamingResponse.HasError)
                    {
                        if (SubscribeResponseSubject != null)
                        {
                            SubscribeResponseSubject.OnError(GetException(rpcStreamingResponse));
                        }
                    }
                    else
                    {
                        var result = rpcStreamingResponse.GetStreamingResult<string>();
                        this.SubscriptionState = SubscriptionState.Subscribed;
                        this.SubscriptionId = result;
                        StreamingClient.AddSubscription(SubscriptionId, this);
                        SubscribeResponseSubject.OnNext(result);
                        SubscribeResponseSubject.OnCompleted();
                    }
                }

                if(!string.IsNullOrEmpty(UnsubscribeRequestId) && rpcStreamingResponse.Id.ToString() == UnsubscribeRequestId)
                {
                    if (rpcStreamingResponse.HasError)
                    {
                        UnsubscribeResponseSubject.OnError(GetException(rpcStreamingResponse));
                    }
                    else
                    {
                        var result = rpcStreamingResponse.GetStreamingResult<bool>();
                        if (result)
                        {
                            this.SubscriptionState = SubscriptionState.Unsubscribed;
                            StreamingClient.RemoveSubscription(SubscriptionId);
                        }
                        UnsubscribeResponseSubject.OnNext(result);
                        UnsubscribeResponseSubject.OnCompleted();
                    }
                }
            }
            else
            {
                if (rpcStreamingResponse.HasError)
                {
                    SubscriptionDataResponseSubject.OnError(GetException(rpcStreamingResponse));
                }
                else
                {
                    var result = rpcStreamingResponse.GetStreamingResult<TSubscriptionDataResponse>();

                    SubscriptionDataResponseSubject.OnNext(result);

                }
            }
        }

        protected RpcResponseException GetException(RpcStreamingResponseMessage rpcStreamingResponse)
        {
            return new RpcResponseException(new Client.RpcError(rpcStreamingResponse.Error.Code, rpcStreamingResponse.Error.Message,
                       rpcStreamingResponse.Error.Data));
        }

        public string SubscriptionId { get; protected set; }

        public SubscriptionState SubscriptionState { get; protected set; } = SubscriptionState.Idle;
        
    }
}
