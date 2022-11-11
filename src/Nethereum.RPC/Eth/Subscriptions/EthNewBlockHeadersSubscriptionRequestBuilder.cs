using System;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Eth.Subscriptions
{
    public class EthNewBlockHeadersSubscriptionRequestBuilder : RpcRequestBuilder
    {
        public EthNewBlockHeadersSubscriptionRequestBuilder() : base(ApiMethods.eth_subscribe.ToString())
        {
        }

        public override RpcRequest BuildRequest(object id = null)
        {
            if (id == null) id = Guid.NewGuid().ToString();
            return base.BuildRequest(id, "newHeads");
        }
    }
}