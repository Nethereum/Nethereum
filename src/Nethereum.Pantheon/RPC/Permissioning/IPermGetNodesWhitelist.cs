using Nethereum.RPC.Infrastructure;

namespace Nethereum.Pantheon.RPC.Permissioning
{
    public interface IPermGetNodesWhitelist : IGenericRpcRequestResponseHandlerNoParam<string[]>
    {
    }
}