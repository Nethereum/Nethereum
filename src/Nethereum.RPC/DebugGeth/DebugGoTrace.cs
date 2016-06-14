using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.DebugGeth
{
    /// <Summary>
    ///     Turns on Go runtime tracing for the given duration and writes trace data to disk.
    /// </Summary>
    public class DebugGoTrace : RpcRequestResponseHandler<object>
    {
        public DebugGoTrace(IClient client) : base(client, ApiMethods.debug_goTrace.ToString())
        {
        }

        public Task<object> SendRequestAsync(string fileName, int seconds, object id = null)
        {
            return base.SendRequestAsync(id, fileName, seconds);
        }

        public RpcRequest BuildRequest(string fileName, int seconds, object id = null)
        {
            return base.BuildRequest(id, fileName, seconds);
        }
    }
}