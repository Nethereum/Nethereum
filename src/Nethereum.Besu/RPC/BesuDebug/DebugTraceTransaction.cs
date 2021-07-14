using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Besu.RPC.Debug
{
    /// <Summary>
    ///     Remix uses debug_traceTransaction to implement debugging. Use the Debugger tab in Remix rather than calling
    ///     debug_traceTransaction directly.
    ///     Reruns the transaction with the same state as when the transaction was executed.
    ///     Parameters
    ///     transactionHash : data - Transaction hash.
    ///     Object - request options (all optional and default to false): * disableStorage : boolean - true disables storage
    ///     capture. * disableMemory : boolean - true disables memory capture. * disableStack : boolean - true disables stack
    ///     capture.
    /// </Summary>
    public class DebugTraceTransaction : RpcRequestResponseHandler<JObject>, IDebugTraceTransaction
    {
        public DebugTraceTransaction(IClient client) : base(client, ApiMethods.debug_traceTransaction.ToString())
        {
        }

        public async Task<JObject> SendRequestAsync(string transactionHash, object id = null)
        {
            return await base.SendRequestAsync(id, transactionHash);
        }

        public RpcRequest BuildRequest(string transactionHash, object id = null)
        {
            return base.BuildRequest(id, transactionHash);
        }
    }
}