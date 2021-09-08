using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.Debug
{
    /// <Summary>
    ///     Similar to debug_traceBlock, traceBlockByHash accepts a block hash and will replay the block that is already
    ///     present in the database.
    /// </Summary>
    public class DebugTraceBlockByHash : RpcRequestResponseHandler<JArray>, IDebugTraceBlockByHash
    {
        public DebugTraceBlockByHash(IClient client) : base(client, ApiMethods.debug_traceBlockByHash.ToString())
        {
        }

        public RpcRequest BuildRequest(string hash, TraceTransactionOptions options, object id = null)
        {
            return base.BuildRequest(id, hash, options);
        }

        public Task<JArray> SendRequestAsync(string hash, TraceTransactionOptions options, object id = null)
        {
            return base.SendRequestAsync(id, hash, options);
        }
    }
}