using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.Subscriptions
{
    public class EthNewSyncingSubscription : RpcStreamingRequestResponseHandler<HexBigInteger>
    {
        public EthNewSyncingSubscription(IStreamingClient client) : base(client, ApiMethods.eth_newFilter.ToString())
        {
        }

        public Task SendRequestAsync(object id = null)
        {
            return base.SendRequestAsync(id);
        }

        public RpcRequest BuildRequest(object id = null)
        {
            return base.BuildRequest(id);
        }
    }
}
