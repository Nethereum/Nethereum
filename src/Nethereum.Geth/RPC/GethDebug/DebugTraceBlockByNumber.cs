using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.Debug
{
    /// <Summary>
    ///     Similar to debug_traceBlock, traceBlockByNumber accepts a block number and will replay the block that is already
    ///     present in the database.
    /// </Summary>
    public class DebugTraceBlockByNumber : RpcRequestResponseHandler<JArray>, IDebugTraceBlockByNumber
    {
        public DebugTraceBlockByNumber(IClient client) : base(client, ApiMethods.debug_traceBlockByNumber.ToString())
        {
        }

        public RpcRequest BuildRequest(ulong blockNumber, TracingCallOptions options, object id = null)
        {
            return base.BuildRequest(id, new HexBigInteger(blockNumber), options.ToDto());
        }
        
        public Task<JArray> SendRequestAsync(ulong blockNumber, TracingCallOptions options, object id = null)
        {
            return base.SendRequestAsync(id, new HexBigInteger(blockNumber), options.ToDto());
        }

        public async Task<BlockResponseDto<TOutputType>> SendRequestAsync<TOutputType>(ulong blockNumber, TracingCallOptions options, object id = null)
        {
            var rawResult = await base.SendRequestAsync(id, new HexBigInteger(blockNumber), options.ToDto());
            return rawResult.ToObject<BlockResponseDto<TOutputType>>();
        }
    }
}