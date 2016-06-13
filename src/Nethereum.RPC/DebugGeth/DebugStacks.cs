
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.RPC
{

    ///<Summary>
    /// Returns a printed representation of the stacks of all goroutines.     
    ///</Summary>
    public class DebugStacks : GenericRpcRequestResponseHandlerNoParam<string>
    {
            public DebugStacks(IClient client) : base(client, ApiMethods.debug_stacks.ToString()) { }
    }

}
            
        