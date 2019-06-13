using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Pantheon.RPC.Permissioning
{
    /// <Summary>
    ///     Removes nodes from the nodes whitelist.
    /// </Summary>
    public class PermRemoveNodesFromWhitelist : RpcRequestResponseHandler<string>, IPermRemoveNodesFromWhitelist
    {
        public PermRemoveNodesFromWhitelist(IClient client) : base(client,
            ApiMethods.perm_removeNodesFromWhitelist.ToString())
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