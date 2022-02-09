using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.Debug
{
    /// <Summary>
    ///     The debug_traceCall method lets you run an eth_call within the context of the given block execution using the
    ///     final state of parent block as the base. The block can be specified either by hash or by number. It takes the
    ///     same input object as a eth_call. It returns the same output as debug_traceTransaction. A tracer can be specified
    ///     as a third argument, similar to debug_traceTransaction.
    ///
    ///     The possible options are:
    ///         from: DATA, 20 Bytes - (optional) The address the transaction is sent from.
    ///         to: DATA, 20 Bytes - The address the transaction is directed to.
    ///         gas: QUANTITY - (optional) Integer of the gas provided for the transaction execution.eth_call consumes zero gas, but this parameter may be needed by some executions.
    ///         gasPrice: QUANTITY - (optional) Integer of the gasPrice used for each paid gas
    ///         value: QUANTITY - (optional) Integer of the value sent with this transaction
    ///         data: DATA - (optional) Hash of the method signature and encoded parameters.For details see Ethereum Contract ABI in the Solidity documentation
    /// </Summary>
    public class DebugTraceCall : RpcRequestResponseHandler<JObject>, IDebugTraceCall
    {
        public DebugTraceCall(IClient client) : base(client, ApiMethods.debug_traceCall.ToString())
        {
        } 

        public RpcRequest BuildRequest(CallInput callArgs, string blockNrOrHash, TraceCallOptions options, object id = null)
        {
            return base.BuildRequest(id, callArgs, blockNrOrHash, options);
        }

        public Task<JObject> SendRequestAsync(CallInput callArgs, string blockNrOrHash, TraceCallOptions options, object id = null)
        {
            return base.SendRequestAsync(id, callArgs, blockNrOrHash, options);
        }
    }
}