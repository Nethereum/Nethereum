using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Nethereum.RPC.Shh.DTOs;

namespace Nethereum.RPC.Shh.MessageFilter
{
    public interface IShhGetFilterMessages : IGenericRpcRequestResponseHandlerParamString<ShhMessage[]>
    {

    }
}