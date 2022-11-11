using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Geth.RPC.Admin
{
    public interface IAdminStartHTTP
    {
        RpcRequest BuildRequest(string host, int port, string cors, string api, string vHosts, object id = null);
        RpcRequest BuildRequest(string host, int port, string cors, string api, object id = null);
        Task<bool> SendRequestAsync(string host, int port, string cors, string api, string vHosts, object id = null);
        Task<bool> SendRequestAsync(string host, int port, string cors, string api, object id = null);
        Task<bool> SendRequestAsync(string host, int port, string cors, object id = null);
        Task<bool> SendRequestAsync(string host, int port, object id = null);
        Task<bool> SendRequestAsync(string host, object id = null);
        Task<bool> SendRequestAsync(object id = null);
    }
}