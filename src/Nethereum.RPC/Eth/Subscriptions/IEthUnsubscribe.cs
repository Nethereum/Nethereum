using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Eth.Subscriptions
{
    public interface IEthUnsubscribe
    {
        RpcRequest BuildRequest(string subscriptionHash, object id = null);
        Task<bool> SendRequestAsync(string subscriptionHash, object id = null);
    }
}