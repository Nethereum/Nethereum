using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.Debug
{
    /// <Summary>
    ///     Returns detailed runtime memory statistics.
    ///     See https://golang.org/pkg/runtime/#MemStats for information about the fields of the returned object.
    ///     Example : Last = {"BySize": [
    ///     {
    ///     "Size": 0,
    ///     "Mallocs": 0,
    ///     "Frees": 0
    ///     },
    /// </Summary>
    public class DebugMemStats : GenericRpcRequestResponseHandlerNoParam<JObject>
    {
        public DebugMemStats(IClient client) : base(client, ApiMethods.debug_memStats.ToString())
        {
        }
    }
}