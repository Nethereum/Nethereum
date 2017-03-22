using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Geth.RPC.Debug
{
    /// <Summary>
    ///     Returns a printed representation of the stacks of all goroutines.
    /// </Summary>
    public class DebugStacks : GenericRpcRequestResponseHandlerNoParam<string>
    {
        public DebugStacks(IClient client) : base(client, ApiMethods.debug_stacks.ToString())
        {
        }
    }
}