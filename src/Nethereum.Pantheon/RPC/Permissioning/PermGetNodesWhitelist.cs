using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Pantheon.RPC.Permissioning
{
    /// <Summary>
    ///     Adds nodes to the nodes whitelist.
    /// </Summary>
    public class PermGetNodesWhitelist : GenericRpcRequestResponseHandlerNoParam<string[]>, IPermGetNodesWhitelist
    {
        public PermGetNodesWhitelist(IClient client) : base(client, ApiMethods.perm_getNodesWhitelist.ToString())
        {
        }
    }
}