using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Subscriptions;
using Nethereum.RPC.Reactive.RpcStreaming;

namespace Nethereum.RPC.Reactive.Eth.Subscriptions
{
    public class EthLogsObservableSubscription : RpcStreamingSubscriptionObservableHandler<FilterLog>
    {
        private EthLogsSubscriptionRequestBuilder _ethLogsSubscriptionRequestBuilder;

        public EthLogsObservableSubscription(IStreamingClient client) : base(client, new EthUnsubscribeRequestBuilder())
        {
            _ethLogsSubscriptionRequestBuilder = new EthLogsSubscriptionRequestBuilder();
        }

        public Task SubscribeAsync(NewFilterInput filterInput, object id = null)
        {
            return base.SubscribeAsync(BuildRequest(filterInput, id));
        }

        public Task SubscribeAsync(object id = null)
        {
            return base.SubscribeAsync(BuildRequest(new NewFilterInput(), id));
        }

        public RpcRequest BuildRequest(NewFilterInput filterInput, object id = null)
        {
            return _ethLogsSubscriptionRequestBuilder.BuildRequest(filterInput, id);
        }
    }
}