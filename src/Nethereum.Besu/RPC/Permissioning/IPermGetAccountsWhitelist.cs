using Nethereum.RPC.Infrastructure;

namespace Nethereum.Besu.RPC.Permissioning
{
    public interface IPermGetAccountsWhitelist : IGenericRpcRequestResponseHandlerNoParam<string[]>
    {
    }
}