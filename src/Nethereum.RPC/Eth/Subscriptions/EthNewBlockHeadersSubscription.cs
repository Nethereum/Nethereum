using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.Streaming;

namespace Nethereum.RPC.Eth.Subscriptions
{
    public class EthNewBlockHeadersSubscription : RpcStreamingSubscriptionEventResponseHandler<Block>
    {
        private EthNewBlockHeadersSubscriptionRequestBuilder _ethNewBlockHeadersSubscriptionRequestBuilder;

        public EthNewBlockHeadersSubscription(IStreamingClient client) : base(client, new EthUnsubscribeRequestBuilder())
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
