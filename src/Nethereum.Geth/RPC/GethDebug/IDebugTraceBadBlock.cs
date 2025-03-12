using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugTraceBadBlock
    {
        RpcRequest BuildRequest(string blockHash, TracingCallOptions options, object id = null);
        Task<JArray> SendRequestAsync(string blockHash, TracingCallOptions options, object id = null);
        Task<BlockResponseDto<TOutput>> SendRequestAsync<TOutput>(string blockHash, TracingCallOptions options, object id = null);

    }
}