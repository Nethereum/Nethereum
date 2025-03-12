using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugTraceBlockByNumber
    {
        RpcRequest BuildRequest(ulong blockNumber, TracingCallOptions options, object id = null);
        Task<JArray> SendRequestAsync(ulong blockNumber, TracingCallOptions options = null, object id = null);
        Task<BlockResponseDto<TOutputType>> SendRequestAsync<TOutputType>(ulong blockNumber, TracingCallOptions options,
            object id = null);

    }
}