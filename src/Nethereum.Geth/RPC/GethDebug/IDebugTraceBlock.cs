using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugTraceBlock
    {
        RpcRequest BuildRequest(string blockRlpHex, TraceTransactionOptions options, object id = null);
        Task<JArray> SendRequestAsync(string blockRlpHex, TraceTransactionOptions options, object id = null);
    }
}