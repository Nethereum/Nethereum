using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.Subscriptions
{
    public class EthNewBlockHeadersSubscription : RpcStreamingRequestResponseHandler<Block>
    {
        public EthNewBlockHeadersSubscription(IStreamingClient client) : base(client, ApiMethods.eth_subscribe.ToString())
        {
        }

        public Task SendRequestAsync(object id)
        {
            return base.SendRequestAsync(id, "newHeads");
        }

        public RpcRequest BuildRequest(object id)
        {
            return base.BuildRequest(id, "newHeads");
        }
    }
}
