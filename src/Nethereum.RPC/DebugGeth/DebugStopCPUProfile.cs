
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.RPC
{

    ///<Summary>
    /// Stops an ongoing CPU profile.    
    ///</Summary>
    public class DebugStopCPUProfile : GenericRpcRequestResponseHandlerNoParam<object>
    {
            public DebugStopCPUProfile(IClient client) : base(client, ApiMethods.debug_stopCPUProfile.ToString()) { }
    }

}
            
        