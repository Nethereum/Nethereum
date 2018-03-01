using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Trace.TraceDTOs;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Trace
{
    /// <Summary>
    ///     Returns traces matching given filter
    /// </Summary>
    public class TraceFilter : RpcRequestResponseHandler<JArray>
    {
        public TraceFilter(IClient client) : base(client, ApiMethods.trace_filter.ToString())
        {
        }

        public async Task<JArray> SendRequestAsync(TraceFilterDTO traceFilter, object id = null)
        {
            return await base.SendRequestAsync(id, traceFilter);
        }

        public RpcRequest BuildRequest(TraceFilterDTO traceFilter, object id = null)
        {
            return base.BuildRequest(id, traceFilter);
        }
    }
}