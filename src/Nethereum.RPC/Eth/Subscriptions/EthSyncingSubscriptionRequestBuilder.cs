using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.JsonRpc.WebSocketStreamingClient;

namespace Nethereum.RPC.Eth.Subscriptions
{
    public class EthSyncingSubscriptionRequestBuilder : RpcRequestBuilder
    {
        public EthSyncingSubscriptionRequestBuilder() : base(ApiMethods.eth_subscribe.ToString())
        {
        }

        public override RpcRequest BuildRequest(object id = null)
        {
            if (id == null) id = Guid.NewGuid().ToString();
            return base.BuildRequest(id, "syncing");
        }
    }
}
