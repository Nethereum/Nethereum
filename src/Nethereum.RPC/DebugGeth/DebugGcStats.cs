using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.RPC
{
    /// <Summary>
    ///     Returns GC statistics.
    ///     See https://golang.org/pkg/runtime/debug/#GCStats for information about the fields of the returned object.
    ///     Example return: {{
    ///     "LastGC": "2016-06-13T11:50:25.0624198+01:00",
    ///     "NumGC": 37,
    ///     "PauseTotal": 77033500,
    ///     "Pause": [
    ///     1501500,
    ///     ...
    ///     ],
    ///     "PauseEnd": [
    ///     "2016-06-13T11:50:25.0624198+01:00",
    ///     ../
    ///     ],
    ///     "PauseQuantiles": null
    ///     }}
    /// </Summary>
    public class DebugGcStats : GenericRpcRequestResponseHandlerNoParam<JObject>
    {
        public DebugGcStats(IClient client) : base(client, ApiMethods.debug_gcStats.ToString())
        {
        }
    }
}