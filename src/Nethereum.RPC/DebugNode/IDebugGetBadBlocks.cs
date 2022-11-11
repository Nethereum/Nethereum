using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.RPC.DebugNode
{
    public interface IDebugGetBadBlocks : IGenericRpcRequestResponseHandlerNoParam<BadBlock[]>
    {

    }
}