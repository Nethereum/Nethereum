using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Besu.RPC.Admin
{
    public interface IAdminAddPeer
    {
        RpcRequest BuildRequest(string enodeUrl, object id = null);
        Task<bool> SendRequestAsync(string enodeUrl, object id = null);
    }
}