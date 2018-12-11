using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Subscriptions;
using Nethereum.RPC.Reactive.RpcStreaming;

namespace Nethereum.RPC.Reactive.Eth.Subscriptions
{

    public class EthNewBlockHeadersObservableSubscription : RpcStreamingSubscriptionObservableHandler<Block>
    {
        private EthNewBlockHeadersSubscriptionRequestBuilder _ethNewBlockHeadersSubscriptionRequestBuilder;

        public EthNewBlockHeadersObservableSubscription(IStreamingClient client) : base(client, new EthUnsubscribeRequestBuilder())
        {
            _ethNewBlockHeadersSubscriptionRequestBuilder = new EthNewBlockHeadersSubscriptionRequestBuilder();
        }

        public Task SubscribeAsync(object id = null)
        {
            return base.SubscribeAsync(BuildRequest(id));
        }

        public RpcRequest BuildRequest(object id)
        {
            return _ethNewBlockHeadersSubscriptionRequestBuilder.BuildRequest(id);
        }
    }
}
