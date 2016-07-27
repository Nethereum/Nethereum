using System.Threading.Tasks;
using EdjCase.JsonRpc.Core;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.RPC.DebugGeth
{
    /// <Summary>
    ///     Similar to debug_traceBlock, traceBlockByNumber accepts a block number and will replay the block that is already
    ///     present in the database.
    /// </Summary>
    public class DebugTraceBlockByNumber : RpcRequestResponseHandler<JObject>
    {
        public DebugTraceBlockByNumber(IClient client) : base(client, ApiMethods.debug_traceBlockByNumber.ToString())
        {
        }

        public Task<JObject> SendRequestAsync(long blockNumber, object id = null)
        {
            return base.SendRequestAsync(id, blockNumber);
        }

        public RpcRequest BuildRequest(long blockNumber, object id = null)
        {
            return base.BuildRequest(id, blockNumber);
        }
    }
}