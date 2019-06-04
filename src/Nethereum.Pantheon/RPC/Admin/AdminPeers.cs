using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Pantheon.RPC.Admin
{
    /// <Summary>
    ///     Returns networking information about connected remote nodes.
    /// </Summary>
    public class AdminPeers : GenericRpcRequestResponseHandlerNoParam<JArray>, IAdminPeers
    {
        public AdminPeers(IClient client) : base(client, ApiMethods.admin_peers.ToString())
        {
        }
    }
}