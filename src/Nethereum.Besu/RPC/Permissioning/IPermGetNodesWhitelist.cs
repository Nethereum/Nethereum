using Nethereum.RPC.Infrastructure;

namespace Nethereum.Besu.RPC.Permissioning
{
    public interface IPermGetNodesWhitelist : IGenericRpcRequestResponseHandlerNoParam<string[]>
    {
    }
}