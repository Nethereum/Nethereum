using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.DebugNode.Dtos.Tracing;
using Newtonsoft.Json.Linq;

namespace Nethereum.RPC.DebugNode
{
    public class DebugTraceTransaction : RpcRequestResponseHandler<JToken>, IDebugTraceTransaction
    {
        public DebugTraceTransaction(IClient client) : base(client, ApiMethods.debug_traceTransaction.ToString())
        {
        }

        public RpcRequest BuildRequest(string txnHash, TracingOptions options, object id = null)
        {
            return base.BuildRequest(id, txnHash, options.ToDto());
        }

        public Task<JToken> SendRequestAsync(string txnHash, TracingOptions options, object id = null)
        {
            return base.SendRequestAsync(id, txnHash, options.ToDto());
        }

        public async Task<TOutput> SendRequestAsync<TOutput>(string txnHash, TracingOptions options, object id = null)
        {
            var rawResult = await base.SendRequestAsync(id, txnHash, options.ToDto());
            return rawResult.ToObject<TOutput>();
        }
    }
}
