using Nethereum.RPC.Eth.Subscriptions;
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.Streaming;

namespace Nethereum.JsonRpc.WebSocketStreamingClient
{

    public class EthNewPendingTransactionObservableSubscription : RpcStreamingSubscriptionObservableHandler<string>
    {
        private EthNewPendingTransactionSubscriptionRequestBuilder _builder;

        public EthNewPendingTransactionObservableSubscription(IStreamingClient client) : base(client, new EthUnsubscribeRequestBuilder())
        {
            _builder = new EthNewPendingTransactionSubscriptionRequestBuilder();
        }

        public Task SubscribeAsync(object id = null)
        {
            return base.SubscribeAsync(BuildRequest(id));
        }

        public RpcRequest BuildRequest(object id)
        {
            return _builder.BuildRequest(id);
        }
    }
}
