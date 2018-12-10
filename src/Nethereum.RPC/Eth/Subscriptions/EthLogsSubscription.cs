using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.Subscriptions
{
    public class EthLogsSubscription : RpcStreamingRequestResponseHandler<FilterLog>
    {
        public EthLogsSubscription(IStreamingClient client) : base(client, ApiMethods.eth_subscribe.ToString())
        {
        }

        public Task SendRequestAsync(NewFilterInput filterInput, object id)
        {
            return base.SendRequestAsync(id, "logs", filterInput);
        }

        public RpcRequest BuildRequest(NewFilterInput filterInput, object id)
        {
            return base.BuildRequest(id, "logs", filterInput);
        }
    }
}
