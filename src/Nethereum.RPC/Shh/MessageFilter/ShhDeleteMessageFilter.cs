using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.RPC.Shh.MessageFilter
{
    public class ShhDeleteMessageFilter : GenericRpcRequestResponseHandlerParamString<bool>, IShhDeleteMessageFilter
    {
        public ShhDeleteMessageFilter(IClient client) : base(client, ApiMethods.shh_deleteMessageFilter.ToString())
        {
        }
    }
}
