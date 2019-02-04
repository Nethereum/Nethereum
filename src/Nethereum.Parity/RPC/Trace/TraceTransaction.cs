using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Trace
{
    /// <Summary>
    ///     Returns all traces of given transaction
    /// </Summary>
    public class TraceTransaction : RpcRequestResponseHandler<JArray>, ITraceTransaction
    {
        public TraceTransaction(IClient client) : base(client, ApiMethods.trace_transaction.ToString())
        {
        }

        public async Task<JArray> SendRequestAsync(string transactionHash, object id = null)
        {
            return await base.SendRequestAsync(id, transactionHash);
        }

        public RpcRequest BuildRequest(string transactionHash, object id = null)
        {
            return base.BuildRequest(id, transactionHash);
        }
    }
}
