using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugTraceCall
    {
        RpcRequest BuildRequest(CallInput callArgs, string blockNrOrHash, TracingCallOptions options, object id = null);

        Task<JObject> SendRequestAsync(CallInput callArgs, string blockNrOrHash, TracingCallOptions options,
            object id = null);

        Task<TOutputType> SendRequestAsync<TOutputType>(CallInput callArgs, string blockNrOrHash,
            TracingCallOptions options, object id = null);

    }
}