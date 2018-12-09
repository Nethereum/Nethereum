using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Geth.RPC.Admin
{
    /// <Summary>
    ///     The startRPC administrative method starts an HTTP based JSON RPC API webserver to handle client requests. All the
    ///     parameters are optional:
    ///     host: network interface to open the listener socket on (defaults to "localhost")
    ///     port: network port to open the listener socket on (defaults to 8545)
    ///     cors: cross-origin resource sharing header to use (defaults to "")
    ///     apis: API modules to offer over this interface (defaults to "eth,net,web3")
    ///     The method returns a boolean flag specifying whether the HTTP RPC listener was opened or not. Please note, only one
    ///     HTTP endpoint is allowed to be active at any time.
    /// </Summary>
    public class AdminStartRPC : RpcRequestResponseHandler<bool>, IAdminStartRPC
    {
        public AdminStartRPC(IClient client) : base(client, ApiMethods.admin_startRPC.ToString())
        {
        }

        public RpcRequest BuildRequest(string host, int port, string cors, string api, object id = null)
        {
            return base.BuildRequest(id, host, port, cors, api);
        }

        public Task<bool> SendRequestAsync(string host, int port, string cors, string api, object id = null)
        {
            return base.SendRequestAsync(id, host, port, cors, api);
        }

        public Task<bool> SendRequestAsync(string host, int port, string cors, object id = null)
        {
            return base.SendRequestAsync(id, host, port, cors);
        }

        public Task<bool> SendRequestAsync(string host, int port, object id = null)
        {
            return base.SendRequestAsync(id, host, port);
        }

        public Task<bool> SendRequestAsync(string host, object id = null)
        {
            return base.SendRequestAsync(id, host);
        }

        public Task<bool> SendRequestAsync(object id = null)
        {
            return base.SendRequestAsync(id);
        }
    }
}