using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.RPC.DebugGeth
{
    /// <Summary>
    ///     Similar to debug_traceBlock, traceBlockByHash accepts a block hash and will replay the block that is already
    ///     present in the database.
    /// </Summary>
    public class DebugTraceBlockByHash : RpcRequestResponseHandler<JObject>
    {
        public DebugTraceBlockByHash(IClient client) : base(client, ApiMethods.debug_traceBlockByHash.ToString())
        {
        }

        public Task<JObject> SendRequestAsync(string hash, object id = null)
        {
            return base.SendRequestAsync(id, hash);
        }

        public RpcRequest BuildRequest(string hash, object id = null)
        {
            return base.BuildRequest(id, hash);
        }
    }
}