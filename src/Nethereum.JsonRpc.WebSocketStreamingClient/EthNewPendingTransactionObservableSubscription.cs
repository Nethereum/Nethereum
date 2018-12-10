using Nethereum.RPC.Eth.Subscriptions;
using System;
using System.Threading.Tasks;

namespace Nethereum.JsonRpc.WebSocketStreamingClient
{
    public class EthNewPendingTransactionObservableSubscription : RpcStreamingSubscriptionObservableHandler<string>
    {
        EthNewPendingTransactionSubscription _ethNewPendingTransactionSubscription;
        public EthNewPendingTransactionObservableSubscription(IStreamingClient streamingClient) : base(streamingClient)
        {
            _ethNewPendingTransactionSubscription = new EthNewPendingTransactionSubscription(null);
        }

        public Task SubscribeAsync(object id = null)
        {
            if (id == null) id = Guid.NewGuid().ToString();
            var request = _ethNewPendingTransactionSubscription.BuildRequest(id);
            return base.SubscribeAsync(request);
        }
    }
}
