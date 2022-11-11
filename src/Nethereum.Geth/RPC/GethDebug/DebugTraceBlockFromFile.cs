using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.Debug
{
    /// <Summary>
    ///     Similar to debug_traceBlock, traceBlockFromFile accepts a file containing the RLP of the block.
    /// </Summary>
    public class DebugTraceBlockFromFile : RpcRequestResponseHandler<JArray>, IDebugTraceBlockFromFile
    {
        public DebugTraceBlockFromFile(IClient client) : base(client, ApiMethods.debug_traceBlockFromFile.ToString())
        {
        }
         
        public RpcRequest BuildRequest(string filePath, TraceTransactionOptions options, object id = null)
        {
            return base.BuildRequest(id, filePath, options);
        }

        public Task<JArray> SendRequestAsync(string filePath, TraceTransactionOptions options, object id = null)
        {
            return base.SendRequestAsync(id, filePath, options);
        }
    }
}