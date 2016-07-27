using System.Threading.Tasks;
using EdjCase.JsonRpc.Core;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.DebugGeth
{
    /// <Summary>
    ///     Starts writing a Go runtime trace to the given file.
    /// </Summary>
    public class DebugStartGoTrace : RpcRequestResponseHandler<object>
    {
        public DebugStartGoTrace(IClient client) : base(client, ApiMethods.debug_startGoTrace.ToString())
        {
        }

        public Task<object> SendRequestAsync(string filePath, object id = null)
        {
            return base.SendRequestAsync(id, filePath);
        }

        public RpcRequest BuildRequest(string filePath, object id = null)
        {
            return base.BuildRequest(id, filePath);
        }
    }
}