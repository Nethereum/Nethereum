using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Pantheon.RPC.Debug
{
    public interface IDebugMetrics : IGenericRpcRequestResponseHandlerNoParam<JObject>
    {
    }
}