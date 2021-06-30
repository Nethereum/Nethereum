using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Besu.RPC.Clique
{
    public interface ICliqueProposals : IGenericRpcRequestResponseHandlerNoParam<JObject>
    {
    }
}