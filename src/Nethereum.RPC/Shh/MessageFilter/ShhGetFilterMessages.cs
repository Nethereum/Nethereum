using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Nethereum.RPC.Shh.DTOs;

namespace Nethereum.RPC.Shh.MessageFilter
{
    public class ShhGetFilterMessages : GenericRpcRequestResponseHandlerParamString<ShhMessage[]>, IShhGetFilterMessages
    {
        public ShhGetFilterMessages(IClient client) : base(client, ApiMethods.shh_getFilterMessages.ToString())
        {
        }
    }
}