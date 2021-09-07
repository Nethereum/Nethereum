using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugTraceBlockFromFile
    {
        RpcRequest BuildRequest(string filePath, TraceTransactionOptions options, object id = null);
        Task<JArray> SendRequestAsync(string filePath, TraceTransactionOptions options, object id = null);
    }
}