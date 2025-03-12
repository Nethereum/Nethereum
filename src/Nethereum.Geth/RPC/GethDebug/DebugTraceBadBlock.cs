using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.Debug
{
    /// <Summary>
    ///     Returns the structured logs created during the execution of EVM against a block pulled from the pool of
    ///     bad ones and returns them as a JSON object.
    /// </Summary>
    public class DebugTraceBadBlock : RpcRequestResponseHandler<JArray>, IDebugTraceBadBlock
    {
        public DebugTraceBadBlock(IClient client) : base(client, ApiMethods.debug_traceBadBlock.ToString())
        {
        }

        public RpcRequest BuildRequest(string blockHash, TracingCallOptions options, object id = null)
        {
            return base.BuildRequest(id, blockHash, options.ToDto());
        }
        
        public Task<JArray> SendRequestAsync(string blockHash, TracingCallOptions options, object id = null)
        {
            return base.SendRequestAsync(id, blockHash, options.ToDto());
        }

        public async Task<BlockResponseDto<TOutputType>> SendRequestAsync<TOutputType>(string blockHash, TracingCallOptions options, object id = null)
        {
            var rawResult = await base.SendRequestAsync(id, blockHash, options.ToDto());
            return rawResult.ToObject<BlockResponseDto<TOutputType>>();
        }
    }
}