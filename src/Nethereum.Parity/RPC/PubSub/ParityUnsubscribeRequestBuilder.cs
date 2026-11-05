using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.Streaming;

namespace Nethereum.Parity.RPC.PubSub
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ClientExtensions, "parity-client", "ParityUnsubscribeRequestBuilder")]
    public class ParityUnsubscribeRequestBuilder : RpcRequestBuilder, IUnsubscribeSubscriptionRpcRequestBuilder
    {
        public ParityUnsubscribeRequestBuilder() : base(ApiMethods.parity_unsubscribe.ToString())
        {
        }

        public RpcRequest BuildRequest(string subscriptionHash, object id = null)
        {
            if (id == null) id = Guid.NewGuid().ToString();
            return base.BuildRequest(id, subscriptionHash.EnsureHexPrefix());
        }
    }
}