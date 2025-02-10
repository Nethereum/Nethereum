using Nethereum.JsonRpc.Client;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.AccountAbstraction
{
    public interface IEthSupportedEntryPoints
    {
        RpcRequest BuildRequest(object id = null);
        Task<string[]> SendRequestAsync(object id = null);
    }
}