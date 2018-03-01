using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Trace
{
    /// <Summary>
    ///     Executes the given call and returns a number of possible traces for it.
    /// </Summary>
    public class TraceCall : RpcRequestResponseHandler<JObject>
    {
        public TraceCall(IClient client) : base(client, ApiMethods.trace_call.ToString())
        {
        }

        public async Task<JObject> SendRequestAsync(CallInput callInput, TraceType[] typeOfTrace, BlockParameter block,
            object id = null)
        {
            return await base.SendRequestAsync(id, callInput, typeOfTrace.ConvertToStringArray(), block);
        }

        public RpcRequest BuildRequest(CallInput callInput, TraceType[] typeOfTrace, BlockParameter block,
            object id = null)
        {
            return base.BuildRequest(id, callInput, typeOfTrace.ConvertToStringArray(), block);
        }
    }
}