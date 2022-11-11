using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Trace.TraceDTOs;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Trace
{
    public interface ITraceFilter
    {
        RpcRequest BuildRequest(TraceFilterDTO traceFilter, object id = null);
        Task<JArray> SendRequestAsync(TraceFilterDTO traceFilter, object id = null);
    }
}