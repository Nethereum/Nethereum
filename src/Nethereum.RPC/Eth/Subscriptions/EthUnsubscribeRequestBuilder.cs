using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.Streaming;

namespace Nethereum.RPC.Eth.Subscriptions
{
    public class EthUnsubscribeRequestBuilder : RpcRequestBuilder, IUnsubscribeSubscriptionRpcRequestBuilder
    {
        public EthUnsubscribeRequestBuilder() : base(ApiMethods.eth_unsubscribe.ToString())
        {
        }

        public RpcRequest BuildRequest(string subscriptionHash, object id = null)
        {
            if (id == null) id = Guid.NewGuid().ToString();
            return base.BuildRequest(id, subscriptionHash.EnsureHexPrefix());
        }
    }
}