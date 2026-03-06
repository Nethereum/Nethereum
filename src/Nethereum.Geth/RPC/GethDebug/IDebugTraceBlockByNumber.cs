using System.Threading.Tasks;
using Nethereum.RPC.DebugNode.Dtos.Tracing;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugTraceBlockByNumber
    {
        RpcRequest BuildRequest(ulong blockNumber, TracingCallOptions options, object id = null);
        Task<JArray> SendRequestAsync(ulong blockNumber, TracingCallOptions options = null, object id = null);
        Task<BlockResponseDto<TOutput>> SendRequestAsync<TOutput>(ulong blockNumber, TracingCallOptions options,
            object id = null);

    }
}