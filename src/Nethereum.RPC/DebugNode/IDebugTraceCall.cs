using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.DebugNode.Dtos.Tracing;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json.Linq;

namespace Nethereum.RPC.DebugNode
{
    public interface IDebugTraceCall
    {
        RpcRequest BuildRequest(CallInput callArgs, string blockNrOrHash, TracingCallOptions options, object id = null);

        Task<JObject> SendRequestAsync(CallInput callArgs, string blockNrOrHash, TracingCallOptions options,
            object id = null);

        Task<TOutput> SendRequestAsync<TOutput>(CallInput callArgs, string blockNrOrHash,
            TracingCallOptions options, object id = null);
    }
}
