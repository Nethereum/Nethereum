using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.Subscriptions
{
    public class EthNewPendingTransactionSubscription : RpcStreamingRequestResponseHandler<string>
    {
        public EthNewPendingTransactionSubscription(IStreamingClient client) : base(client, ApiMethods.eth_subscribe.ToString())
        {
        }

        public Task SendRequestAsync(object id)
        {
            return base.SendRequestAsync(id, "newPendingTransactions");
        }

        public RpcRequest BuildRequest(object id)
        {
            return base.BuildRequest(id, "newPendingTransactions");
        }
    }
}
