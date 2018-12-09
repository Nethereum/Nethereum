using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Eth.Mining
{
    public interface IEthSubmitHashrate
    {
        RpcRequest BuildRequest(string hashRate, string clientId, object id = null);
        Task<bool> SendRequestAsync(string hashRate, string clientId, object id = null);
    }
}