using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugTraceTransaction
    {
        RpcRequest BuildRequest(string txnHash, TraceTransactionOptions options, object id = null);
        Task<JObject> SendRequestAsync(string txnHash, TraceTransactionOptions options, object id = null);
    }
}