using System.Threading.Tasks;
using Nethereum.RPC.DebugNode.Dtos.Tracing;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugTraceBlock
    {
        RpcRequest BuildRequest(string blockRlpHex, TracingCallOptions options, object id = null);
        Task<JArray> SendRequestAsync(string blockRlpHex, TracingCallOptions options, object id = null);

        Task<BlockResponseDto<TOutput>> SendRequestAsync<TOutput>(string blockRlpHex,
            TracingCallOptions options, object id = null);

    }
}