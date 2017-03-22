using System.Threading.Tasks;
using EdjCase.JsonRpc.Core;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.Debug
{
    /// <Summary>
    ///     Similar to debug_traceBlock, traceBlockFromFile accepts a file containing the RLP of the block.
    /// </Summary>
    public class DebugTraceBlockFromFile : RpcRequestResponseHandler<JObject>
    {
        public DebugTraceBlockFromFile(IClient client) : base(client, ApiMethods.debug_traceBlockFromFile.ToString())
        {
        }

        public RpcRequest BuildRequest(string filePath, object id = null)
        {
            return base.BuildRequest(id, filePath);
        }

        public Task<JObject> SendRequestAsync(string filePath, object id = null)
        {
            return base.SendRequestAsync(id, filePath);
        }
    }
}