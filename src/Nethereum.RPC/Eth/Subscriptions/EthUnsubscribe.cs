using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.Subscriptions
{
    public class EthUnsubscribe : RpcRequestResponseHandler<bool>, IEthUnsubscribe
    {
        public EthUnsubscribe(IClient client) : base(client, ApiMethods.eth_unsubscribe.ToString())
        {
        }

        public Task<bool> SendRequestAsync(string subscriptionHash, object id = null)
        {
            return base.SendRequestAsync(id, subscriptionHash.EnsureHexPrefix());
        }

        public RpcRequest BuildRequest(string subscriptionHash, object id = null)
        {
            return base.BuildRequest(id, subscriptionHash.EnsureHexPrefix());
        }
    }
}
