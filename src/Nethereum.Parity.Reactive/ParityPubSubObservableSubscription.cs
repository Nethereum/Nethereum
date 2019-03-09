using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Subscriptions;
using Nethereum.RPC.Reactive.RpcStreaming;
using Newtonsoft.Json.Linq;
using Nethereum.Parity.RPC.PubSub;

namespace Nethereum.Parity.Reactive
{
    public class ParityPubSubObservableSubscription<TResponse> : RpcStreamingSubscriptionObservableHandler<TResponse>
    {
        private ParitySubscribeRequestBuilder _paritySubscribeRequestBuilder;

        public ParityPubSubObservableSubscription(IStreamingClient client) : base(client, new EthUnsubscribeRequestBuilder())
        {
            _paritySubscribeRequestBuilder = new ParitySubscribeRequestBuilder();
        }

        public Task SubscribeAsync(RpcRequest originalRequestToSubscribe, object id = null)
        {
            return base.SubscribeAsync(BuildRequest(originalRequestToSubscribe, id));
        }

        public RpcRequest BuildRequest(RpcRequest originalRequestToSubscribe, object id = null)
        {
            return _paritySubscribeRequestBuilder.BuildRequest(originalRequestToSubscribe, id);
        }
    }
}
