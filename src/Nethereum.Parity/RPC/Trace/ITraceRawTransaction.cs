using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Trace
{
    public interface ITraceRawTransaction
    {
        RpcRequest BuildRequest(string rawTransaction, TraceType[] traceTypes, object id = null);
        Task<JObject> SendRequestAsync(string rawTransaction, TraceType[] traceTypes, object id = null);
    }
}