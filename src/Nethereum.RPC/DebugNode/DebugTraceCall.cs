using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.DebugNode.Dtos.Tracing;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json.Linq;

namespace Nethereum.RPC.DebugNode
{
    public class DebugTraceCall : RpcRequestResponseHandler<JObject>, IDebugTraceCall
    {
        public DebugTraceCall(IClient client) : base(client, ApiMethods.debug_traceCall.ToString())
        {
        }

        public RpcRequest BuildRequest(CallInput callArgs, string blockNrOrHash, TracingCallOptions options, object id = null)
        {
            return base.BuildRequest(id, callArgs, blockNrOrHash, options.ToDto());
        }

        public Task<JObject> SendRequestAsync(CallInput callArgs, string blockNrOrHash, TracingCallOptions options, object id = null)
        {
            return base.SendRequestAsync(id, callArgs, blockNrOrHash, options.ToDto());
        }

        public async Task<TOutput> SendRequestAsync<TOutput>(CallInput callArgs, string blockNrOrHash, TracingCallOptions options, object id = null)
        {
            var rawResult = await base.SendRequestAsync(id, callArgs, blockNrOrHash, options.ToDto());
            return rawResult.ToObject<TOutput>();
        }
    }
}
