using System.Threading.Tasks;
 
using Nethereum.JsonRpc.Client;

namespace Nethereum.Geth.RPC.Admin
{
    /// <Summary>
    ///     The startWS administrative method starts an WebSocket based JSON RPC API webserver to handle client requests. All
    ///     the parameters are optional:
    ///     host: network interface to open the listener socket on (defaults to "localhost")
    ///     port: network port to open the listener socket on (defaults to 8546)
    ///     cors: cross-origin resource sharing header to use (defaults to "")
    ///     apis: API modules to offer over this interface (defaults to "eth,net,web3")
    ///     The method returns a boolean flag specifying whether the WebSocket RPC listener was opened or not. Please note,
    ///     only one WebSocket endpoint is allowed to be active at any time.
    /// </Summary>
    public class AdminStartWS : RpcRequestResponseHandler<bool>
    {
        public AdminStartWS(IClient client) : base(client, ApiMethods.admin_startWS.ToString())
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