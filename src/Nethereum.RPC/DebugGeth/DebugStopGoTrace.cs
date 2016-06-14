
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.RPC
{

    ///<Summary>
    /// Stops writing the Go runtime trace.    
    ///</Summary>
    public class DebugStopGoTrace : GenericRpcRequestResponseHandlerNoParam<object>
    {
            public DebugStopGoTrace(IClient client) : base(client, ApiMethods.debug_stopGoTrace.ToString()) { }
    }

}
            
        