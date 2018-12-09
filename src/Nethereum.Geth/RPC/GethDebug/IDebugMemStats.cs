using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugMemStats : IGenericRpcRequestResponseHandlerNoParam<JObject>
    {

    }
}