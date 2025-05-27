using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugTraceTransaction 
    {
        RpcRequest BuildRequest(string txnHash, TracingOptions options, object id = null);
        Task<JToken> SendRequestAsync(string txnHash, TracingOptions options, object id = null);
        Task<TOutput> SendRequestAsync<TOutput>(string txnHash, TracingOptions options,
            object id = null);
    }
}