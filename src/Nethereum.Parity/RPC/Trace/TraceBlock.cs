using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Trace
{
    /// <Summary>
    ///     Returns traces created at given block
    /// </Summary>
    public class TraceBlock : RpcRequestResponseHandler<JArray>, ITraceBlock
    {
        public TraceBlock(IClient client) : base(client, ApiMethods.trace_block.ToString())
        {
        }

        public Task<JArray> SendRequestAsync(HexBigInteger blockNumber, object id = null)
        {
            return base.SendRequestAsync(id, blockNumber);
        }

        public RpcRequest BuildRequest(HexBigInteger blockNumber, object id = null)
        {
            return base.BuildRequest(id, blockNumber);
        }
    }
}