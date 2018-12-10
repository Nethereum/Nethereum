using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Subscriptions;
using System;
using System.Threading.Tasks;

namespace Nethereum.JsonRpc.WebSocketStreamingClient
{
    public class EthNewBlockHeadersObservableSubscription : RpcStreamingSubscriptionObservableHandler<Block>
    {
        EthNewBlockHeadersSubscription _ethNewBlockHeadersSubscription;
        public EthNewBlockHeadersObservableSubscription(IStreamingClient streamingClient) : base(streamingClient)
        {
            _ethNewBlockHeadersSubscription = new EthNewBlockHeadersSubscription(null);
        }

        public Task SubscribeAsync(object id = null)
        {
            if (id == null) id = Guid.NewGuid().ToString();
            var request = _ethNewBlockHeadersSubscription.BuildRequest(id);
            return base.SubscribeAsync(request);
        }
    }


    public class EthLogsObservableSubscription : RpcStreamingSubscriptionObservableHandler<FilterLog>
    {
        EthLogsSubscription _ethLogsSubscription;
        public EthLogsObservableSubscription(IStreamingClient streamingClient) : base(streamingClient)
        {
            _ethLogsSubscription = new EthLogsSubscription(null);
        }

        public Task SubscribeAsync(NewFilterInput newFilterInput, object id = null)
        {
            if (id == null) id = Guid.NewGuid().ToString();
            var request = _ethLogsSubscription.BuildRequest(newFilterInput, id);
            return base.SubscribeAsync(request);
        }

        public Task SubscribeAsync(object id = null)
        {
            if (id == null) id = Guid.NewGuid().ToString();
            var request = _ethLogsSubscription.BuildRequest(new NewFilterInput(), id);
            return base.SubscribeAsync(request);
        }
    }
}
