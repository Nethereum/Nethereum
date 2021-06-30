using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Besu.RPC.IBFT
{
    public interface IIbftGetPendingVotes : IGenericRpcRequestResponseHandlerNoParam<JObject>
    {
    }
}