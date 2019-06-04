using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Pantheon.RPC.IBFT
{
    public interface IIbftGetPendingVotes : IGenericRpcRequestResponseHandlerNoParam<JObject>
    {
    }
}