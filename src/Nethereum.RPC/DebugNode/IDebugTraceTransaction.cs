using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.DebugNode.Dtos.Tracing;
using Newtonsoft.Json.Linq;

namespace Nethereum.RPC.DebugNode
{
    public interface IDebugTraceTransaction
    {
        RpcRequest BuildRequest(string txnHash, TracingOptions options, object id = null);
        Task<JToken> SendRequestAsync(string txnHash, TracingOptions options, object id = null);
        Task<TOutput> SendRequestAsync<TOutput>(string txnHash, TracingOptions options,
            object id = null);
    }
}
