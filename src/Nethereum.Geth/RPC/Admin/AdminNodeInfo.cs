using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.RPC.Admin
{
    /// <Summary>
    ///     The nodeInfo administrative property can be queried for all the information known about the running Geth node at
    ///     the networking granularity. These include general information about the node itself as a participant of the ÐΞVp2p
    ///     P2P overlay protocol, as well as specialized information added by each of the running application protocols (e.g.
    ///     eth, les, shh, bzz).
    /// </Summary>
    public class AdminNodeInfo : GenericRpcRequestResponseHandlerNoParam<JObject>
    {
        public AdminNodeInfo(IClient client) : base(client, ApiMethods.admin_nodeInfo.ToString())
        {
        }
    }
}