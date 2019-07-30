using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Pantheon.RPC.Permissioning
{
    public interface IPermRemoveNodesFromWhitelist
    {
        Task<string> SendRequestAsync(string[] addresses, object id = null);
        RpcRequest BuildRequest(string[] addresses, object id = null);
    }
}