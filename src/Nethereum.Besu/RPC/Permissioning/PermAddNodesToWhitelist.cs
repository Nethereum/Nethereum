using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Besu.RPC.Permissioning
{
    /// <Summary>
    ///     Adds nodes to the nodes whitelist.
    /// </Summary>
    public class PermAddNodesToWhitelist : RpcRequestResponseHandler<string>, IPermAddNodesToWhitelist
    {
        public PermAddNodesToWhitelist(IClient client) : base(client, ApiMethods.perm_addNodesToWhitelist.ToString())
        {
        }

        public async Task<string> SendRequestAsync(string[] addresses, object id = null)
        {
            return await base.SendRequestAsync(id, new object[] { addresses });
        }

        public RpcRequest BuildRequest(string[] addresses, object id = null)
        {
            return base.BuildRequest(id, new object[] { addresses });
        }
    }
}