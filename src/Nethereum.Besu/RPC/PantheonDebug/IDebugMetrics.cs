using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Besu.RPC.Debug
{
    public interface IDebugMetrics : IGenericRpcRequestResponseHandlerNoParam<JObject>
    {
    }
}