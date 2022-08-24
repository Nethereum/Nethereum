using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.RPC.DebugNode
{
    public class DebugGetBadBlocks : GenericRpcRequestResponseHandlerNoParam<BadBlock[]>, IDebugGetBadBlocks
    {
        public DebugGetBadBlocks(IClient client) : base(client, ApiMethods.debug_getBadBlocks.ToString())
        {
        }
    }
}
