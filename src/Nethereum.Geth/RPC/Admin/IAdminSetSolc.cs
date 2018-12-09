using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Geth.RPC.Admin
{
    public interface IAdminSetSolc
    {
        RpcRequest BuildRequest(string path, object id = null);
        Task<string> SendRequestAsync(string path, object id = null);
    }
}