using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Geth.RPC.Admin
{
    public interface IAdminImportChain
    {
        RpcRequest BuildRequest(string filePath, object id = null);
        Task<bool> SendRequestAsync(string filePath, object id = null);
    }
}