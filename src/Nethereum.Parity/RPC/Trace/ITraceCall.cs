using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Trace
{
    public interface ITraceCall
    {
        RpcRequest BuildRequest(CallInput callInput, TraceType[] typeOfTrace, BlockParameter block, object id = null);
        Task<JObject> SendRequestAsync(CallInput callInput, TraceType[] typeOfTrace, BlockParameter block, object id = null);
    }
}