using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Pantheon.RPC.Clique
{
    public interface ICliqueProposals : IGenericRpcRequestResponseHandlerNoParam<JObject>
    {
    }
}