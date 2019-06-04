using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Pantheon.RPC.Clique
{
    /// <Summary>
    ///     Returns current proposals.
    /// </Summary>
    public class CliqueProposals : GenericRpcRequestResponseHandlerNoParam<JObject>, ICliqueProposals
    {
        public CliqueProposals(IClient client) : base(client, ApiMethods.clique_proposals.ToString())
        {
        }
    }
}