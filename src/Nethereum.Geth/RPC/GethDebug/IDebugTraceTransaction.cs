using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.Geth.RPC.Debug.Tracers;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugTraceTransaction 
    {
        RpcRequest BuildRequest(string txnHash, TracingOptions options, object id = null);
        Task<JToken> SendRequestAsync(string txnHash, TracingOptions options, object id = null);
        Task<TOutputType> SendRequestAsync<TOutputType>(string txnHash, TracingOptions options,
            object id = null);
    }
}